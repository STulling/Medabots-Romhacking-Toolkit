namespace Medabots.Rom.Metadata;

public sealed record MedabotsRomTextProfile(
    string Id,
    string Name,
    string HeaderSignature,
    MedabotsRomAddresses Addresses)
{
    public int TextPointerTableOffset => Addresses.TextPointerTableOffset;

    public int TextDumpOffset => Addresses.TextDumpOffset;

    public int StarterOffset => Addresses.StarterOffset;

    public int BattlePointerTableOffset => Addresses.BattlePointerTableOffset;

    public int BattleCount => Addresses.BattleCount;

    public int EventTableOffset => Addresses.EventTableOffset;

    public int EventCount => Addresses.EventCount;
}
