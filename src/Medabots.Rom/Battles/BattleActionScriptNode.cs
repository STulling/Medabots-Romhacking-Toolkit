namespace Medabots.Rom.Battles;

public sealed record BattleActionScriptNode(
    int RelativeOffset,
    byte Value,
    bool IsLabel,
    IReadOnlyList<byte> InlineArguments);
