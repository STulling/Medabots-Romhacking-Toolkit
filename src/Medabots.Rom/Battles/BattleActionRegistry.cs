using System.Reflection;
using System.Text.Json;

namespace Medabots.Rom.Battles;

public sealed class BattleActionRegistry
{
    private readonly IReadOnlyDictionary<byte, BattleActionOpcodeDefinition> _opcodeDefinitions;
    private readonly IReadOnlyDictionary<byte, BattleActionRouteDefinition> _routes;

    private BattleActionRegistry(
        IReadOnlyDictionary<byte, BattleActionOpcodeDefinition> opcodeDefinitions,
        IReadOnlyDictionary<byte, BattleActionRouteDefinition> routes)
    {
        _opcodeDefinitions = opcodeDefinitions;
        _routes = routes;
    }

    public static BattleActionRegistry LoadDefault()
    {
        var assembly = Assembly.GetExecutingAssembly();
        const string resourceName = "Medabots.Rom.battleactions.json";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' was not found.");
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();

        var document = JsonSerializer.Deserialize<RegistryDocument>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidDataException("The embedded battle action registry is invalid.");

        var opcodeDefinitions = document.Opcodes.ToDictionary(
            entry => Convert.ToByte(entry.Key, 16),
            entry => new BattleActionOpcodeDefinition(
                Convert.ToByte(entry.Key, 16),
                entry.Value.Name,
                entry.Value.HandlerName,
                entry.Value.Summary,
                entry.Value.InlineArgumentCount));

        var routes = document.Actions.ToDictionary(
            entry => Convert.ToByte(entry.Key, 16),
            entry => new BattleActionRouteDefinition(
                Convert.ToByte(entry.Key, 16),
                entry.Value.FamilyHandler,
                entry.Value.FamilySummary,
                entry.Value.FamilySubsequence,
                entry.Value.SharedScriptName,
                entry.Value.SharedScriptSummary,
                entry.Value.KnownOpcodeSequence.Select(value => Convert.ToByte(value, 16)).ToArray(),
                entry.Value.ActualFlow,
                entry.Value.Notes));

        return new BattleActionRegistry(opcodeDefinitions, routes);
    }

    public bool TryGetOpcodeDefinition(byte opcode, out BattleActionOpcodeDefinition definition)
        => _opcodeDefinitions.TryGetValue(opcode, out definition!);

    public bool TryGetRoute(byte actionId, out BattleActionRouteDefinition route)
        => _routes.TryGetValue(actionId, out route!);

    public IReadOnlyDictionary<byte, BattleActionOpcodeDefinition> GetOpcodeDefinitions()
        => _opcodeDefinitions;

    public BattleActionAnalysis Analyze(
        byte actionId,
        string actionName,
        IReadOnlyList<BattleActionOpcodeEntry> opcodeTable,
        IReadOnlyList<BattleActionScriptEntry> scriptTable)
    {
        var route = _routes.TryGetValue(actionId, out var routeDefinition) ? routeDefinition : null;
        var opcodes = new List<BattleActionOpcodeAnalysis>();
        var script = scriptTable.FirstOrDefault(entry => entry.ActionScriptId == actionId);
        var scriptNodes = new List<BattleActionScriptAnalysisNode>();

        if (route is not null)
        {
            foreach (var opcode in route.KnownOpcodeSequence)
            {
                var tableEntry = opcodeTable.FirstOrDefault(entry => entry.Opcode == opcode);
                var definition = _opcodeDefinitions.TryGetValue(opcode, out var opcodeDefinition)
                    ? opcodeDefinition
                    : new BattleActionOpcodeDefinition(opcode, $"Opcode 0x{opcode:X2}", "Unknown", "No registry summary yet.", 0);

                opcodes.Add(new BattleActionOpcodeAnalysis(
                    opcode,
                    definition.Name,
                    definition.HandlerName,
                    definition.Summary,
                    definition.InlineArgumentCount,
                    tableEntry?.HandlerRomAddress ?? 0,
                    tableEntry?.HandlerOffset ?? 0));
            }
        }

        if (script is not null)
        {
            var parsedScript = new BattleActionScriptParser().Parse(script, _opcodeDefinitions);
            foreach (var node in parsedScript.Nodes)
            {
                if (node.IsLabel)
                {
                    scriptNodes.Add(new BattleActionScriptAnalysisNode(
                        node.RelativeOffset,
                        node.Value,
                        true,
                        $"Label 0x{node.Value:X2}",
                        "Branch label used by action-script match opcodes.",
                        node.InlineArguments,
                        0,
                        0));
                    continue;
                }

                var definition = _opcodeDefinitions.TryGetValue(node.Value, out var opcodeDefinition)
                    ? opcodeDefinition
                    : new BattleActionOpcodeDefinition(node.Value, $"Opcode 0x{node.Value:X2}", "Unknown", "No registry summary yet.", node.InlineArguments.Count);
                var tableEntry = opcodeTable.FirstOrDefault(entry => entry.Opcode == node.Value);

                scriptNodes.Add(new BattleActionScriptAnalysisNode(
                    node.RelativeOffset,
                    node.Value,
                    false,
                    definition.Name,
                    definition.Summary,
                    node.InlineArguments,
                    tableEntry?.HandlerRomAddress ?? 0,
                    tableEntry?.HandlerOffset ?? 0));
            }
        }

        return new BattleActionAnalysis(actionId, actionName, route, opcodes, script, scriptNodes);
    }

    private sealed class RegistryDocument
    {
        public Dictionary<string, OpcodeDocument> Opcodes { get; set; } = [];

        public Dictionary<string, ActionDocument> Actions { get; set; } = [];
    }

    private sealed class OpcodeDocument
    {
        public string Name { get; set; } = string.Empty;

        public string HandlerName { get; set; } = string.Empty;

        public string Summary { get; set; } = string.Empty;

        public int InlineArgumentCount { get; set; }
    }

    private sealed class ActionDocument
    {
        public string FamilyHandler { get; set; } = string.Empty;

        public string FamilySummary { get; set; } = string.Empty;

        public string? FamilySubsequence { get; set; }

        public string? SharedScriptName { get; set; }

        public string? SharedScriptSummary { get; set; }

        public List<string> KnownOpcodeSequence { get; set; } = [];

        public List<string> ActualFlow { get; set; } = [];

        public List<string> Notes { get; set; } = [];
    }
}
