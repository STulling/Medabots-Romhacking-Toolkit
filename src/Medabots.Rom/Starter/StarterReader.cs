using Medabots.Rom.Metadata;

namespace Medabots.Rom.Starter;

public sealed class StarterReader
{
    public StarterDefinition Read(RomFile romFile, MedabotsRomTextProfile profile)
    {
        ArgumentNullException.ThrowIfNull(romFile);
        ArgumentNullException.ThrowIfNull(profile);

        var partId = romFile.Data[profile.StarterOffset];
        var isFemale = romFile.Data[profile.StarterOffset + 16] != 0;
        var medalOffset = FindMedalOffset(romFile);
        var medalId = romFile.Data[medalOffset];
        return new StarterDefinition(profile.StarterOffset, medalOffset, partId, medalId, isFemale);
    }

    public int FindMedalOffset(RomFile romFile)
    {
        ArgumentNullException.ThrowIfNull(romFile);
        return MedabotsRomSchema.StarterMedalSlot.Locate(romFile.Data);
    }
}
