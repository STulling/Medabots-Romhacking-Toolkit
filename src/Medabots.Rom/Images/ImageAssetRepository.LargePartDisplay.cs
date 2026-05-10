using Medabots.Rom.Compression;
using Medabots.Rom.Parts;

namespace Medabots.Rom.Images;

public sealed partial class ImageAssetRepository
{
    public IReadOnlyList<LargePartDisplayDescriptorRecord> ReadLargePartDisplayDescriptorRecords(RomFile romFile, PartDefinition part, int? variantSelector = null)
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
        var resolvedVariantSelector = GetLargeDisplayVariantSelector(part.Kind, variantSelector);
        var records = new List<LargePartDisplayDescriptorRecord>();
        var visited = new HashSet<(int DescriptorId, int TableIndex)>();
        foreach (var rootEntry in rootEntries)
        {
            var descriptorId = (int)(rootEntry & 0x3F);
            var tableIndex = ResolveCompositePreviewTableIndex(rootEntry, resolvedVariantSelector);
            ReadLargePartDisplayDescriptorRecordRecursive(romFile, appearanceEntries, appearanceEntryOffset, descriptorId, tableIndex, resolvedVariantSelector, records, visited);
        }

        return records;
    }

    public IReadOnlyList<LargePartDisplayDescriptorVariantResolution> ReadLargePartDisplayDescriptorVariants(RomFile romFile, PartDefinition part, int descriptorId)
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

        var appearanceEntry = appearanceEntries.FirstOrDefault(entry => (int)(entry & 0x3F) == descriptorId);
        if (appearanceEntry == 0)
        {
            return [];
        }

        var descriptorPointerOffset = CompositePreviewDescriptorPointerTableOffset + (descriptorId * sizeof(uint));
        var descriptorOffset = ReadRequiredPointer(romFile, descriptorPointerOffset);
        var blobPointerTableOffset = ReadRequiredPointer(romFile, descriptorOffset);
        return BuildVariantResolutions(romFile, appearanceEntry, blobPointerTableOffset, GetVariantSelectorsForPartKind(part.Kind)).ToArray();
    }

    public LargePartDisplayPieceAsset ReadLargePartDisplayPieceFromRecord(RomFile romFile, LargePartDisplayDescriptorRecord record)
    {
        ArgumentNullException.ThrowIfNull(romFile);
        ArgumentNullException.ThrowIfNull(record);
        if (record.ImageOffset <= 0)
        {
            throw new InvalidDataException($"Descriptor {record.DescriptorId} has no image target for variant {record.SelectedVariantSelector}.");
        }

        var decoded = GbaLz77.Decompress(romFile.Data, record.ImageOffset)
            ?? throw new InvalidDataException($"Large part display descriptor {record.DescriptorId} does not contain valid LZ77 image data.");
        var unpacked = TileImageCodec.Split4BppTiles(decoded);
        var tileWidth = Math.Max(1, record.EffectiveWidth / 8);
        var tileHeight = Math.Max(1, record.EffectiveHeight / 8);
        var allocatedPixels = new byte[Math.Max(tileWidth * tileHeight * 64, unpacked.Length)];
        Array.Copy(unpacked, allocatedPixels, Math.Min(unpacked.Length, allocatedPixels.Length));
        var paletteBytes = record.PaletteOffset == 0 ? [] : romFile.ReadBytes(record.PaletteOffset, PaletteSize).ToArray();

        return new LargePartDisplayPieceAsset(
            record.DescriptorId,
            record.AppearanceEntryOffset,
            record.RecordOffset,
            record.DescriptorRecordBytes.ToArray(),
            record.ImagePointerOffset,
            record.PalettePointerOffset,
            record.ImageOffset,
            record.PaletteOffset,
            paletteBytes,
            record.PaletteBank,
            record.X,
            record.Y,
            record.SiblingDescriptorId,
            record.ChildDescriptorId,
            record.RawWidth,
            record.RawHeight,
            record.SizeDivisors,
            Math.Max(1, unpacked.Length / 64),
            false,
            false,
            new IndexedImage(tileWidth, tileHeight, allocatedPixels, paletteBytes));
    }

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
        var rootRecordOffset = ReadRequiredPointer(romFile, CompositePreviewDescriptorPointerTableOffset + (rootDescriptorId * sizeof(uint)));

        var resolvedVariantSelector = GetLargeDisplayVariantSelector(part.Kind, variantSelector);
        var descriptorRecords = ReadLargePartDisplayDescriptorRecords(romFile, part, resolvedVariantSelector);
        var pieces = descriptorRecords
            .Where(record => record.HasImage)
            .Select(record => ReadLargePartDisplayPieceFromRecord(romFile, record))
            .ToArray();

        var initialPaletteBanks = BuildInitialLargeDisplayPaletteBanks(romFile);
        return new LargePartDisplayAsset(
            part.Id,
            partOrdinal,
            part.Kind,
            resolvedVariantSelector,
            rootDescriptorId,
            rootRecordOffset,
            initialPaletteBanks,
            pieces);
    }

    public MedabotLargeDisplayFrame ReadMedabotLargeDisplayFrame(
        RomFile romFile,
        IReadOnlyList<PartDefinition> allParts,
        int medabotId,
        int side,
        int anchorX = 0x3C,
        int anchorY = 0x32)
    {
        ArgumentNullException.ThrowIfNull(romFile);
        ArgumentNullException.ThrowIfNull(allParts);

        var head = allParts.First(part => part.MedabotId == medabotId && part.Kind == PartKind.Head);
        var rightArm = allParts.First(part => part.MedabotId == medabotId && part.Kind == PartKind.RightArm);
        var leftArm = allParts.First(part => part.MedabotId == medabotId && part.Kind == PartKind.LeftArm);
        var legs = allParts.First(part => part.MedabotId == medabotId && part.Kind == PartKind.Legs);
        var headRootDescriptorId = GetFirstAppearanceDescriptorId(romFile, head);
        var rightArmRootDescriptorId = GetFirstAppearanceDescriptorId(romFile, rightArm);
        var leftArmRootDescriptorId = GetFirstAppearanceDescriptorId(romFile, leftArm);
        var legsRootDescriptorId = GetFirstAppearanceDescriptorId(romFile, legs);
        var combinedAppearanceEntries = ReadCombinedCompositePreviewAppearanceEntries(romFile, head, rightArm, leftArm, legs);
        var resolvedSide = side & 1;
        var imageSourceOverrides = BuildCompositePreviewImageSourceOverrides(romFile, rightArm, leftArm, resolvedSide);
        var syntheticDescriptors = BuildCombinedPreviewSyntheticDescriptors(
            romFile,
            headRootDescriptorId,
            rightArmRootDescriptorId,
            leftArmRootDescriptorId,
            legsRootDescriptorId,
            resolvedSide != 0);
        var mutablePieces = new List<MutableLargePartDisplayPiece>();
        ReadCompositeLargeDisplayDescriptorRecursive(
            romFile,
            combinedAppearanceEntries,
            1,
            resolvedSide,
            anchorY << 8,
            anchorX << 8,
            0x1C2,
            mutablePieces,
            new HashSet<int>(),
            syntheticDescriptors,
            imageSourceOverrides);

        var combinedFrame = new MedabotLargeDisplayFrame(
            medabotId,
            resolvedSide,
            false,
            BuildInitialLargeDisplayPaletteBanks(romFile),
            mutablePieces
                .OrderBy(piece => piece.SortKey)
                // The ROM keeps OAM entries sorted by priority key, but lower OAM indexes win
                // overlap. Our software blitter draws later pieces on top, so render cache order
                // has to be reversed to match OBJ priority.
                .Reverse()
                .Select(piece => piece.ToAsset())
                .ToArray());
        return combinedFrame;
    }

    private static int GetCompositePreviewAppearanceTableOffset(PartKind kind) => kind switch
    {
        PartKind.Head => CompositePreviewHeadAppearanceTableOffset,
        PartKind.RightArm => CompositePreviewRightArmAppearanceTableOffset,
        PartKind.LeftArm => CompositePreviewLeftArmAppearanceTableOffset,
        PartKind.Legs => CompositePreviewLegsAppearanceTableOffset,
        _ => throw new InvalidOperationException($"Unsupported part kind '{kind}'.")
    };

    private IReadOnlyList<uint> ReadCombinedCompositePreviewAppearanceEntries(
        RomFile romFile,
        PartDefinition head,
        PartDefinition rightArm,
        PartDefinition leftArm,
        PartDefinition legs)
    {
        var combined = new List<uint>(32);
        AppendCompositePreviewAppearanceEntries(romFile, head, combined);
        AppendCompositePreviewAppearanceEntries(romFile, rightArm, combined);
        AppendCompositePreviewAppearanceEntries(romFile, leftArm, combined);
        AppendCompositePreviewAppearanceEntries(romFile, legs, combined);
        combined.Add(0);
        return combined;
    }

    private void AppendCompositePreviewAppearanceEntries(RomFile romFile, PartDefinition part, ICollection<uint> target)
    {
        var (_, appearanceEntries) = ReadCompositePreviewAppearanceRow(romFile, part.Kind, part.Id / 4);
        foreach (var entry in appearanceEntries)
        {
            if ((entry & 0x3F) == 0)
            {
                break;
            }

            target.Add(entry);
        }
    }

    private (int RowOffset, uint[] Entries) ReadCompositePreviewAppearanceRow(RomFile romFile, PartKind kind, int partOrdinal)
    {
        var appearanceTableOffset = GetCompositePreviewAppearanceTableOffset(kind);
        var appearanceEntrySize = kind == PartKind.Legs ? 0x20 : 0x10;
        var appearanceEntryOffset = appearanceTableOffset + (partOrdinal * appearanceEntrySize);
        if (appearanceEntryOffset < 0 || appearanceEntryOffset + appearanceEntrySize > romFile.Length)
        {
            throw new InvalidDataException($"Large part display appearance row for part ordinal {partOrdinal} is out of range.");
        }

        var appearanceEntries = new uint[appearanceEntrySize / sizeof(uint)];
        for (var index = 0; index < appearanceEntries.Length; index++)
        {
            appearanceEntries[index] = BitConverter.ToUInt32(romFile.Data, appearanceEntryOffset + (index * sizeof(uint)));
        }

        return (appearanceEntryOffset, appearanceEntries);
    }

    private void ReadCompositeLargeDisplayDescriptorRecursive(
        RomFile romFile,
        IReadOnlyList<uint> appearanceEntries,
        int descriptorId,
        int side,
        int anchorYFixed,
        int anchorXFixed,
        int inheritedSortKey,
        ICollection<MutableLargePartDisplayPiece> pieces,
        ISet<int> recursionStack,
        IReadOnlyDictionary<int, byte[]> syntheticDescriptors)
        => ReadCompositeLargeDisplayDescriptorRecursive(
            romFile,
            appearanceEntries,
            descriptorId,
            side,
            anchorYFixed,
            anchorXFixed,
            inheritedSortKey,
            pieces,
            recursionStack,
            syntheticDescriptors,
            new Dictionary<int, CompositePreviewImageSourceOverride>());

    private void ReadCompositeLargeDisplayDescriptorRecursive(
        RomFile romFile,
        IReadOnlyList<uint> appearanceEntries,
        int descriptorId,
        int side,
        int anchorYFixed,
        int anchorXFixed,
        int inheritedSortKey,
        ICollection<MutableLargePartDisplayPiece> pieces,
        ISet<int> recursionStack,
        IReadOnlyDictionary<int, byte[]> syntheticDescriptors,
        IReadOnlyDictionary<int, CompositePreviewImageSourceOverride> imageSourceOverrides)
    {
        if (descriptorId <= 0 || !recursionStack.Add(descriptorId))
        {
            return;
        }

        var appearanceEntryRaw = FindCombinedLargeDisplayAppearanceEntry(appearanceEntries, descriptorId);
        var renderDescriptorId = descriptorId;
        if (!syntheticDescriptors.ContainsKey(descriptorId) && (side & 1) != 0)
        {
            renderDescriptorId = MirrorCompositePreviewDescriptorId(descriptorId);
        }

        if (!TryGetCombinedPreviewDescriptorRecord(romFile, descriptorId, syntheticDescriptors, out var logicalDescriptorOffset, out var logicalDescriptorBytes, out _))
        {
            recursionStack.Remove(descriptorId);
            return;
        }

        if (!TryGetCombinedPreviewDescriptorRecord(romFile, renderDescriptorId, syntheticDescriptors, out _, out var descriptorBytes, out _))
        {
            recursionStack.Remove(descriptorId);
            return;
        }

        var hasImageSourceOverride = imageSourceOverrides.TryGetValue(descriptorId, out var imageSourceOverride);
        var imageSourceDescriptorId = hasImageSourceOverride ? imageSourceOverride.DescriptorId : descriptorId;
        var imageSourceAppearanceEntryRaw = hasImageSourceOverride
            ? imageSourceOverride.AppearanceEntryRaw
            : appearanceEntryRaw;
        if (!TryGetCombinedPreviewDescriptorRecord(romFile, imageSourceDescriptorId, syntheticDescriptors, out var imageSourceDescriptorOffset, out _, out _))
        {
            recursionStack.Remove(descriptorId);
            return;
        }

        var rawHeightPixels = logicalDescriptorBytes[0x12];
        var rawWidthPixels = logicalDescriptorBytes[0x13];
        var height = rawHeightPixels;
        var width = rawWidthPixels;
        var divisors = logicalDescriptorBytes[0x14];
        var heightDivisor = Math.Max(1, divisors & 0x0F);
        var widthDivisor = Math.Max(1, divisors >> 4);
        width /= (byte)widthDivisor;
        height /= (byte)heightDivisor;

        var tileWidth = Math.Max(1, width / 8);
        var tileHeight = Math.Max(1, height / 8);
        var rawY = BitConverter.ToInt32(descriptorBytes, 0x04);
        var rawX = BitConverter.ToInt32(descriptorBytes, 0x08);
        var localOffsetY = (sbyte)descriptorBytes[0x0C];
        var localOffsetX = (sbyte)descriptorBytes[0x0D];
        var isCenteredDescriptor = (appearanceEntryRaw & 0x4000) != 0;
        var (pa, pb, pc, pd) = GetDefaultLargeDisplayTransformMatrix();
        var pieceAnchorYFixed = anchorYFixed + (localOffsetX * pb) + (localOffsetY * pd);
        var pieceAnchorXFixed = anchorXFixed + (((side & 1) == 0 ? localOffsetX : -localOffsetX) * pa) + (localOffsetY * pc);
        var x = (((side & 1) == 0)
            ? pieceAnchorXFixed - (rawX << 8)
            : pieceAnchorXFixed - ((rawWidthPixels - rawX) << 8)) >> 8;
        var y = (pieceAnchorYFixed - (rawY << 8)) >> 8;
        var sortKey = Math.Max(0, inheritedSortKey + (sbyte)descriptorBytes[0x0E]);
        // In the ROM, side-1 centered (0x4000) descriptors are mirrored via the affine matrix
        // branch rather than a normal OBJ hflip bit. The toolkit has no affine renderer here, so
        // use a bitmap mirror for side 1 while keeping the current ROM-derived coordinates.
        var mirrorDisplayHorizontally = (side & 1) != 0;

        if (appearanceEntryRaw != 0)
        {
            var imageAppearanceEntryRaw = imageSourceAppearanceEntryRaw != 0
                ? imageSourceAppearanceEntryRaw
                : appearanceEntryRaw;
            var imageVariantSelector = hasImageSourceOverride ? 0 : side & 1;
            var resolvedTableIndex = ResolveCompositePreviewTableIndex(imageAppearanceEntryRaw, imageVariantSelector);
            var blobPointerTableOffset = ReadRequiredPointer(romFile, imageSourceDescriptorOffset);
            var imagePointerOffset = blobPointerTableOffset + (resolvedTableIndex * sizeof(uint));
            var imageOffset = ReadRequiredPointer(romFile, imagePointerOffset);
            var decoded = GbaLz77.Decompress(romFile.Data, imageOffset)
                ?? throw new InvalidDataException($"Large part display descriptor {descriptorId} does not contain valid LZ77 image data.");
            var unpacked = TileImageCodec.Split4BppTiles(decoded);
            var paletteTableIndex = ResolveCompositePreviewTableIndex(appearanceEntryRaw, side & 1);
            var paletteBlobPointerTableOffset = ReadRequiredPointer(romFile, logicalDescriptorOffset);
            var palettePointerOffset = paletteBlobPointerTableOffset + ((paletteTableIndex * sizeof(uint)) + sizeof(uint));
            var paletteOffset = TryReadOptionalPointer(romFile, palettePointerOffset, out var resolvedPaletteOffset)
                ? resolvedPaletteOffset
                : 0;
            if (paletteOffset == imageOffset)
            {
                paletteOffset = 0;
                palettePointerOffset = 0;
            }

            var paletteBytes = paletteOffset == 0 ? [] : romFile.ReadBytes(paletteOffset, PaletteSize).ToArray();
            var paletteBank = logicalDescriptorBytes[0x11];
            var loadedTileCount = Math.Max(1, unpacked.Length / 64);
            var allocatedPixels = new byte[Math.Max(tileWidth * tileHeight * 64, unpacked.Length)];
            Array.Copy(unpacked, allocatedPixels, Math.Min(unpacked.Length, allocatedPixels.Length));

            pieces.Add(new MutableLargePartDisplayPiece(
                descriptorId,
                0,
                logicalDescriptorOffset,
                logicalDescriptorBytes,
                imagePointerOffset,
                palettePointerOffset,
                imageOffset,
                paletteOffset,
                paletteBytes,
                paletteBank,
                x,
                y,
                logicalDescriptorBytes[0x0F],
                logicalDescriptorBytes[0x10],
                logicalDescriptorBytes[0x13],
                logicalDescriptorBytes[0x12],
                logicalDescriptorBytes[0x14],
                loadedTileCount,
                tileWidth,
                tileHeight,
                mirrorDisplayHorizontally,
                false,
                sortKey,
                allocatedPixels));
        }

        var childDescriptorId = logicalDescriptorBytes[0x10];
        if (childDescriptorId != 0)
        {
            ReadCompositeLargeDisplayDescriptorRecursive(
                romFile,
                appearanceEntries,
                childDescriptorId,
                side,
                pieceAnchorYFixed,
                pieceAnchorXFixed,
                sortKey,
                pieces,
                recursionStack,
                syntheticDescriptors,
                imageSourceOverrides);
        }

        var siblingDescriptorId = logicalDescriptorBytes[0x0F];
        if (siblingDescriptorId != 0)
        {
            ReadCompositeLargeDisplayDescriptorRecursive(
                romFile,
                appearanceEntries,
                siblingDescriptorId,
                side,
                anchorYFixed,
                anchorXFixed,
                inheritedSortKey,
                pieces,
                recursionStack,
                syntheticDescriptors,
                imageSourceOverrides);
        }

        recursionStack.Remove(descriptorId);
    }

    private void ReadLargePartDisplayDescriptorRecursive(
        RomFile romFile,
        IReadOnlyList<uint> appearanceEntries,
        int appearanceEntryBaseOffset,
        int descriptorId,
        int tableIndex,
        int variantSelector,
        int anchorXFixed,
        int anchorYFixed,
        ICollection<MutableLargePartDisplayPiece> pieces,
        ISet<(int DescriptorId, int TableIndex)> recursionStack)
    {
        var stackKey = (descriptorId, tableIndex);
        if (descriptorId <= 0 || !recursionStack.Add(stackKey))
        {
            return;
        }

        var descriptorPointerOffset = CompositePreviewDescriptorPointerTableOffset + (descriptorId * sizeof(uint));
        var descriptorOffset = ReadRequiredPointer(romFile, descriptorPointerOffset);
        if (descriptorOffset + 0x18 > romFile.Length)
        {
            throw new InvalidDataException($"Large part display descriptor {descriptorId} is out of range.");
        }
        var descriptorBytes = romFile.ReadBytes(descriptorOffset, 0x18).ToArray();
        var appearanceEntryOffset = FindLargeDisplayAppearanceEntryOffset(appearanceEntries, appearanceEntryBaseOffset, descriptorId);
        var appearanceEntryRaw = FindLargeDisplayAppearanceEntry(romFile, appearanceEntryBaseOffset, descriptorId, appearanceEntries);
        var height = descriptorBytes[0x12];
        var width = descriptorBytes[0x13];
        var divisors = descriptorBytes[0x14];
        var heightDivisor = Math.Max(1, divisors & 0x0F);
        var widthDivisor = Math.Max(1, divisors >> 4);
        width /= (byte)widthDivisor;
        height /= (byte)heightDivisor;

        var tileWidth = Math.Max(1, width / 8);
        var tileHeight = Math.Max(1, height / 8);
        var rawY = BitConverter.ToInt32(descriptorBytes, 0x04);
        var rawX = BitConverter.ToInt32(descriptorBytes, 0x08);
        var localOffsetY = (sbyte)descriptorBytes[0x0C];
        var localOffsetX = (sbyte)descriptorBytes[0x0D];
        var (pa, pb, pc, pd) = GetDefaultLargeDisplayTransformMatrix();
        var pieceAnchorYFixed = anchorYFixed + (localOffsetX * pb) + (localOffsetY * pd);
        var pieceAnchorXFixed = anchorXFixed + (localOffsetX * pa) + (localOffsetY * pc);
        var x = (pieceAnchorXFixed - (rawX << 8)) >> 8;
        var y = (pieceAnchorYFixed - (rawY << 8)) >> 8;

        if (appearanceEntryRaw != 0)
        {
            var resolvedTableIndex = ResolveCompositePreviewTableIndex(appearanceEntryRaw, variantSelector);
            var blobPointerTableOffset = ReadRequiredPointer(romFile, descriptorOffset);
            var imagePointerOffset = blobPointerTableOffset + (resolvedTableIndex * sizeof(uint));
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
            var paletteBank = descriptorBytes[0x11];
            var loadedTileCount = Math.Max(1, unpacked.Length / 64);
            var allocatedPixels = new byte[Math.Max(tileWidth * tileHeight * 64, unpacked.Length)];
            Array.Copy(unpacked, allocatedPixels, Math.Min(unpacked.Length, allocatedPixels.Length));

            pieces.Add(new MutableLargePartDisplayPiece(
                descriptorId,
                appearanceEntryOffset,
                descriptorOffset,
                descriptorBytes,
                imagePointerOffset,
                palettePointerOffset,
                imageOffset,
                paletteOffset,
                paletteBytes,
                paletteBank,
                x,
                y,
                descriptorBytes[0x0F],
                descriptorBytes[0x10],
                descriptorBytes[0x13],
                descriptorBytes[0x12],
                descriptorBytes[0x14],
                loadedTileCount,
                tileWidth,
                tileHeight,
                false,
                false,
                0,
                allocatedPixels));
        }

        var childDescriptorId = romFile.Data[descriptorOffset + 0x10];
        if (childDescriptorId != 0)
        {
            ReadLargePartDisplayDescriptorRecursive(
                romFile,
                appearanceEntries,
                appearanceEntryBaseOffset,
                childDescriptorId,
                ResolveCompositePreviewTableIndex(appearanceEntries, childDescriptorId, variantSelector),
                variantSelector,
                pieceAnchorXFixed,
                pieceAnchorYFixed,
                pieces,
                recursionStack);
        }

        var siblingDescriptorId = romFile.Data[descriptorOffset + 0x0F];
        if (siblingDescriptorId != 0)
        {
            ReadLargePartDisplayDescriptorRecursive(
                romFile,
                appearanceEntries,
                appearanceEntryBaseOffset,
                siblingDescriptorId,
                ResolveCompositePreviewTableIndex(appearanceEntries, siblingDescriptorId, variantSelector),
                variantSelector,
                anchorXFixed,
                anchorYFixed,
                pieces,
                recursionStack);
        }

        recursionStack.Remove(stackKey);
    }

    private void ReadLargePartDisplayDescriptorRecordRecursive(
        RomFile romFile,
        IReadOnlyList<uint> appearanceEntries,
        int appearanceEntryBaseOffset,
        int descriptorId,
        int tableIndex,
        int variantSelector,
        ICollection<LargePartDisplayDescriptorRecord> records,
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

        var descriptorBytes = romFile.ReadBytes(descriptorOffset, 0x18).ToArray();
        var blobPointerTableOffset = ReadRequiredPointer(romFile, descriptorOffset);
        var imagePointerOffset = blobPointerTableOffset + (tableIndex * sizeof(uint));
        var imageOffset = TryReadOptionalPointer(romFile, imagePointerOffset, out var resolvedImageOffset)
            ? resolvedImageOffset
            : 0;
        var palettePointerOffset = imagePointerOffset + sizeof(uint);
        var paletteOffset = TryReadOptionalPointer(romFile, palettePointerOffset, out var resolvedPaletteOffset)
            ? resolvedPaletteOffset
            : 0;
        if (paletteOffset == imageOffset)
        {
            paletteOffset = 0;
            palettePointerOffset = 0;
        }

        var appearanceEntryRaw = FindLargeDisplayAppearanceEntry(romFile, appearanceEntryBaseOffset, descriptorId, appearanceEntries);
        var rawY = BitConverter.ToInt32(descriptorBytes, 0x04);
        var rawX = BitConverter.ToInt32(descriptorBytes, 0x08);
        var heightDivisor = Math.Max(1, descriptorBytes[0x14] & 0x0F);
        var widthDivisor = Math.Max(1, descriptorBytes[0x14] >> 4);
        records.Add(new LargePartDisplayDescriptorRecord(
            descriptorId,
            FindLargeDisplayAppearanceEntryOffset(appearanceEntries, appearanceEntryBaseOffset, descriptorId),
            appearanceEntryRaw,
            descriptorPointerOffset,
            descriptorOffset,
            blobPointerTableOffset,
            descriptorBytes,
            imagePointerOffset,
            palettePointerOffset,
            imageOffset,
            paletteOffset,
            rawX,
            rawY,
            -rawX,
            -rawY,
            descriptorBytes[0x0F],
            descriptorBytes[0x10],
            descriptorBytes[0x11],
            descriptorBytes[0x13],
            descriptorBytes[0x12],
            descriptorBytes[0x14],
            descriptorBytes[0x0C],
            descriptorBytes[0x0D],
            descriptorBytes[0x0E],
            descriptorBytes[0x15],
            descriptorBytes[0x16],
            descriptorBytes[0x17],
            widthDivisor,
            heightDivisor,
            Math.Max(8, descriptorBytes[0x13] / widthDivisor),
            Math.Max(8, descriptorBytes[0x12] / heightDivisor),
            tableIndex,
            imageOffset > 0,
            variantSelector,
            BuildVariantResolutions(romFile, appearanceEntryRaw, blobPointerTableOffset, GetVariantSelectorsForPartKind(GetPartKindForAppearanceOffset(appearanceEntryBaseOffset))).ToArray()));

        if (descriptorBytes[0x10] != 0)
        {
            ReadLargePartDisplayDescriptorRecordRecursive(romFile, appearanceEntries, appearanceEntryBaseOffset, descriptorBytes[0x10], ResolveCompositePreviewTableIndex(appearanceEntries, descriptorBytes[0x10], variantSelector), variantSelector, records, visited);
        }

        if (descriptorBytes[0x0F] != 0)
        {
            ReadLargePartDisplayDescriptorRecordRecursive(romFile, appearanceEntries, appearanceEntryBaseOffset, descriptorBytes[0x0F], ResolveCompositePreviewTableIndex(appearanceEntries, descriptorBytes[0x0F], variantSelector), variantSelector, records, visited);
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

    private void ApplyCompositeMedabotArmOverlayPass(
        RomFile romFile,
        int rightArmPartOrdinal,
        int leftArmPartOrdinal,
        int variantSelector,
        IList<MutableLargePartDisplayPiece> pieces)
    {
        var (_, rightEntries) = ReadCompositePreviewAppearanceRow(romFile, PartKind.RightArm, rightArmPartOrdinal);
        var (_, leftEntries) = ReadCompositePreviewAppearanceRow(romFile, PartKind.LeftArm, leftArmPartOrdinal);
        ApplyCompositeMedabotArmOverlayPass(romFile, leftEntries, rightEntries, variantSelector, pieces);
        ApplyCompositeMedabotArmOverlayPass(romFile, rightEntries, leftEntries, variantSelector, pieces);
    }

    private void ApplyCompositeMedabotArmOverlayPass(
        RomFile romFile,
        IReadOnlyList<uint> sourceEntries,
        IReadOnlyList<uint> targetEntries,
        int variantSelector,
        IList<MutableLargePartDisplayPiece> pieces)
    {
        var limit = Math.Min(sourceEntries.Count, targetEntries.Count);
        for (var index = 0; index < limit; index++)
        {
            var targetEntry = targetEntries[index];
            var targetDescriptorId = (int)(targetEntry & 0x3F);
            if (targetDescriptorId == 0)
            {
                break;
            }

            var targetByte1Signed = unchecked((sbyte)((targetEntry >> 8) & 0xFF));
            if (targetByte1Signed < 0)
            {
                continue;
            }

            var sourceDescriptorId = (int)(sourceEntries[index] & 0x3F);
            if (sourceDescriptorId == 0)
            {
                continue;
            }

            var sourceEntry = FindCombinedLargeDisplayAppearanceEntry(sourceEntries, sourceDescriptorId);
            if (sourceEntry == 0)
            {
                continue;
            }

            var selector = ResolveCompositePreviewTableIndex(sourceEntry, variantSelector);
            var sourceDescriptorOffset = ReadRequiredPointer(romFile, CompositePreviewDescriptorPointerTableOffset + (sourceDescriptorId * sizeof(uint)));
            var blobPointerTableOffset = ReadRequiredPointer(romFile, sourceDescriptorOffset);
            var imageOffset = ReadRequiredPointer(romFile, blobPointerTableOffset + (selector * sizeof(uint)));
            var decoded = GbaLz77.Decompress(romFile.Data, imageOffset);
            if (decoded is null)
            {
                continue;
            }

            var unpacked = TileImageCodec.Split4BppTiles(decoded);
            var targetPiece = pieces.FirstOrDefault(piece => piece.DescriptorId == targetDescriptorId);
            if (targetPiece is null)
            {
                continue;
            }

            Array.Copy(unpacked, targetPiece.PixelIndices, Math.Min(unpacked.Length, targetPiece.PixelIndices.Length));
        }
    }

    private MedabotLargeDisplayFrame BuildLegacyMedabotLargeDisplayFrame(
        RomFile romFile,
        int medabotId,
        int side,
        PartDefinition head,
        PartDefinition rightArm,
        PartDefinition leftArm,
        PartDefinition legs,
        IReadOnlyDictionary<PartKind, (int X, int Y)> rootAnchors)
    {
        var mutablePieces = new List<MutableLargePartDisplayPiece>();
        const int previewVariantSelector = 0;
        var entries = side == 0
            ? new (PartDefinition Part, int VariantSelector, bool MirrorPieces, Func<int, int> DescriptorRemap)[]
            {
                (head, 0, false, descriptorId => descriptorId),
                (rightArm, previewVariantSelector, false, descriptorId => descriptorId),
                (leftArm, previewVariantSelector, false, descriptorId => descriptorId),
                (legs, 0, false, descriptorId => descriptorId)
            }
            : new (PartDefinition Part, int VariantSelector, bool MirrorPieces, Func<int, int> DescriptorRemap)[]
            {
                (head, 0, false, descriptorId => descriptorId),
                (rightArm, previewVariantSelector, false, descriptorId => descriptorId),
                (leftArm, previewVariantSelector, false, descriptorId => descriptorId),
                (legs, 0, false, descriptorId => descriptorId)
            };
        foreach (var entry in entries)
        {
            AppendLegacyMedabotLargeDisplayPieces(
                romFile,
                entry.Part,
                entry.VariantSelector,
                side,
                entry.MirrorPieces,
                entry.DescriptorRemap,
                rootAnchors,
                mutablePieces);
        }

        if (side != 0)
        {
            // The mirrored-side Medawatch path copies arm graphics into the opposite slot rows
            // before it flips the synthetic descriptor offsets. Slot geometry stays on the
            // original left/right descriptor ids; only the graphics are copied across.
            ApplyLegacyMedabotArmOverlayPass(romFile, rightArm.Id / 4, leftArm.Id / 4, previewVariantSelector, mutablePieces);
        }

        return new MedabotLargeDisplayFrame(
            medabotId,
            side,
            false,
            BuildInitialLargeDisplayPaletteBanks(romFile),
            mutablePieces
                .OrderBy(piece => piece.SortKey)
                .ThenBy(piece => piece.Y)
                .ThenBy(piece => piece.X)
                .Select(piece => piece.ToAsset())
                .ToArray());
    }

    private void AppendLegacyMedabotLargeDisplayPieces(
        RomFile romFile,
        PartDefinition part,
        int variantSelector,
        int side,
        bool mirrorPieces,
        Func<int, int> descriptorRemap,
        IReadOnlyDictionary<PartKind, (int X, int Y)> rootAnchors,
        ICollection<MutableLargePartDisplayPiece> pieces)
    {
        var asset = ReadLargePartDisplay(romFile, part, variantSelector);
        AppendLegacyMedabotLargeDisplayPiecesByRootAnchor(
            romFile,
            asset,
            part.Kind,
            side,
            mirrorPieces,
            descriptorRemap,
            rootAnchors,
            pieces);
    }

    private void AppendLegacyMedabotLargeDisplayPiecesByRootAnchor(
        RomFile romFile,
        LargePartDisplayAsset asset,
        PartKind kind,
        int side,
        bool mirrorPieces,
        Func<int, int> descriptorRemap,
        IReadOnlyDictionary<PartKind, (int X, int Y)> rootAnchors,
        ICollection<MutableLargePartDisplayPiece> pieces)
    {
        if (asset.Pieces.Count == 0)
        {
            return;
        }

        var rootPiece = asset.Pieces.FirstOrDefault(piece => piece.DescriptorId == asset.RootDescriptorId) ?? asset.Pieces[0];
        if (!rootAnchors.TryGetValue(kind, out var rootAnchor))
        {
            return;
        }

        for (var pieceIndex = 0; pieceIndex < asset.Pieces.Count; pieceIndex++)
        {
            var piece = asset.Pieces[pieceIndex];
            var resolvedDescriptorId = descriptorRemap(piece.DescriptorId);
            var deltaX = piece.X - rootPiece.X;
            var deltaY = piece.Y - rootPiece.Y;
            var preserveArmSlotGeometry = side != 0 && (kind is PartKind.LeftArm or PartKind.RightArm);
            var x = side == 0 || preserveArmSlotGeometry
                ? rootAnchor.X + deltaX
                : rootAnchor.X + rootPiece.Image.Width - piece.Image.Width - deltaX;
            var y = rootAnchor.Y + deltaY;
            var sortKey = pieceIndex;
            var shouldMirrorPiece = mirrorPieces != piece.MirrorDisplayHorizontally;
            var appearanceEntryRaw = piece.AppearanceEntryOffset != 0 && piece.AppearanceEntryOffset + sizeof(uint) <= romFile.Length
                ? BitConverter.ToUInt32(romFile.Data, piece.AppearanceEntryOffset)
                : 0u;
            if (side != 0 &&
                kind is PartKind.LeftArm or PartKind.RightArm &&
                (appearanceEntryRaw & 0x4000) != 0)
            {
                // Centered-arm descriptors use the transformed renderer branch in the ROM.
                // The fallback cannot reproduce the affine path, so mirror the bitmap for any
                // side-1 arm entry that carries appearance bit 0x4000 rather than special-casing
                // only the root descriptor.
                shouldMirrorPiece = !shouldMirrorPiece;
            }

            pieces.Add(new MutableLargePartDisplayPiece(
                resolvedDescriptorId,
                piece.AppearanceEntryOffset,
                piece.RecordOffset,
                piece.DescriptorRecordBytes.ToArray(),
                piece.ImagePointerOffset,
                piece.PalettePointerOffset,
                piece.ImageOffset,
                piece.PaletteOffset,
                piece.PaletteBytes.ToArray(),
                piece.PaletteBank,
                x,
                y,
                piece.SiblingDescriptorId,
                piece.ChildDescriptorId,
                piece.RawWidth,
                piece.RawHeight,
                piece.SizeDivisors,
                piece.LoadedTileCount,
                piece.Image.TileWidth,
                piece.Image.TileHeight,
                shouldMirrorPiece,
                piece.ForceIndependentSource,
                sortKey,
                piece.Image.PixelIndices.ToArray()));
        }
    }

    private void ApplyLegacyMedabotArmOverlayPass(
        RomFile romFile,
        int rightArmPartOrdinal,
        int leftArmPartOrdinal,
        int variantSelector,
        IList<MutableLargePartDisplayPiece> pieces)
    {
        var (_, rightEntries) = ReadCompositePreviewAppearanceRow(romFile, PartKind.RightArm, rightArmPartOrdinal);
        var (_, leftEntries) = ReadCompositePreviewAppearanceRow(romFile, PartKind.LeftArm, leftArmPartOrdinal);
        ApplyCompositeMedabotArmOverlayPass(romFile, leftEntries, rightEntries, variantSelector, pieces);
        ApplyCompositeMedabotArmOverlayPass(romFile, rightEntries, leftEntries, variantSelector, pieces);
    }

    private static int RemapLeftArmDescriptorsToRightSlots(int descriptorId) => descriptorId switch
    {
        14 => 17,
        15 => 18,
        _ => descriptorId
    };

    private static int RemapRightArmDescriptorsToLeftSlots(int descriptorId) => descriptorId switch
    {
        17 => 14,
        18 => 15,
        _ => descriptorId
    };

    private static int RemapLeftArmChildDescriptorToRightSlot(int descriptorId) => descriptorId switch
    {
        15 => 18,
        _ => descriptorId
    };

    private static int RemapRightArmChildDescriptorToLeftSlot(int descriptorId) => descriptorId switch
    {
        18 => 15,
        _ => descriptorId
    };


    private const int LegacyMedabotPreviewWidth = 68;

    private IReadOnlyDictionary<PartKind, (int X, int Y)> BuildLegacyMedabotPreviewRootAnchors(
        PartDefinition head,
        PartDefinition rightArm,
        PartDefinition leftArm,
        PartDefinition legs,
        int side,
        RomFile romFile)
    {
        var anchors = new Dictionary<PartKind, (int X, int Y)>(4);
        anchors[PartKind.Head] = GetLegacyMedabotPreviewRootAnchor(PartKind.Head, side, GetRootPieceWidth(romFile, head));
        anchors[PartKind.RightArm] = GetLegacyMedabotPreviewRootAnchor(PartKind.RightArm, side, GetRootPieceWidth(romFile, rightArm));
        anchors[PartKind.LeftArm] = GetLegacyMedabotPreviewRootAnchor(PartKind.LeftArm, side, GetRootPieceWidth(romFile, leftArm));
        anchors[PartKind.Legs] = GetLegacyMedabotPreviewRootAnchor(PartKind.Legs, side, GetRootPieceWidth(romFile, legs));
        return anchors;
    }

    private int GetRootPieceWidth(
        RomFile romFile,
        PartDefinition part)
    {
        var descriptorId = GetFirstAppearanceDescriptorId(romFile, part);
        var descriptorPointerOffset = CompositePreviewDescriptorPointerTableOffset + (descriptorId * sizeof(uint));
        var descriptorOffset = ReadRequiredPointer(romFile, descriptorPointerOffset);
        var descriptorBytes = romFile.ReadBytes(descriptorOffset, 0x18).ToArray();
        var widthDivisor = Math.Max(1, descriptorBytes[0x14] >> 4);
        return Math.Max(8, descriptorBytes[0x13] / widthDivisor);
    }

    private static (int X, int Y) GetLegacyMedabotPreviewRootAnchor(PartKind kind, int side, int rootPieceWidth)
    {
        var side0 = kind switch
        {
            PartKind.Head => (24, 26),
            PartKind.RightArm => (0, 13),
            PartKind.LeftArm => (33, 12),
            PartKind.Legs => (24, 38),
            _ => (0, 0)
        };
        return side == 0
            ? side0
            : (LegacyMedabotPreviewWidth - (side0.Item1 + rootPieceWidth), side0.Item2);
    }

    private static bool IsCompositeMedabotFrameLikelyJumbled(MedabotLargeDisplayFrame frame)
    {
        var rightArmRoot = frame.Pieces.FirstOrDefault(piece => piece.DescriptorId == 17);
        var leftArmRoot = frame.Pieces.FirstOrDefault(piece => piece.DescriptorId == 14);
        if (rightArmRoot is null || leftArmRoot is null)
        {
            return true;
        }

        return Math.Abs(rightArmRoot.X - leftArmRoot.X) < 12;
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

    private static uint FindLargeDisplayAppearanceEntry(RomFile romFile, int rowOffset, int descriptorId, IReadOnlyList<uint> appearanceEntries)
    {
        for (var offset = 0; offset < appearanceEntries.Count; offset++)
        {
            var entry = appearanceEntries[offset];
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

        return FindLargeDisplayAppearanceEntry(romFile, rowOffset, descriptorId);
    }

    private static uint FindCombinedLargeDisplayAppearanceEntry(IReadOnlyList<uint> appearanceEntries, int descriptorId)
    {
        for (var offset = 0; offset < appearanceEntries.Count; offset++)
        {
            var entry = appearanceEntries[offset];
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

    private static bool TryReadOptionalPointerSafe(RomFile romFile, int pointerOffset, out int fileOffset)
    {
        try
        {
            return TryReadOptionalPointer(romFile, pointerOffset, out fileOffset);
        }
        catch (ArgumentOutOfRangeException)
        {
            fileOffset = 0;
            return false;
        }
    }

    private static bool TryGetCombinedPreviewDescriptorRecord(
        RomFile romFile,
        int descriptorId,
        IReadOnlyDictionary<int, byte[]> syntheticDescriptors,
        out int descriptorOffset,
        out byte[] descriptorBytes,
        out bool isSyntheticDescriptor)
    {
        if (syntheticDescriptors.TryGetValue(descriptorId, out var syntheticDescriptorBytes))
        {
            descriptorOffset = 0;
            descriptorBytes = syntheticDescriptorBytes;
            isSyntheticDescriptor = true;
            return true;
        }

        var descriptorPointerOffset = CompositePreviewDescriptorPointerTableOffset + (descriptorId * sizeof(uint));
        if (TryReadOptionalPointerSafe(romFile, descriptorPointerOffset, out descriptorOffset) &&
            descriptorOffset + 0x18 <= romFile.Length)
        {
            descriptorBytes = romFile.ReadBytes(descriptorOffset, 0x18).ToArray();
            isSyntheticDescriptor = false;
            return true;
        }

        descriptorOffset = 0;
        descriptorBytes = [];
        isSyntheticDescriptor = false;
        return false;
    }

    private IReadOnlyDictionary<int, byte[]> BuildCombinedPreviewSyntheticDescriptors(
        RomFile romFile,
        int headRootDescriptorId,
        int rightArmRootDescriptorId,
        int leftArmRootDescriptorId,
        int legsRootDescriptorId,
        bool mirrorSide)
    {
        var head = romFile.ReadBytes(CompositePreviewHeadSyntheticDescriptorTemplateOffset, 0x18).ToArray();
        var left = romFile.ReadBytes(CompositePreviewRightArmSyntheticDescriptorTemplateOffset, 0x18).ToArray();
        var right = romFile.ReadBytes(CompositePreviewLeftArmSyntheticDescriptorTemplateOffset, 0x18).ToArray();
        var legs = romFile.ReadBytes(CompositePreviewLegsSyntheticDescriptorTemplateOffset, 0x18).ToArray();
        head[0x10] = (byte)headRootDescriptorId;
        left[0x10] = (byte)leftArmRootDescriptorId;
        right[0x10] = (byte)rightArmRootDescriptorId;
        legs[0x10] = (byte)legsRootDescriptorId;
        if (mirrorSide)
        {
            FlipSyntheticDescriptorAnchorByte(head);
            FlipSyntheticDescriptorAnchorByte(left);
            FlipSyntheticDescriptorAnchorByte(right);
            FlipSyntheticDescriptorAnchorByte(legs);
        }

        return new Dictionary<int, byte[]>
        {
            [3] = head,
            [4] = left,
            [5] = right,
            [6] = legs
        };
    }

    private static void FlipSyntheticDescriptorAnchorByte(byte[] descriptorBytes)
    {
        var current = unchecked((sbyte)descriptorBytes[0x0D]);
        descriptorBytes[0x0D] = unchecked((byte)(-2 - current));
    }

    private static int MirrorCompositePreviewDescriptorId(int descriptorId)
    {
        return descriptorId switch
        {
            0x0E => 0x11,
            0x0F => 0x12,
            0x10 => 0x13,
            0x11 => 0x0E,
            0x12 => 0x0F,
            0x13 => 0x10,
            _ => descriptorId
        };
    }

    private IReadOnlyDictionary<int, CompositePreviewImageSourceOverride> BuildCompositePreviewImageSourceOverrides(
        RomFile romFile,
        PartDefinition rightArm,
        PartDefinition leftArm,
        int side)
    {
        if ((side & 1) == 0)
        {
            return new Dictionary<int, CompositePreviewImageSourceOverride>();
        }

        var overrides = new Dictionary<int, CompositePreviewImageSourceOverride>();
        AddCompositePreviewArmCopyPassImageSourceOverrides(
            overrides,
            ReadCompositePreviewAppearanceRow(romFile, PartKind.RightArm, rightArm.Id / 4).Entries,
            ReadCompositePreviewAppearanceRow(romFile, PartKind.LeftArm, rightArm.Id / 4).Entries);
        AddCompositePreviewArmCopyPassImageSourceOverrides(
            overrides,
            ReadCompositePreviewAppearanceRow(romFile, PartKind.LeftArm, leftArm.Id / 4).Entries,
            ReadCompositePreviewAppearanceRow(romFile, PartKind.RightArm, leftArm.Id / 4).Entries);
        return overrides;
    }

    private static void AddCompositePreviewArmCopyPassImageSourceOverrides(
        IDictionary<int, CompositePreviewImageSourceOverride> overrides,
        IReadOnlyList<uint> targetEntries,
        IReadOnlyList<uint> sourceEntries)
    {
        var count = Math.Min(targetEntries.Count, sourceEntries.Count);
        for (var index = 0; index < count; index++)
        {
            var targetEntry = targetEntries[index];
            var targetDescriptorId = (int)(targetEntry & 0x3F);
            if (targetDescriptorId == 0)
            {
                break;
            }

            var sourceEntry = sourceEntries[index];
            var sourceDescriptorId = (int)(sourceEntry & 0x3F);
            if (sourceDescriptorId == 0)
            {
                continue;
            }

            // CopyCompositePreviewDescriptorGraphics only runs for non-negative byte 1 on the
            // target row entry. It copies graphics from the opposite arm table at the same part
            // ordinal and leaves the target descriptor's template/palette attributes intact.
            var targetByte1Signed = unchecked((sbyte)((targetEntry >> 8) & 0xFF));
            if (targetByte1Signed >= 0)
            {
                overrides[targetDescriptorId] = new CompositePreviewImageSourceOverride(sourceDescriptorId, sourceEntry);
            }
        }
    }

    private int GetFirstAppearanceDescriptorId(RomFile romFile, PartDefinition part)
    {
        var (_, entries) = ReadCompositePreviewAppearanceRow(romFile, part.Kind, part.Id / 4);
        return (int)(entries[0] & 0x3F);
    }

    private static int FindLargeDisplayAppearanceEntryOffset(IReadOnlyList<uint> appearanceEntries, int rowOffset, int descriptorId)
    {
        for (var offset = 0; offset < appearanceEntries.Count; offset++)
        {
            var entryDescriptorId = (int)(appearanceEntries[offset] & 0x3F);
            if (entryDescriptorId == 0)
            {
                break;
            }

            if (entryDescriptorId == descriptorId)
            {
                return rowOffset + (offset * sizeof(uint));
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

    private static IEnumerable<int> GetVariantSelectorsForPartKind(PartKind kind)
    {
        if (kind is PartKind.RightArm or PartKind.LeftArm)
        {
            yield return 0;
            yield return 1;
            yield break;
        }

        yield return 0;
    }

    private static PartKind GetPartKindForAppearanceOffset(int appearanceEntryBaseOffset) => appearanceEntryBaseOffset switch
    {
        >= CompositePreviewHeadAppearanceTableOffset and < CompositePreviewRightArmAppearanceTableOffset => PartKind.Head,
        >= CompositePreviewRightArmAppearanceTableOffset and < CompositePreviewLeftArmAppearanceTableOffset => PartKind.RightArm,
        >= CompositePreviewLeftArmAppearanceTableOffset and < CompositePreviewLegsAppearanceTableOffset => PartKind.LeftArm,
        _ => PartKind.Legs
    };

    private static IEnumerable<LargePartDisplayDescriptorVariantResolution> BuildVariantResolutions(
        RomFile romFile,
        uint appearanceEntryRaw,
        int blobPointerTableOffset,
        IEnumerable<int> variantSelectors)
    {
        var selectorBase = (int)((appearanceEntryRaw >> 6) & 0xFF);
        var variantBit = (int)((appearanceEntryRaw >> 15) & 0x1);
        var signedByte1 = unchecked((sbyte)((appearanceEntryRaw >> 8) & 0xFF));

        foreach (var variantSelector in variantSelectors.Distinct())
        {
            var tableIndex = ResolveCompositePreviewTableIndex(appearanceEntryRaw, variantSelector);
            var imagePointerOffset = blobPointerTableOffset + (tableIndex * sizeof(uint));
            var imageOffset = TryReadOptionalPointer(romFile, imagePointerOffset, out var resolvedImageOffset)
                ? resolvedImageOffset
                : 0;
            var palettePointerOffset = imagePointerOffset + sizeof(uint);
            var paletteOffset = TryReadOptionalPointer(romFile, palettePointerOffset, out var resolvedPaletteOffset)
                ? resolvedPaletteOffset
                : 0;
            if (paletteOffset == imageOffset)
            {
                paletteOffset = 0;
                palettePointerOffset = 0;
            }

            yield return new LargePartDisplayDescriptorVariantResolution(
                variantSelector,
                appearanceEntryRaw,
                selectorBase,
                variantBit,
                signedByte1,
                tableIndex,
                imagePointerOffset,
                palettePointerOffset,
                imageOffset,
                paletteOffset,
                imageOffset > 0);
        }
    }

    private IEnumerable<int> CollectReachableDescriptorIds(RomFile romFile, int descriptorId, ISet<int> visited)
    {
        if (descriptorId <= 0 || !visited.Add(descriptorId))
        {
            yield break;
        }

        yield return descriptorId;

        var descriptorPointerOffset = CompositePreviewDescriptorPointerTableOffset + (descriptorId * sizeof(uint));
        var descriptorOffset = ReadRequiredPointer(romFile, descriptorPointerOffset);
        if (descriptorOffset + 0x18 > romFile.Length)
        {
            yield break;
        }

        var siblingDescriptorId = romFile.Data[descriptorOffset + 0x0F];
        var childDescriptorId = romFile.Data[descriptorOffset + 0x10];

        foreach (var child in CollectReachableDescriptorIds(romFile, childDescriptorId, visited))
        {
            yield return child;
        }

        foreach (var sibling in CollectReachableDescriptorIds(romFile, siblingDescriptorId, visited))
        {
            yield return sibling;
        }
    }

    private static (int Pa, int Pb, int Pc, int Pd) GetDefaultLargeDisplayTransformMatrix()
    {
        return (0x100, 0, 0, 0x100);
    }

    private readonly record struct CompositePreviewImageSourceOverride(int DescriptorId, uint AppearanceEntryRaw);

    private sealed class MutableLargePartDisplayPiece
    {
        public MutableLargePartDisplayPiece(int descriptorId, int appearanceEntryOffset, int recordOffset, byte[] descriptorRecordBytes, int imagePointerOffset, int palettePointerOffset, int imageOffset, int paletteOffset, byte[] paletteBytes, int paletteBank, int x, int y, byte siblingDescriptorId, byte childDescriptorId, byte rawWidth, byte rawHeight, byte sizeDivisors, int loadedTileCount, int tileWidth, int tileHeight, bool mirrorDisplayHorizontally, bool forceIndependentSource, int sortKey, byte[] pixelIndices)
        {
            DescriptorId = descriptorId;
            AppearanceEntryOffset = appearanceEntryOffset;
            RecordOffset = recordOffset;
            DescriptorRecordBytes = descriptorRecordBytes;
            ImagePointerOffset = imagePointerOffset;
            PalettePointerOffset = palettePointerOffset;
            ImageOffset = imageOffset;
            PaletteOffset = paletteOffset;
            PaletteBytes = paletteBytes;
            PaletteBank = paletteBank;
            X = x;
            Y = y;
            SiblingDescriptorId = siblingDescriptorId;
            ChildDescriptorId = childDescriptorId;
            RawWidth = rawWidth;
            RawHeight = rawHeight;
            SizeDivisors = sizeDivisors;
            LoadedTileCount = loadedTileCount;
            TileWidth = tileWidth;
            TileHeight = tileHeight;
            MirrorDisplayHorizontally = mirrorDisplayHorizontally;
            ForceIndependentSource = forceIndependentSource;
            SortKey = sortKey;
            PixelIndices = pixelIndices;
        }

        public int DescriptorId { get; }
        public int AppearanceEntryOffset { get; }
        public int RecordOffset { get; }
        public byte[] DescriptorRecordBytes { get; }
        public int ImagePointerOffset { get; }
        public int PalettePointerOffset { get; }
        public int ImageOffset { get; }
        public int PaletteOffset { get; }
        public byte[] PaletteBytes { get; }
        public int PaletteBank { get; }
        public int X { get; }
        public int Y { get; }
        public byte SiblingDescriptorId { get; }
        public byte ChildDescriptorId { get; }
        public byte RawWidth { get; }
        public byte RawHeight { get; }
        public byte SizeDivisors { get; }
        public int LoadedTileCount { get; }
        public int TileWidth { get; }
        public int TileHeight { get; }
        public bool MirrorDisplayHorizontally { get; set; }
        public bool ForceIndependentSource { get; set; }
        public int SortKey { get; }
        public byte[] PixelIndices { get; }

        public LargePartDisplayPieceAsset ToAsset()
        {
            var palette = PaletteBytes.Length == 0 ? new byte[PaletteSize] : PaletteBytes;
            return new LargePartDisplayPieceAsset(
                DescriptorId,
                AppearanceEntryOffset,
                RecordOffset,
                DescriptorRecordBytes.ToArray(),
                ImagePointerOffset,
                PalettePointerOffset,
                ImageOffset,
                PaletteOffset,
                palette,
                PaletteBank,
                X,
                Y,
                SiblingDescriptorId,
                ChildDescriptorId,
                RawWidth,
                RawHeight,
                SizeDivisors,
                LoadedTileCount,
                MirrorDisplayHorizontally,
                ForceIndependentSource,
                new IndexedImage(TileWidth, TileHeight, PixelIndices.ToArray(), palette));
        }
    }
}
