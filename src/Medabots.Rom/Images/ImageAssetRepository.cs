using Medabots.Rom.Metadata;
using Medabots.Rom.Compression;
using Medabots.Rom.Parts;

namespace Medabots.Rom.Images;

public sealed class ImageAssetRepository
{
    public const int PortraitPointerTableOffset = MedabotsRomSchema.PortraitPointerTableOffset;
    public const int PortraitPaletteTableOffset = MedabotsRomSchema.PortraitPaletteTableOffset;
    public const int PortraitsPerCharacter = MedabotsRomSchema.PortraitsPerCharacter;
    public const int CompositeBattleSpritePointerTableOffset = MedabotsRomSchema.CompositeBattleSpritePointerTableOffset;
    public const int CompositeBattleSpritePointersPerPart = MedabotsRomSchema.CompositeBattleSpritePointersPerPart;
    public const int CompositeBattleSpritePartCount = MedabotsRomSchema.CompositeBattleSpritePartCount;
    public const int CompositeBattleSpritePaletteFamilyTableOffset = MedabotsRomSchema.CompositeBattleSpritePaletteFamilyTableOffset;
    public const int CompositeBattleSpritePaletteDataOffset = MedabotsRomSchema.CompositeBattleSpritePaletteDataOffset;
    public const int CompositeBattleSpritePaletteCount = MedabotsRomSchema.CompositeBattleSpritePaletteCount;
    public const int PartSelectionComponentPaletteSetOffset = MedabotsRomSchema.PartSelectionComponentPaletteSetOffset;
    public const int CompositePreviewDescriptorPointerTableOffset = MedabotsRomSchema.CompositePreviewDescriptorPointerTableOffset;
    public const int CompositePreviewHeadAppearanceTableOffset = MedabotsRomSchema.CompositePreviewHeadAppearanceTableOffset;
    public const int CompositePreviewRightArmAppearanceTableOffset = MedabotsRomSchema.CompositePreviewRightArmAppearanceTableOffset;
    public const int CompositePreviewLeftArmAppearanceTableOffset = MedabotsRomSchema.CompositePreviewLeftArmAppearanceTableOffset;
    public const int CompositePreviewLegsAppearanceTableOffset = MedabotsRomSchema.CompositePreviewLegsAppearanceTableOffset;
    public const int PartDetailObjPaletteBlockAOffset = MedabotsRomSchema.PartDetailObjPaletteBlockAOffset;
    public const int PartDetailObjPaletteBlockBOffset = MedabotsRomSchema.PartDetailObjPaletteBlockBOffset;
    public const int PartDetailObjPaletteBlockCOffset = MedabotsRomSchema.PartDetailObjPaletteBlockCOffset;
    public const int SharedPartScreenTileSheetOffset = MedabotsRomSchema.SharedPartScreenTileSheetOffset;
    public const int SpritePointerTableOffset = MedabotsRomSchema.SpritePointerTableOffset;
    public const int SpritePaletteTableOffset = MedabotsRomSchema.SpritePaletteTableOffset;
    public const int PaletteSize = MedabotsRomSchema.PaletteSize;

    public PortraitAsset ReadPortrait(RomFile romFile, int characterId, int portraitIndex)
    {
        ArgumentNullException.ThrowIfNull(romFile);
        ArgumentOutOfRangeException.ThrowIfNegative(characterId);
        ArgumentOutOfRangeException.ThrowIfNegative(portraitIndex);

        var imagePointerOffset = PortraitPointerTableOffset + ((characterId * PortraitsPerCharacter) + portraitIndex) * sizeof(uint);
        var palettePointerOffset = PortraitPaletteTableOffset + (characterId * sizeof(uint));

        var imageOffset = ReadRequiredPointer(romFile, imagePointerOffset);
        var paletteOffset = ReadRequiredPointer(romFile, palettePointerOffset);

        var compressed = Malias2.Decompress(romFile.Data, imageOffset) ?? DecompressPortraitPayload(romFile.Data, imageOffset);
        var unpacked = TileImageCodec.Split4BppTiles(compressed);
        var palette = romFile.ReadBytes(paletteOffset, PaletteSize).ToArray();
        return new PortraitAsset(
            characterId,
            portraitIndex,
            imagePointerOffset,
            palettePointerOffset,
            imageOffset,
            paletteOffset,
            new IndexedImage(
                MedabotsRomSchema.PortraitTileWidth,
                unpacked.Length / 0x40 / MedabotsRomSchema.PortraitTileWidth,
                unpacked,
                palette));
    }

