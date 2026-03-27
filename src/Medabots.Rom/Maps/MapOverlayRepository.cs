using Medabots.Rom.Metadata;

namespace Medabots.Rom.Maps;

public sealed class MapOverlayRepository
{
    public MapOverlayAsset ReadMap(RomFile romFile, int mapId)
    {
        ArgumentNullException.ThrowIfNull(romFile);
        ArgumentOutOfRangeException.ThrowIfNegative(mapId);
        if (mapId >= MedabotsRomSchema.MapCount)
        {
            throw new ArgumentOutOfRangeException(nameof(mapId));
        }

        var warpPointerOffset = MedabotsRomSchema.MapWarpPointerTableOffset + (mapId * sizeof(uint));
        var warpDataOffset = ReadPointerOffset(romFile, warpPointerOffset);
        var entityPointerOffset = MedabotsRomSchema.MapEntitySpawnPointerTableOffset + (mapId * sizeof(uint));
        var entityDataOffset = ReadPointerOffset(romFile, entityPointerOffset);

        return new MapOverlayAsset(
            mapId,
            warpPointerOffset,
            warpDataOffset,
            entityPointerOffset,
            entityDataOffset,
            ReadWarps(romFile, warpDataOffset),
            ReadEntitySpawns(romFile, entityDataOffset));
    }

    private static IReadOnlyList<MapWarpRecord> ReadWarps(RomFile romFile, int dataOffset)
    {
        if (dataOffset < 0)
        {
            return [];
        }

        var records = new List<MapWarpRecord>();
        var offset = dataOffset;
        while (offset + 8 <= romFile.Data.Length)
        {
            var tileX = romFile.Data[offset];
            if (tileX == 0xFF)
            {
                break;
            }

            records.Add(new MapWarpRecord(
                tileX,
                romFile.Data[offset + 1],
                romFile.Data[offset + 2],
                romFile.Data[offset + 3],
                romFile.Data[offset + 4],
                romFile.Data[offset + 5],
                romFile.Data[offset + 6],
                romFile.Data[offset + 7]));
            offset += 8;
        }

        return records;
    }

    private static IReadOnlyList<MapEntitySpawnRecord> ReadEntitySpawns(RomFile romFile, int dataOffset)
    {
        if (dataOffset < 0)
        {
            return [];
        }

        var records = new List<MapEntitySpawnRecord>();
        var offset = dataOffset;
        while (offset + 8 <= romFile.Data.Length)
        {
            var tileX = romFile.Data[offset];
            if (tileX == 0xFF)
            {
                break;
            }

            records.Add(new MapEntitySpawnRecord(
                tileX,
                romFile.Data[offset + 1],
                BitConverter.ToUInt16(romFile.Data, offset + 2),
                romFile.Data[offset + 4],
                romFile.Data[offset + 5],
                BitConverter.ToUInt16(romFile.Data, offset + 6)));
            offset += 8;
        }

        return records;
    }

    private static int ReadPointerOffset(RomFile romFile, int pointerOffset)
    {
        return GbaPointer.TryReadFileOffset(romFile.Data, pointerOffset, out var dataOffset)
            ? dataOffset
            : -1;
    }
}
