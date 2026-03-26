namespace Medabots.Rom.Battles;

public sealed record BattleActionScriptParseResult(
    BattleActionScriptEntry Script,
    IReadOnlyList<BattleActionScriptNode> Nodes);
