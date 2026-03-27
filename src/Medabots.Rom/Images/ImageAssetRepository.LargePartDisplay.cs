using Medabots.Rom.Compression;
using Medabots.Rom.Parts;

namespace Medabots.Rom.Images;

public sealed partial class ImageAssetRepository
{
    public LargePartDisplayAsset ReadLargePartDisplay(RomFile romFile, PartDefinition part, int? variantSelector = null)
    {
        ArgumentNullException.ThrowIfNull(romFile);
        ArgumentNullException.ThrowIfNull(part);

        var partOrdinal = part.Id / 4;
        var appearanceTableOffset = GetCompositePreviewAppearanceTableOffset(part.Kind);
        var appearanceEntrySize = part.Kind == PartKind.Legs ? 0x20 : 0x10;
        var appearanceEntryOffset = appearanceTableOffset + (partOrdinal * appearanceEntrySize);
        if (appearanceEntryOffset < 0 || appearanceEntryOffset + appearanceEntrySize > romFile.Length)
        {
            throw new InvalidDataException($"Large part display appearance row for part {part.Id} is out of range.");
        }

        var appearanceEntries = new uint[appearanceEntrySize / sizeof(uint)];
        for (var index = 0; index < appearanceEntries.Length; index++)
        {
            appearanceEntries[index] = BitConverter.ToUInt32(romFile.Data, appearanceEntryOffset + (index * sizeof(uint)));
        }

        var rootEntries = appearanceEntries.TakeWhile(entry => (entry & 0x3F) != 0).ToArray();
        var rootDescriptorId = (int)(rootEntries.FirstOrDefault() & 0x3F);
        if (rootDescriptorId == 0)
        {
            throw new InvalidDataException($"Large part display root descriptor for part {part.Id} is empty.");
        }

        var resolvedVariantSelector = GetLargeDisplayVariantSelector(part.Kind, variantSelector);
        var mutablePieces = new List<MutableLargePartDisplayPiece>();
        var visited = new HashSet<(int DescriptorId, int TableIndex)>();
        foreach (var rootEntry in rootEntries)
        {
            var descriptorId = (int)(rootEntry & 0x3F);
            var tableIndex = ResolveCompositePreviewTableIndex(rootEntry, resolvedVariantSelector);
            ReadLargePartDisplayDescriptorRecursive(romFile, appearanceEntries, descriptorId, tableIndex, resolvedVariantSelector, mutablePieces, visited);
        }

        ApplyLargeDisplayArmOverlayPass(romFile, part.Kind, partOrdinal, mutablePieces);

        var rootRecordOffset = ReadRequiredPointer(romFile, CompositePreviewDescriptorPointerTableOffset + (rootDescriptorId * sizeof(uint)));
        var initialPaletteBanks = BuildInitialLargeDisplayPaletteBanks(romFile);
        return new LargePartDisplayAsset(
            part.Id,
            partOrdinal,
            part.Kind,
            resolvedVariantSelector,
            rootDescriptorId,
            rootRecordOffset,
            initialPaletteBanks,
            mutablePieces.Select(piece => piece.ToAsset()).ToArray());
    }

    private static int GetCompositePreviewAppearanceTableOffset(PartKind kind) => kind switch
    {
        PartKind.Head => CompositePreviewHeadAppearanceTableOffset,
        PartKind.RightArm => CompositePreviewRightArmAppearanceTableOffset,
        PartKind.LeftArm => CompositePreviewLeftArmAppearanceTableOffset,
        PartKind.Legs => CompositePreviewLegsAppearanceTableOffset,
        _ => throw new InvalidOperationException($"Unsupported part kind '{kind}'.")
    };

