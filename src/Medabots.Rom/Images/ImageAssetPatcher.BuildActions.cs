using Medabots.Rom.Compression;
using Medabots.Rom.Projects;

namespace Medabots.Rom.Images;

public sealed partial class ImageAssetPatcher
{
    public IReadOnlyList<RomPatchAction> BuildSpriteSmartActions(RomFile romFile, SpriteAsset asset, FreeSpaceAllocator allocator, int expansionStartOffset = DefaultExpansionStartOffset)
    {
        ArgumentNullException.ThrowIfNull(romFile);
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(allocator);

        var packed = TileImageCodec.Pack4BppTiles(asset.Image.PixelIndices);
        var compressed = GbaLz77.Compress(packed);
        var imageOffset = ResolveWriteOffset(romFile, asset.ImageOffset, compressed.Length, Math.Max(expansionStartOffset, allocator.CurrentOffset), GbaLz77.TryGetEncodedLength);
        if (imageOffset != asset.ImageOffset)
        {
            allocator.EnsureAtLeast(imageOffset + compressed.Length);
        }

        var paletteExpansionStart = imageOffset == asset.ImageOffset ? Math.Max(expansionStartOffset, allocator.CurrentOffset) : FreeSpaceAllocator.AlignUp(imageOffset + compressed.Length, 4);
        var paletteOffset = ResolveWriteOffset(romFile, asset.PaletteOffset, asset.Image.PaletteBytes.Length, paletteExpansionStart, null);
        if (paletteOffset != asset.PaletteOffset)
        {
            allocator.EnsureAtLeast(paletteOffset + asset.Image.PaletteBytes.Length);
        }

        Span<byte> pointer = stackalloc byte[4];
        BitConverter.TryWriteBytes(pointer, GbaPointer.ToRomAddress(imageOffset));
        var imagePointer = pointer.ToArray();
        BitConverter.TryWriteBytes(pointer, GbaPointer.ToRomAddress(paletteOffset));
        var palettePointer = pointer.ToArray();
        return
        [
            RomPatchAction.Create(imageOffset, compressed, $"Write sprite {asset.SpriteId} image"),
            RomPatchAction.Create(paletteOffset, asset.Image.PaletteBytes, $"Write sprite {asset.SpriteId} palette"),
            RomPatchAction.Create(asset.ImagePointerOffset, imagePointer, $"Repoint sprite {asset.SpriteId} image"),
            RomPatchAction.Create(asset.PalettePointerOffset, palettePointer, $"Repoint sprite {asset.SpriteId} palette")
        ];
    }

    public IReadOnlyList<RomPatchAction> BuildPortraitSmartActions(RomFile romFile, PortraitAsset asset, FreeSpaceAllocator allocator, int expansionStartOffset = DefaultExpansionStartOffset)
    {
        ArgumentNullException.ThrowIfNull(romFile);
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(allocator);

        var packed = TileImageCodec.Pack4BppTiles(asset.Image.PixelIndices);
        var compressed = Malias2.Compress(packed);
        var imageOffset = ResolveWriteOffset(romFile, asset.ImageOffset, compressed.Length, Math.Max(expansionStartOffset, allocator.CurrentOffset), Malias2.TryGetEncodedLength);
        if (imageOffset != asset.ImageOffset)
        {
            allocator.EnsureAtLeast(imageOffset + compressed.Length);
        }

        var paletteExpansionStart = imageOffset == asset.ImageOffset ? Math.Max(expansionStartOffset, allocator.CurrentOffset) : FreeSpaceAllocator.AlignUp(imageOffset + compressed.Length, 4);
        var paletteOffset = ResolveWriteOffset(romFile, asset.PaletteOffset, asset.Image.PaletteBytes.Length, paletteExpansionStart, null);
        if (paletteOffset != asset.PaletteOffset)
        {
            allocator.EnsureAtLeast(paletteOffset + asset.Image.PaletteBytes.Length);
        }

        Span<byte> pointer = stackalloc byte[4];
        BitConverter.TryWriteBytes(pointer, GbaPointer.ToRomAddress(imageOffset));
        var imagePointer = pointer.ToArray();
        BitConverter.TryWriteBytes(pointer, GbaPointer.ToRomAddress(paletteOffset));
        var palettePointer = pointer.ToArray();
        return
        [
            RomPatchAction.Create(imageOffset, compressed, $"Write portrait {asset.CharacterId}:{asset.PortraitIndex} image"),
            RomPatchAction.Create(paletteOffset, asset.Image.PaletteBytes, $"Write portrait {asset.CharacterId} palette"),
            RomPatchAction.Create(asset.ImagePointerOffset, imagePointer, $"Repoint portrait {asset.CharacterId}:{asset.PortraitIndex} image"),
            RomPatchAction.Create(asset.PalettePointerOffset, palettePointer, $"Repoint portrait {asset.CharacterId} palette")
        ];
    }

