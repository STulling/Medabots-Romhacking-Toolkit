using System.Buffers.Binary;
using Medabots.Rom.Compression;
using Medabots.Rom.Metadata;

namespace Medabots.Rom.Maps;

public sealed class MapLayerPatcher
{
    private readonly Dictionary<(int MapId, int LayerIndex), (int Offset, int Length)> _allocations = [];

    public void RewriteLayer(RomHackSession session, MapLayerPatch patch, string description)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(patch);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        var rawBytes = SerializeLayer(patch);
        var payload = GbaLz77.Compress(rawBytes);
        var destination = ReserveSpace(session.RomFile, patch.MapId, patch.LayerIndex, payload.Length);
        session.ApplyPatch(RomPatchAction.Create(destination, payload, description));

        Span<byte> pointerBytes = stackalloc byte[sizeof(uint)];
        GbaPointer.WriteFileOffset(pointerBytes, 0, destination);
        var pointerOffset = MedabotsRomSchema.MapLayerTilemapPointerTableOffset + ((patch.MapId * MedabotsRomSchema.MapLayerCount + patch.LayerIndex) * sizeof(uint));
        session.ApplyPatch(RomPatchAction.Create(pointerOffset, pointerBytes, $"Update map {patch.MapId} layer {patch.LayerIndex + 1} pointer"));
    }

    internal static byte[] SerializeLayer(MapLayerPatch patch)
    {
        var entryBytes = new byte[MedabotsRomSchema.MapTilemapHeaderSize + (patch.TileEntries.Length * 2)];
        BinaryPrimitives.WriteUInt16LittleEndian(entryBytes.AsSpan(0, 2), patch.HeaderWidthInTiles);
        BinaryPrimitives.WriteUInt16LittleEndian(entryBytes.AsSpan(2, 2), patch.HeaderOriginX);
        BinaryPrimitives.WriteUInt16LittleEndian(entryBytes.AsSpan(4, 2), patch.HeaderHeightInTiles);
        BinaryPrimitives.WriteUInt16LittleEndian(entryBytes.AsSpan(6, 2), patch.HeaderOriginY);
        BinaryPrimitives.WriteUInt16LittleEndian(entryBytes.AsSpan(8, 2), patch.HeaderOriginX2);
        BinaryPrimitives.WriteUInt16LittleEndian(entryBytes.AsSpan(10, 2), patch.HeaderOriginY2);
        for (var index = 0; index < patch.TileEntries.Length; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(entryBytes.AsSpan(MedabotsRomSchema.MapTilemapHeaderSize + (index * 2), 2), patch.TileEntries[index]);
        }

        return entryBytes;
    }

    private int ReserveSpace(RomFile romFile, int mapId, int layerIndex, int requiredLength)
    {
        if (_allocations.TryGetValue((mapId, layerIndex), out var allocation) && requiredLength <= allocation.Length)
        {
            return allocation.Offset;
        }

        var nextOffset = AlignUp(Math.Max(romFile.Length, 0x800000), 4);
        _allocations[(mapId, layerIndex)] = (nextOffset, requiredLength);
        return nextOffset;
    }

    private static int AlignUp(int value, int alignment)
    {
        var remainder = value % alignment;
        return remainder == 0 ? value : value + (alignment - remainder);
    }
}