    private void ReadLargePartDisplayDescriptorRecursive(
        RomFile romFile,
        IReadOnlyList<uint> appearanceEntries,
        int descriptorId,
        int tableIndex,
        int variantSelector,
        ICollection<MutableLargePartDisplayPiece> pieces,
        ISet<(int DescriptorId, int TableIndex)> visited)
    {
        if (descriptorId <= 0 || !visited.Add((descriptorId, tableIndex)))
        {
            return;
        }

        var descriptorPointerOffset = CompositePreviewDescriptorPointerTableOffset + (descriptorId * sizeof(uint));
        var descriptorOffset = ReadRequiredPointer(romFile, descriptorPointerOffset);
        if (descriptorOffset + 0x18 > romFile.Length)
        {
            throw new InvalidDataException($"Large part display descriptor {descriptorId} is out of range.");
        }

        var blobPointerTableOffset = ReadRequiredPointer(romFile, descriptorOffset);
        var imagePointerOffset = blobPointerTableOffset + (tableIndex * sizeof(uint));
        var imageOffset = ReadRequiredPointer(romFile, imagePointerOffset);
        var decoded = GbaLz77.Decompress(romFile.Data, imageOffset)
            ?? throw new InvalidDataException($"Large part display descriptor {descriptorId} does not contain valid LZ77 image data.");
        var unpacked = TileImageCodec.Split4BppTiles(decoded);
        var paletteOffset = TryReadOptionalPointer(romFile, imagePointerOffset + sizeof(uint), out var resolvedPaletteOffset)
            ? resolvedPaletteOffset
            : 0;
        var palettePointerOffset = imagePointerOffset + sizeof(uint);
        if (paletteOffset == imageOffset)
        {
            paletteOffset = 0;
            palettePointerOffset = 0;
        }

        var paletteBytes = paletteOffset == 0 ? [] : romFile.ReadBytes(paletteOffset, PaletteSize).ToArray();
        var width = romFile.Data[descriptorOffset + 0x12];
        var height = romFile.Data[descriptorOffset + 0x13];
        var divisors = romFile.Data[descriptorOffset + 0x14];
        var widthDivisor = Math.Max(1, divisors & 0x0F);
        var heightDivisor = Math.Max(1, divisors >> 4);
        width /= (byte)widthDivisor;
        height /= (byte)heightDivisor;

        var tileWidth = Math.Max(1, width / 8);
        var tileHeight = Math.Max(1, height / 8);
        var x = -BitConverter.ToInt32(romFile.Data, descriptorOffset + 0x04);
        var y = -BitConverter.ToInt32(romFile.Data, descriptorOffset + 0x08);
        var paletteBank = romFile.Data[descriptorOffset + 0x11];
        var loadedTileCount = Math.Max(1, unpacked.Length / 64);
        var allocatedPixels = new byte[Math.Max(tileWidth * tileHeight * 64, unpacked.Length)];
        Array.Copy(unpacked, allocatedPixels, Math.Min(unpacked.Length, allocatedPixels.Length));

        pieces.Add(new MutableLargePartDisplayPiece(
            descriptorId,
            descriptorOffset,
            imagePointerOffset,
            palettePointerOffset,
            imageOffset,
            paletteOffset,
            paletteBytes,
            paletteBank,
            x,
            y,
            loadedTileCount,
            tileWidth,
            tileHeight,
            allocatedPixels));

        var childDescriptorId = romFile.Data[descriptorOffset + 0x10];
        if (childDescriptorId != 0)
        {
            ReadLargePartDisplayDescriptorRecursive(romFile, appearanceEntries, childDescriptorId, ResolveCompositePreviewTableIndex(appearanceEntries, childDescriptorId, variantSelector), variantSelector, pieces, visited);
        }

        var siblingDescriptorId = romFile.Data[descriptorOffset + 0x0F];
        if (siblingDescriptorId != 0)
        {
            ReadLargePartDisplayDescriptorRecursive(romFile, appearanceEntries, siblingDescriptorId, ResolveCompositePreviewTableIndex(appearanceEntries, siblingDescriptorId, variantSelector), variantSelector, pieces, visited);
        }
    }

    private static int ResolveCompositePreviewTableIndex(IReadOnlyList<uint> appearanceEntries, int descriptorId, int variantSelector)
    {
        foreach (var entry in appearanceEntries)
        {
            var entryDescriptorId = (int)(entry & 0x3F);
            if (entryDescriptorId == 0)
            {
                break;
            }

            if (entryDescriptorId == descriptorId)
            {
                return ResolveCompositePreviewTableIndex(entry, variantSelector);
            }
        }

        return 0;
    }

    private static int ResolveCompositePreviewTableIndex(uint entry, int variantSelector)
    {
        var baseSelector = (int)((entry >> 6) & 0xFF);
        var variantBit = (int)((entry >> 15) & 0x1);
        return (baseSelector + (variantSelector & variantBit)) << 1;
    }

