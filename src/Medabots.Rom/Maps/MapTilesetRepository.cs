using System.Buffers.Binary;
using Medabots.Rom.Compression;
using Medabots.Rom.Images;
using Medabots.Rom.Metadata;

namespace Medabots.Rom.Maps;

public sealed class MapTilesetRepository
{
    public MapTilesetAsset ReadMap(RomFile romFile, int mapId, string? mapName = null)
    {
        ArgumentNullException.ThrowIfNull(romFile);
        ArgumentOutOfRangeException.ThrowIfNegative(mapId);
        if (mapId >= MedabotsRomSchema.MapCount)
        {
            throw new ArgumentOutOfRangeException(nameof(mapId));
        }

        var widthInTiles = romFile.Data[MedabotsRomSchema.MapDimensionsInMetaTilesTableOffset + (mapId * 2)];
        var heightInTiles = romFile.Data[MedabotsRomSchema.MapDimensionsInMetaTilesTableOffset + (mapId * 2) + 1];

        var graphicsPointerOffset = MedabotsRomSchema.MapTilesetGraphicsPointerTableOffset + (mapId * sizeof(uint));
        var graphicsDataOffset = ReadPointerOffset(romFile, graphicsPointerOffset);
        var tilesetPixelIndices = ReadTilesetPixelIndices(romFile, graphicsDataOffset);
        var tileCount = tilesetPixelIndices.Length / 64;

        var palettePointerOffset = MedabotsRomSchema.MapTilesetPalettePointerTableOffset + (mapId * sizeof(uint));
        var paletteDataOffset = ReadPointerOffset(romFile, palettePointerOffset);
        var paletteBytes = paletteDataOffset >= 0
            ? romFile.ReadBytes(paletteDataOffset, MedabotsRomSchema.MapPaletteSize).ToArray()
            : new byte[MedabotsRomSchema.MapPaletteSize];

        var colorAttributePointerOffset = MedabotsRomSchema.MapColorAttributePointerTableOffset + (mapId * sizeof(uint));
        var colorAttributeDataOffset = ReadPointerOffset(romFile, colorAttributePointerOffset);
        var colorAttributeBytes = ReadCompressedBytes(romFile, colorAttributeDataOffset);

        var layers = new List<MapLayerAsset>(MedabotsRomSchema.MapLayerCount);
        var tilePaletteBanks = Enumerable.Repeat(-1, tileCount).ToArray();
        for (var layerIndex = 0; layerIndex < MedabotsRomSchema.MapLayerCount; layerIndex++)
        {
            var layerPointerOffset = MedabotsRomSchema.MapLayerTilemapPointerTableOffset + ((mapId * MedabotsRomSchema.MapLayerCount + layerIndex) * sizeof(uint));
            var layerDataOffset = ReadPointerOffset(romFile, layerPointerOffset);
            layers.Add(ReadLayer(romFile, layerIndex, layerPointerOffset, layerDataOffset, widthInTiles, heightInTiles, tilesetPixelIndices, paletteBytes, tilePaletteBanks));
        }

        var tilesetSheet = BuildTilesetSheet(tilesetPixelIndices, paletteBytes, tilePaletteBanks);
        return new MapTilesetAsset(
            mapId,
            string.IsNullOrWhiteSpace(mapName) ? $"Map {mapId:D2}" : mapName,
            widthInTiles,
            heightInTiles,
            graphicsPointerOffset,
            graphicsDataOffset,
            palettePointerOffset,
            paletteDataOffset,
            colorAttributePointerOffset,
            colorAttributeDataOffset,
            paletteBytes,
            colorAttributeBytes,
            tilesetSheet,
            layers);
    }

    private static MapLayerAsset ReadLayer(
        RomFile romFile,
        int layerIndex,
        int pointerOffset,
        int dataOffset,
        int widthInTiles,
        int heightInTiles,
        byte[] tilesetPixelIndices,
        byte[] paletteBytes,
        int[] tilePaletteBanks)
    {
        if (dataOffset < 0)
        {
            return new MapLayerAsset(layerIndex, pointerOffset, dataOffset, (ushort)widthInTiles, (ushort)heightInTiles, 0, 0, 0, 0, new ushort[widthInTiles * heightInTiles], new IndexedImage(widthInTiles, heightInTiles, new byte[widthInTiles * heightInTiles * 64], paletteBytes));
        }

        var decompressed = ReadCompressedBytes(romFile, dataOffset) ?? throw new InvalidOperationException($"Map layer {layerIndex} at 0x{dataOffset:X} is not valid LZ77 data.");
        if (decompressed.Length < MedabotsRomSchema.MapTilemapHeaderSize)
        {
            throw new InvalidOperationException($"Map layer {layerIndex} at 0x{dataOffset:X} is too short.");
        }

        var header = decompressed.AsSpan(0, MedabotsRomSchema.MapTilemapHeaderSize);
        var headerWidthInTiles = BinaryPrimitives.ReadUInt16LittleEndian(header[..2]);
        var headerHeightInTiles = BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(4, 2));
        var effectiveWidthInTiles = headerWidthInTiles == 0 ? widthInTiles : headerWidthInTiles;
        var effectiveHeightInTiles = headerHeightInTiles == 0 ? heightInTiles : headerHeightInTiles;
        var tileEntries = new ushort[effectiveWidthInTiles * effectiveHeightInTiles];
        var availableEntryCount = Math.Min(tileEntries.Length, (decompressed.Length - MedabotsRomSchema.MapTilemapHeaderSize) / 2);

