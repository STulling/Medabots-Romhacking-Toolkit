namespace Medabots.Rom.Battles;

public sealed record BattleActionAnalysis(
    byte ActionId,
    string ActionName,
    BattleActionRouteDefinition? Route,
    IReadOnlyList<BattleActionOpcodeAnalysis> Opcodes,
    BattleActionScriptEntry? Script,
    IReadOnlyList<BattleActionScriptAnalysisNode> ScriptNodes);

public sealed record BattleActionOpcodeAnalysis(
    byte Opcode,
    string Name,
    string HandlerName,
    string Summary,
    int InlineArgumentCount,
    uint HandlerRomAddress,
    int HandlerOffset);
