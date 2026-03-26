using Medabots.Rom.Metadata;

namespace Medabots.Rom.Encounters;

public sealed class EncounterTableReader
{
    public const int EncounterCount = MedabotsRomSchema.EncounterCount;
    public const int EncounterSize = MedabotsRomSchema.EncounterSize;

    public int FindTableOffset(RomFile romFile)
    {
        ArgumentNullException.ThrowIfNull(romFile);
        return MedabotsRomSchema.EncounterTable.Locate(romFile.Data);
    }

    public IReadOnlyList<EncounterDefinition> ReadAll(RomFile romFile)
    {
        var tableOffset = FindTableOffset(romFile);
        var result = new List<EncounterDefinition>(EncounterCount);
        for (var i = 0; i < EncounterCount; i++)
        {
            var dataOffset = tableOffset + (i * EncounterSize);
            var data = romFile.ReadBytes(dataOffset, EncounterSize).Span;
            result.Add(new EncounterDefinition(i, dataOffset, data[0], data[1], data[2], data[3]));
        }

        return result;
    }

    public static byte[] Serialize(EncounterDefinition encounter) => [encounter.Battle1, encounter.Battle2, encounter.Battle3, encounter.Battle4];
}
