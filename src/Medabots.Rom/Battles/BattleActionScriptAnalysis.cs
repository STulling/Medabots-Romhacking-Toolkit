namespace Medabots.Rom.Battles;

public sealed record BattleActionScriptAnalysisNode(
    int RelativeOffset,
    byte Value,
    bool IsLabel,
    string DisplayName,
    string Summary,
    IReadOnlyList<byte> InlineArguments,
    uint HandlerRomAddress,
    int HandlerOffset);
