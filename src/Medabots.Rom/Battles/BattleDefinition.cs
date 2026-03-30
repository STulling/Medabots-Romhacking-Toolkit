namespace Medabots.Rom.Battles;

public sealed record BattleDefinition(
    int Id,
    int PointerOffset,
    int DataOffset,
    byte CharacterId,
    byte InitializationMode,
    byte NumberOfBots,
    byte TemplateFlags,
    IReadOnlyList<BattleBot> Bots);