        for (var index = 0; index < availableEntryCount; index++)
        {
            var entry = BinaryPrimitives.ReadUInt16LittleEndian(decompressed.AsSpan(MedabotsRomSchema.MapTilemapHeaderSize + (index * 2), 2));
            tileEntries[index] = entry;
            var tileIndex = entry & 0x03FF;
            if ((uint)tileIndex < (uint)tilePaletteBanks.Length && tilePaletteBanks[tileIndex] < 0)
            {
                tilePaletteBanks[tileIndex] = (entry >> 12) & 0xF;
            }
        }

        var image = RenderLayerImage(effectiveWidthInTiles, effectiveHeightInTiles, tileEntries, tilesetPixelIndices, paletteBytes);
        return new MapLayerAsset(
            layerIndex,
            pointerOffset,
            dataOffset,
            headerWidthInTiles,
            headerHeightInTiles,
            BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(2, 2)),
            BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(6, 2)),
            BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(8, 2)),
            BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(10, 2)),
            tileEntries,
            image);
    }

    private static IndexedImage BuildTilesetSheet(byte[] tilesetPixelIndices, byte[] paletteBytes, IReadOnlyList<int> tilePaletteBanks)
    {
        var tileCount = tilesetPixelIndices.Length / 64;
        var previewPixels = new byte[tilesetPixelIndices.Length];
        for (var tileIndex = 0; tileIndex < tileCount; tileIndex++)
        {
            var paletteBank = tileIndex < tilePaletteBanks.Count && tilePaletteBanks[tileIndex] >= 0
                ? tilePaletteBanks[tileIndex]
                : 0;

            var tileStart = tileIndex * 64;
            for (var pixelIndex = 0; pixelIndex < 64; pixelIndex++)
            {
                previewPixels[tileStart + pixelIndex] = (byte)(tilesetPixelIndices[tileStart + pixelIndex] + (paletteBank * 16));
            }
        }

        var tileWidth = MedabotsRomSchema.MapTilesetSheetTileWidth;
        var tileHeight = Math.Max(1, (int)Math.Ceiling(tileCount / (double)tileWidth));
        return new IndexedImage(tileWidth, tileHeight, previewPixels, paletteBytes);
    }

    private static IndexedImage RenderLayerImage(int widthInTiles, int heightInTiles, ushort[] tileEntries, byte[] tilesetPixelIndices, byte[] paletteBytes)
    {
        var tileCount = tilesetPixelIndices.Length / 64;
        var pixelIndices = new byte[widthInTiles * heightInTiles * 64];

        for (var tileY = 0; tileY < heightInTiles; tileY++)
        {
            for (var tileX = 0; tileX < widthInTiles; tileX++)
            {
                var entry = tileEntries[(tileY * widthInTiles) + tileX];
                var tileIndex = entry & 0x03FF;
                if ((uint)tileIndex >= (uint)tileCount)
                {
                    continue;
                }

                var hFlip = (entry & 0x0400) != 0;
                var vFlip = (entry & 0x0800) != 0;
                var paletteBank = (entry >> 12) & 0xF;
                var sourceTileStart = tileIndex * 64;
                var destinationTileStart = (tileY * widthInTiles + tileX) * 64;

                for (var localY = 0; localY < 8; localY++)
                {
                    for (var localX = 0; localX < 8; localX++)
                    {
                        var sourceX = hFlip ? 7 - localX : localX;
                        var sourceY = vFlip ? 7 - localY : localY;
                        var sourcePixel = tilesetPixelIndices[sourceTileStart + (sourceY * 8) + sourceX];
                        pixelIndices[destinationTileStart + (localY * 8) + localX] = (byte)(sourcePixel + (paletteBank * 16));
                    }
                }
            }
        }

        return new IndexedImage(widthInTiles, heightInTiles, pixelIndices, paletteBytes);
    }

    private static byte[] ReadTilesetPixelIndices(RomFile romFile, int dataOffset)
    {
        if (dataOffset < 0)
        {
            return [];
        }

        var decompressed = ReadCompressedBytes(romFile, dataOffset) ?? throw new InvalidOperationException($"Map tileset graphics at 0x{dataOffset:X} are not valid LZ77 data.");
        return TileImageCodec.Split4BppTiles(decompressed);
    }

    private static byte[]? ReadCompressedBytes(RomFile romFile, int dataOffset)
    {
        return dataOffset >= 0 ? GbaLz77.Decompress(romFile.Data, dataOffset) : null;
    }

    private static int ReadPointerOffset(RomFile romFile, int pointerOffset)
    {
        return GbaPointer.TryReadFileOffset(romFile.Data, pointerOffset, out var dataOffset)
            ? dataOffset
            : -1;
    }
}
