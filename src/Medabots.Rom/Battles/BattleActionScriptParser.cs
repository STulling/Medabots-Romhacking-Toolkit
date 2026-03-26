namespace Medabots.Rom.Battles;

public sealed class BattleActionScriptParser
{
    public BattleActionScriptParseResult Parse(
        BattleActionScriptEntry script,
        IReadOnlyDictionary<byte, BattleActionOpcodeDefinition> opcodeDefinitions)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(opcodeDefinitions);

        var nodes = new List<BattleActionScriptNode>();
        var offset = 0;

        while (offset < script.ScriptBytes.Count)
        {
            var value = script.ScriptBytes[offset];
            if (value >= 0x80)
            {
                nodes.Add(new BattleActionScriptNode(offset, value, true, []));
                offset++;
                continue;
            }

            var inlineArgumentCount = opcodeDefinitions.TryGetValue(value, out var definition)
                ? definition.InlineArgumentCount
                : 0;

            if (offset + inlineArgumentCount >= script.ScriptBytes.Count)
            {
                inlineArgumentCount = Math.Max(0, script.ScriptBytes.Count - offset - 1);
            }

            var inlineArgs = new byte[inlineArgumentCount];
            for (var i = 0; i < inlineArgumentCount; i++)
            {
                inlineArgs[i] = script.ScriptBytes[offset + 1 + i];
            }

            nodes.Add(new BattleActionScriptNode(offset, value, false, inlineArgs));
            offset += 1 + inlineArgumentCount;
        }

        return new BattleActionScriptParseResult(script, nodes);
    }
}