    public IReadOnlyList<RomPatchAction> BuildBattleCompositeSpriteComponentSmartActions(RomFile romFile, BattleCompositeSpriteComponentAsset asset, FreeSpaceAllocator allocator, int expansionStartOffset = DefaultExpansionStartOffset)
    {
        ArgumentNullException.ThrowIfNull(romFile);
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(allocator);

        var packed = TileImageCodec.Pack4BppTiles(asset.Image.PixelIndices);
        var compressed = Malias2.Compress(packed);
        var imageOffset = ResolveWriteOffset(romFile, asset.ImageOffset, compressed.Length, Math.Max(expansionStartOffset, allocator.CurrentOffset), Malias2.TryGetEncodedLength);
        if (imageOffset != asset.ImageOffset)
        {
            allocator.EnsureAtLeast(imageOffset + compressed.Length);
        }

        Span<byte> pointer = stackalloc byte[4];
        BitConverter.TryWriteBytes(pointer, GbaPointer.ToRomAddress(imageOffset));
        var imagePointer = pointer.ToArray();
        return
        [
            RomPatchAction.Create(imageOffset, compressed, $"Write Medabot component {asset.MedabotId}:{asset.ComponentIndex} image"),
            RomPatchAction.Create(asset.PalettePointerOffset, [asset.PaletteFamily], $"Set Medabot component palette family {asset.MedabotId}:{asset.ComponentIndex}"),
            RomPatchAction.Create(asset.ImagePointerOffset, imagePointer, $"Repoint Medabot component {asset.MedabotId}:{asset.ComponentIndex} image")
        ];
    }

    public IReadOnlyList<RomPatchAction> BuildLargePartDisplaySmartActions(RomFile romFile, LargePartDisplayAsset asset, FreeSpaceAllocator allocator, int expansionStartOffset = DefaultExpansionStartOffset)
    {
        ArgumentNullException.ThrowIfNull(romFile);
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(allocator);

        var actions = new List<RomPatchAction>();
        allocator.EnsureAtLeast(expansionStartOffset);
        Span<byte> pointer = stackalloc byte[4];
        foreach (var piece in asset.Pieces)
        {
            if (piece.RecordOffset > 0 && piece.DescriptorRecordBytes.Length != 0)
            {
                actions.Add(RomPatchAction.Create(piece.RecordOffset, piece.DescriptorRecordBytes, $"Write large part display {asset.PartId} variant {asset.VariantSelector} descriptor {piece.DescriptorId} record"));
            }

            if (piece.ImagePointerOffset <= 0)
            {
                continue;
            }

            var packed = TileImageCodec.Pack4BppTiles(piece.Image.PixelIndices);
            var compressed = GbaLz77.Compress(packed);
            var imageOffset = FreeSpaceAllocator.AlignUp(Math.Max(allocator.CurrentOffset, expansionStartOffset), 4);
            allocator.EnsureAtLeast(imageOffset + compressed.Length);

            actions.Add(RomPatchAction.Create(imageOffset, compressed, $"Write large part display {asset.PartId} variant {asset.VariantSelector} descriptor {piece.DescriptorId} image"));
            BitConverter.TryWriteBytes(pointer, GbaPointer.ToRomAddress(imageOffset));
            actions.Add(RomPatchAction.Create(piece.ImagePointerOffset, pointer.ToArray(), $"Repoint large part display {asset.PartId} variant {asset.VariantSelector} descriptor {piece.DescriptorId} image"));
        }

        var paletteWrites = new Dictionary<int, (int CurrentOffset, byte[] PaletteBytes, int DescriptorId)>();
        foreach (var piece in asset.Pieces)
        {
            if (piece.PalettePointerOffset <= 0 || piece.PaletteBytes.Length == 0)
            {
                continue;
            }

            paletteWrites[piece.PalettePointerOffset] = (piece.PaletteOffset, piece.PaletteBytes, piece.DescriptorId);
        }

        foreach (var paletteWrite in paletteWrites)
        {
            var paletteOffset = FreeSpaceAllocator.AlignUp(Math.Max(allocator.CurrentOffset, expansionStartOffset), 4);
            allocator.EnsureAtLeast(paletteOffset + paletteWrite.Value.PaletteBytes.Length);

            actions.Add(RomPatchAction.Create(paletteOffset, paletteWrite.Value.PaletteBytes, $"Write large part display {asset.PartId} variant {asset.VariantSelector} descriptor {paletteWrite.Value.DescriptorId} palette"));
            BitConverter.TryWriteBytes(pointer, GbaPointer.ToRomAddress(paletteOffset));
            actions.Add(RomPatchAction.Create(paletteWrite.Key, pointer.ToArray(), $"Repoint large part display {asset.PartId} variant {asset.VariantSelector} descriptor {paletteWrite.Value.DescriptorId} palette"));
        }

        return actions;
    }
}
