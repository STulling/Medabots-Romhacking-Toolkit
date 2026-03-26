namespace Medabots.Rom.Metadata;

public sealed record MedabotsRomAddresses(
    int TextPointerTableOffset,
    int TextDumpOffset,
    int StarterOffset,
    int BattlePointerTableOffset,
    int BattleCount,
    int EventTableOffset,
    int EventCount);