    private void ApplyLargeDisplayArmOverlayPass(RomFile romFile, PartKind kind, int partOrdinal, IList<MutableLargePartDisplayPiece> pieces)
    {
        if (kind != PartKind.LeftArm)
        {
            return;
        }

        var currentRowOffset = CompositePreviewLeftArmAppearanceTableOffset + (partOrdinal * 0x10);
        var companionRowOffset = CompositePreviewRightArmAppearanceTableOffset + (partOrdinal * 0x10);
        for (var offset = 0; offset < 0x10; offset += sizeof(uint))
        {
            var currentEntry = BitConverter.ToUInt32(romFile.Data, currentRowOffset + offset);
            var currentDescriptorId = (int)(currentEntry & 0x3F);
            if (currentDescriptorId == 0)
            {
                break;
            }

            var currentEntryByte1 = unchecked((sbyte)romFile.Data[currentRowOffset + offset + 1]);
            if (currentEntryByte1 < 0)
            {
                continue;
            }

            var companionEntry = BitConverter.ToUInt32(romFile.Data, companionRowOffset + offset);
            var companionDescriptorId = (int)(companionEntry & 0x3F);
            if (companionDescriptorId == 0)
            {
                continue;
            }

            var companionSourceEntry = FindLargeDisplayAppearanceEntry(romFile, companionRowOffset, companionDescriptorId);
            if (companionSourceEntry == 0)
            {
                continue;
            }

            var selector = (int)((companionSourceEntry >> 6) & 0xFF);
            var companionDescriptorOffset = ReadRequiredPointer(romFile, CompositePreviewDescriptorPointerTableOffset + (companionDescriptorId * sizeof(uint)));
            var blobPointerTableOffset = ReadRequiredPointer(romFile, companionDescriptorOffset);
            var imageOffset = ReadRequiredPointer(romFile, blobPointerTableOffset + (selector * sizeof(ulong)));
            var decoded = GbaLz77.Decompress(romFile.Data, imageOffset);
            if (decoded is null)
            {
                continue;
            }

            var unpacked = TileImageCodec.Split4BppTiles(decoded);
            var targetPiece = pieces.FirstOrDefault(piece => piece.DescriptorId == currentDescriptorId);
            if (targetPiece is null)
            {
                continue;
            }

            Array.Copy(unpacked, targetPiece.PixelIndices, Math.Min(unpacked.Length, targetPiece.PixelIndices.Length));
        }
    }

    private static uint FindLargeDisplayAppearanceEntry(RomFile romFile, int rowOffset, int descriptorId)
    {
        for (var offset = 0; offset < 0x10; offset += sizeof(uint))
        {
            var entry = BitConverter.ToUInt32(romFile.Data, rowOffset + offset);
            var entryDescriptorId = (int)(entry & 0x3F);
            if (entryDescriptorId == 0)
            {
                break;
            }

            if (entryDescriptorId == descriptorId)
            {
                return entry;
            }
        }

        return 0;
    }

    private static IReadOnlyDictionary<int, byte[]> BuildInitialLargeDisplayPaletteBanks(RomFile romFile) =>
        new Dictionary<int, byte[]>
        {
            [8] = romFile.ReadBytes(PartDetailObjPaletteBlockBOffset, PaletteSize).ToArray(),
            [9] = romFile.ReadBytes(PartDetailObjPaletteBlockAOffset, PaletteSize).ToArray(),
            [10] = romFile.ReadBytes(PartDetailObjPaletteBlockCOffset, PaletteSize).ToArray()
        };

    private static int GetLargeDisplayVariantSelector(PartKind kind, int? requestedVariantSelector)
    {
        if (kind is not PartKind.RightArm and not PartKind.LeftArm)
        {
            return 0;
        }

        return requestedVariantSelector.HasValue ? requestedVariantSelector.Value & 1 : (kind == PartKind.LeftArm ? 1 : 0);
    }

    private sealed class MutableLargePartDisplayPiece
    {
        public MutableLargePartDisplayPiece(int descriptorId, int recordOffset, int imagePointerOffset, int palettePointerOffset, int imageOffset, int paletteOffset, byte[] paletteBytes, int paletteBank, int x, int y, int loadedTileCount, int tileWidth, int tileHeight, byte[] pixelIndices)
        {
            DescriptorId = descriptorId;
            RecordOffset = recordOffset;
            ImagePointerOffset = imagePointerOffset;
            PalettePointerOffset = palettePointerOffset;
            ImageOffset = imageOffset;
            PaletteOffset = paletteOffset;
            PaletteBytes = paletteBytes;
            PaletteBank = paletteBank;
            X = x;
            Y = y;
            LoadedTileCount = loadedTileCount;
            TileWidth = tileWidth;
            TileHeight = tileHeight;
            PixelIndices = pixelIndices;
        }

        public int DescriptorId { get; }
        public int RecordOffset { get; }
        public int ImagePointerOffset { get; }
        public int PalettePointerOffset { get; }
        public int ImageOffset { get; }
        public int PaletteOffset { get; }
        public byte[] PaletteBytes { get; }
        public int PaletteBank { get; }
        public int X { get; }
        public int Y { get; }
        public int LoadedTileCount { get; }
        public int TileWidth { get; }
        public int TileHeight { get; }
        public byte[] PixelIndices { get; }

        public LargePartDisplayPieceAsset ToAsset()
        {
            var palette = PaletteBytes.Length == 0 ? new byte[PaletteSize] : PaletteBytes;
            return new LargePartDisplayPieceAsset(
                DescriptorId,
                RecordOffset,
                ImagePointerOffset,
                PalettePointerOffset,
                ImageOffset,
                PaletteOffset,
                palette,
                PaletteBank,
                X,
                Y,
                LoadedTileCount,
                new IndexedImage(TileWidth, TileHeight, PixelIndices.ToArray(), palette));
        }
    }
}
