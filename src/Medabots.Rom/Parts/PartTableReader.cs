using Medabots.Rom.Metadata;

namespace Medabots.Rom.Parts;

public sealed class PartTableReader
{
    public const int PartSize = MedabotsRomSchema.PartSize;
    public const int PartCount = MedabotsRomSchema.PartCount;

    public IReadOnlyList<PartDefinition> ReadAll(RomFile romFile)
    {
        ArgumentNullException.ThrowIfNull(romFile);
        var tableOffset = FindTableOffset(romFile);
        return ReadAll(romFile, tableOffset);
    }

    public IReadOnlyList<PartDefinition> ReadAll(RomFile romFile, int tableOffset)
    {
        ArgumentNullException.ThrowIfNull(romFile);
        var parts = new List<PartDefinition>(PartCount);

        for (var partId = 0; partId < PartCount; partId++)
        {
            var dataOffset = tableOffset + (partId * PartSize);
            parts.Add(ReadSingle(romFile, partId, dataOffset));
        }

        return parts;
    }

    public PartDefinition ReadSingle(RomFile romFile, int partId, int dataOffset)
    {
        ArgumentNullException.ThrowIfNull(romFile);
        var data = romFile.ReadBytes(dataOffset, PartSize).Span;

        return new PartDefinition(
            partId,
            partId / 4,
            (PartKind)(partId % 4),
            dataOffset,
            data[0],
            data[1],
            data[2],
            data[3],
            data[4],
            data[5],
            data[6],
            data[7],
            data[8],
            data[9],
            data[10],
            data[11],
            data[12],
            data[13],
            data[14],
            data[15]);
    }

    public int FindTableOffset(RomFile romFile)
    {
        ArgumentNullException.ThrowIfNull(romFile);
        return MedabotsRomSchema.PartTable.Locate(romFile.Data);
    }

    public static byte[] Serialize(PartDefinition part)
    {
        ArgumentNullException.ThrowIfNull(part);
        return
        [
            part.MedalCompatibility,
            part.TechniqueOrLegType,
            part.Speciality,
            part.Gender,
            part.Armor,
            part.RateOfSuccessOrPropulsion,
            part.PowerOrEvasion,
            part.ChainReactionOrDefense,
            part.AmountOfUsesOrProximity,
            part.Unknown2,
            part.Unknown3,
            part.Unknown4,
            part.Unknown5,
            part.Unknown6,
            part.Unknown7,
            part.Unknown8
        ];
    }
}