    public SpriteAsset ReadSprite(RomFile romFile, int spriteId)
    {
        ArgumentNullException.ThrowIfNull(romFile);
        ArgumentOutOfRangeException.ThrowIfNegative(spriteId);

        var imagePointerOffset = SpritePointerTableOffset + (spriteId * sizeof(uint));
        var palettePointerOffset = SpritePaletteTableOffset + (spriteId * sizeof(uint));
        var imageOffset = ReadRequiredPointer(romFile, imagePointerOffset);
        var paletteOffset = ReadRequiredPointer(romFile, palettePointerOffset);

        var compressed = GbaLz77.Decompress(romFile.Data, imageOffset)
            ?? throw new InvalidDataException($"Sprite {spriteId} does not contain valid LZ77 image data.");
        var unpacked = TileImageCodec.Split4BppTiles(compressed);
        var palette = romFile.ReadBytes(paletteOffset, PaletteSize).ToArray();
        return new SpriteAsset(
            spriteId,
            imagePointerOffset,
            palettePointerOffset,
            imageOffset,
            paletteOffset,
            new IndexedImage(2, unpacked.Length / 0x40, unpacked, palette));
    }

    public BattleCompositeSpriteComponentAsset ReadBattleCompositeSpriteComponent(RomFile romFile, int medabotId, int componentIndex)
    {
        ArgumentNullException.ThrowIfNull(romFile);
        ArgumentOutOfRangeException.ThrowIfNegative(medabotId);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(medabotId, CompositeBattleSpritePartCount);
        ArgumentOutOfRangeException.ThrowIfNegative(componentIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(componentIndex, CompositeBattleSpritePointersPerPart);

        var imagePointerOffset = CompositeBattleSpritePointerTableOffset +
                                 ((medabotId * CompositeBattleSpritePointersPerPart) + componentIndex) * sizeof(uint);
        var imageOffset = ReadRequiredPointer(romFile, imagePointerOffset);
        var compressed = Malias2.Decompress(romFile.Data, imageOffset)
            ?? throw new InvalidDataException($"Composite battle sprite component {medabotId}:{componentIndex} does not contain valid Malias2 image data.");
        var unpacked = TileImageCodec.Split4BppTiles(compressed);
        var tileCount = Math.Max(1, unpacked.Length / 0x40);
        var tileWidth = GuessCompositeComponentTileWidth(componentIndex, tileCount);
        var paletteFamily = ReadCompositePaletteFamily(romFile, medabotId);
        var paletteBank = (byte)(paletteFamily + 4);
        var paletteOffset = ResolvePartSelectionPaletteOffset(paletteFamily);
        var palette = romFile.ReadBytes(paletteOffset, PaletteSize).ToArray();

        return new BattleCompositeSpriteComponentAsset(
            medabotId,
            componentIndex,
            imagePointerOffset,
            imageOffset,
            CompositeBattleSpritePaletteFamilyTableOffset + medabotId,
            paletteOffset,
            paletteFamily,
            0,
            paletteBank,
            new IndexedImage(tileWidth, Math.Max(1, tileCount / tileWidth), unpacked, palette));
    }

    public byte[] ReadBattleCompositePaletteBytesForFamily(RomFile romFile, byte family)
    {
        ArgumentNullException.ThrowIfNull(romFile);

        var paletteOffset = ResolvePartSelectionPaletteOffset(family);
        return romFile.ReadBytes(paletteOffset, PaletteSize).ToArray();
    }

    public SharedSpriteSheetAsset ReadSharedPartSpriteSheet(RomFile romFile, string key)
    {
        ArgumentNullException.ThrowIfNull(romFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var (name, imageOffset, tileWidth) = key switch
        {
            "part-screen-main" => ("Shared Part Screen Tile Sheet", MedabotsRomSchema.SharedPartScreenTileSheetOffset, MedabotsRomSchema.SharedPartScreenTileSheetTileWidth),
            "part-detail-a" => ("Part Detail Tile Sheet A", MedabotsRomSchema.SharedPartDetailTileSheetAOffset, 8),
            "part-detail-b" => ("Part Detail Tile Sheet B", MedabotsRomSchema.SharedPartDetailTileSheetBOffset, 8),
            "part-detail-c" => ("Part Detail Tile Sheet C", MedabotsRomSchema.SharedPartDetailTileSheetCOffset, 4),
            "part-detail-d" => ("Part Detail Tile Sheet D", MedabotsRomSchema.SharedPartDetailTileSheetDOffset, 4),
            "part-detail-e" => ("Part Detail Tile Sheet E", MedabotsRomSchema.SharedPartDetailTileSheetEOffset, 8),
            "part-detail-f" => ("Part Detail Tile Sheet F", MedabotsRomSchema.SharedPartDetailTileSheetFOffset, 4),
            "part-detail-g" => ("Part Detail Tile Sheet G", MedabotsRomSchema.SharedPartDetailTileSheetGOffset, 4),
            _ => throw new InvalidOperationException($"Unsupported shared sprite sheet key '{key}'.")
        };

        var compressed = Malias2.Decompress(romFile.Data, imageOffset)
            ?? throw new InvalidDataException($"Shared sprite sheet '{key}' does not contain valid Malias2 image data.");
        var unpacked = TileImageCodec.Split4BppTiles(compressed);
        var tileCount = Math.Max(1, unpacked.Length / 0x40);
        tileWidth = Math.Min(tileWidth, tileCount);

        return new SharedSpriteSheetAsset(
            key,
            name,
            imageOffset,
            new IndexedImage(tileWidth, Math.Max(1, tileCount / tileWidth), unpacked, BuildPlaceholderCompositePalette()));
    }

    public LargePartDisplayAsset ReadLargePartDisplay(RomFile romFile, PartDefinition part)
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

        var rootEntries = appearanceEntries
            .TakeWhile(entry => (entry & 0x3F) != 0)
            .ToArray();

        var rootDescriptorId = (int)(rootEntries.FirstOrDefault() & 0x3F);
        if (rootDescriptorId == 0)
        {
            throw new InvalidDataException($"Large part display root descriptor for part {part.Id} is empty.");
        }

        var variantSelector = GetLargeDisplayVariantSelector(part.Kind);
        var mutablePieces = new List<MutableLargePartDisplayPiece>();
        var visited = new HashSet<(int DescriptorId, int TableIndex)>();
        foreach (var rootEntry in rootEntries)
        {
            var descriptorId = (int)(rootEntry & 0x3F);
            var tableIndex = ResolveCompositePreviewTableIndex(rootEntry, variantSelector);
            ReadLargePartDisplayDescriptorRecursive(romFile, appearanceEntries, descriptorId, tableIndex, variantSelector, mutablePieces, visited);
        }

        ApplyLargeDisplayArmOverlayPass(romFile, part.Kind, partOrdinal, mutablePieces);

        var rootRecordOffset = ReadRequiredPointer(romFile, CompositePreviewDescriptorPointerTableOffset + (rootDescriptorId * sizeof(uint)));
        var initialPaletteBanks = BuildInitialLargeDisplayPaletteBanks(romFile);
        return new LargePartDisplayAsset(
            part.Id,
            partOrdinal,
            part.Kind,
            rootDescriptorId,
            rootRecordOffset,
            initialPaletteBanks,
            mutablePieces.Select(piece => piece.ToAsset()).ToArray());
    }

    private static int ReadRequiredPointer(RomFile romFile, int pointerOffset)
    {
        if (!GbaPointer.TryReadFileOffset(romFile.Data, pointerOffset, out var fileOffset))
        {
            throw new InvalidDataException($"Invalid pointer at 0x{pointerOffset:X}.");
        }

        return fileOffset;
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
        if (paletteOffset == imageOffset)
        {
            paletteOffset = 0;
        }

        var paletteBytes = paletteOffset == 0
            ? []
            : romFile.ReadBytes(paletteOffset, PaletteSize).ToArray();

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
            ReadLargePartDisplayDescriptorRecursive(
                romFile,
                appearanceEntries,
                childDescriptorId,
                ResolveCompositePreviewTableIndex(appearanceEntries, childDescriptorId, variantSelector),
                variantSelector,
                pieces,
                visited);
        }

        var siblingDescriptorId = romFile.Data[descriptorOffset + 0x0F];
        if (siblingDescriptorId != 0)
        {
            ReadLargePartDisplayDescriptorRecursive(
                romFile,
                appearanceEntries,
                siblingDescriptorId,
                ResolveCompositePreviewTableIndex(appearanceEntries, siblingDescriptorId, variantSelector),
                variantSelector,
                pieces,
                visited);
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

    private void ApplyLargeDisplayArmOverlayPass(
        RomFile romFile,
        PartKind kind,
        int partOrdinal,
        IList<MutableLargePartDisplayPiece> pieces)
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

    private static IReadOnlyDictionary<int, byte[]> BuildInitialLargeDisplayPaletteBanks(RomFile romFile)
    {
        return new Dictionary<int, byte[]>
        {
            [8] = romFile.ReadBytes(PartDetailObjPaletteBlockBOffset, PaletteSize).ToArray(),
            [9] = romFile.ReadBytes(PartDetailObjPaletteBlockAOffset, PaletteSize).ToArray(),
            [10] = romFile.ReadBytes(PartDetailObjPaletteBlockCOffset, PaletteSize).ToArray()
        };
    }

    private static int GetLargeDisplayVariantSelector(PartKind kind)
    {
        return kind == PartKind.LeftArm ? 1 : 0;
    }

    private static int GuessCompositeComponentTileWidth(int componentIndex, int tileCount)
    {
        if (componentIndex == 5)
        {
            return tileCount >= 8 ? 2 : Math.Max(1, tileCount);
        }

        return tileCount >= 4 ? 2 : Math.Max(1, tileCount);
    }

    private static byte ReadCompositePaletteFamily(RomFile romFile, int medabotId)
    {
        var familyOffset = CompositeBattleSpritePaletteFamilyTableOffset + medabotId;
        if (familyOffset < 0 || familyOffset >= romFile.Length)
        {
            throw new InvalidDataException($"Composite battle sprite palette family table offset 0x{familyOffset:X} is out of range.");
        }

        var family = romFile.Data[familyOffset];
        if (CompositeBattleSpritePaletteDataOffset + ((family + 4) * PaletteSize) + PaletteSize > romFile.Length)
        {
            throw new InvalidDataException($"Composite battle sprite palette family {family} for medabot {medabotId} is out of range.");
        }

        return family;
    }

    private static int ResolvePartSelectionPaletteOffset(byte family)
    {
        var paletteOffset = PartSelectionComponentPaletteSetOffset + (family * PaletteSize);
        if (family >= 8)
        {
            return CompositeBattleSpritePaletteDataOffset + (family * PaletteSize);
        }

        return paletteOffset;
    }

    private static byte[] BuildPlaceholderCompositePalette()
    {
        var palette = new byte[PaletteSize];
        for (var index = 0; index < PaletteSize / 2; index++)
        {
            var shade = (byte)Math.Min(31, index * 2);
            var raw = (ushort)(shade | (shade << 5) | (shade << 10));
            palette[index * 2] = (byte)(raw & 0xFF);
            palette[(index * 2) + 1] = (byte)(raw >> 8);
        }

        return palette;
    }

    private static byte[] DecompressPortraitPayload(byte[] data, int offset)
    {
        if (offset + 6 > data.Length || data[offset] != (byte)'L' || data[offset + 1] != (byte)'e')
        {
            throw new InvalidDataException("Portrait data does not contain a supported compression header.");
        }

        var output = new List<byte>();
        var cursor = offset + 6;
        while (cursor < data.Length)
        {
            var command = data[cursor++];
            if (command != 0xAA)
            {
                break;
            }

            for (var i = 0; i < 4 && cursor < data.Length; i++)
            {
                output.Add(data[cursor++]);
            }
        }

        return output.ToArray();
    }

    private sealed class MutableLargePartDisplayPiece
    {
        public MutableLargePartDisplayPiece(
            int descriptorId,
            int recordOffset,
            int imageOffset,
            int paletteOffset,
            byte[] paletteBytes,
            int paletteBank,
            int x,
            int y,
            int loadedTileCount,
            int tileWidth,
            int tileHeight,
            byte[] pixelIndices)
        {
            DescriptorId = descriptorId;
            RecordOffset = recordOffset;
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

    private static bool TryReadOptionalPointer(RomFile romFile, int pointerOffset, out int fileOffset)
    {
        if (!GbaPointer.TryReadFileOffset(romFile.Data, pointerOffset, out fileOffset))
        {
            fileOffset = 0;
            return false;
        }

        return true;
    }
}
