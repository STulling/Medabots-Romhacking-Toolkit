namespace Medabots.Rom.Battles;

public sealed record BattleDefinition(
    int Id,
    int PointerOffset,
    int DataOffset,
    byte CharacterId,
    byte Unknown1,
    byte NumberOfBots,
    IReadOnlyList<BattleBot> Bots,
    byte AlwaysZero);
