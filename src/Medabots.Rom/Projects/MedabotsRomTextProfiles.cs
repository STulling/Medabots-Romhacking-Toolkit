using System.Text;

namespace Medabots.Rom.Metadata;

public static class MedabotsRomTextProfiles
{
    private static readonly IReadOnlyList<MedabotsRomTextProfile> Profiles =
    [
        new("MEDABOTSRKSVA9BPE9", "Medabots Rokusho Version (E)", "MEDABOTSRKSVA9BPE9", new MedabotsRomAddresses(0x47DF44, 0x7F5500, 0x7852F4, 0x3C1BA0, 0xF6, 0x769EE4, 0x8F0)),
        new("MEDABOTSRKSVA9BEE9", "Medabots Rokusho Version (U)", "MEDABOTSRKSVA9BEE9", new MedabotsRomAddresses(0x47E45C, 0x7F5500, 0x7840C0, 0x3C1A00, 0xF6, 0x769EE4, 0x8F0)),
        new("MEDABOTSMTBVA8BEE9", "Medabots Metabee Version (U)", "MEDABOTSMTBVA8BEE9", new MedabotsRomAddresses(0x47E43C, 0x7F5500, 0x78409B, 0x3C19E0, 0xF6, 0x769EE4, 0x8F0)),
        new("MEDABOTSMTBVA8BPE9", "Medabots Metabee Version (E)", "MEDABOTSMTBVA8BPE9", new MedabotsRomAddresses(0x47DF24, 0x7F5500, 0x7852CF, 0x3C1B80, 0xF6, 0x769EE4, 0x8F0))
    ];

    public static IReadOnlyList<MedabotsRomTextProfile> All => Profiles;

    public static MedabotsRomTextProfile? FindById(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return Profiles.FirstOrDefault(profile => string.Equals(profile.Id, id, StringComparison.Ordinal));
    }

    public static MedabotsRomTextProfile? Detect(RomFile romFile)
    {
        ArgumentNullException.ThrowIfNull(romFile);
        return Detect(romFile.Data);
    }

    public static MedabotsRomTextProfile? Detect(ReadOnlySpan<byte> romData)
    {
        var signature = ReadHeaderSignature(romData);
        return Profiles.FirstOrDefault(profile => string.Equals(profile.HeaderSignature, signature, StringComparison.Ordinal));
    }

    public static string ReadHeaderSignature(ReadOnlySpan<byte> romData)
    {
        if (romData.Length < MedabotsRomSchema.HeaderOffset + MedabotsRomSchema.HeaderLength)
        {
            throw new ArgumentException("The ROM data is too small to contain a valid GBA header.", nameof(romData));
        }

        return Encoding.ASCII.GetString(romData.Slice(MedabotsRomSchema.HeaderOffset, MedabotsRomSchema.HeaderLength)).TrimEnd('\0', ' ');
    }
}
