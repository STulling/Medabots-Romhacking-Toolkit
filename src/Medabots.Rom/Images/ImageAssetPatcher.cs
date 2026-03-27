using Medabots.Rom.Compression;

namespace Medabots.Rom.Images;

public sealed class ImageAssetPatcher
{
    public const int DefaultExpansionStartOffset = 0x800000;

    public void ApplyBattleCompositeSpriteComponent(RomHackSession session, BattleCompositeSpriteComponentAsset asset, int imageDumpOffset)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(asset);

        var packed = TileImageCodec.Pack4BppTiles(asset.Image.PixelIndices);
        var compressed = Malias2.Compress(packed);
        session.ApplyPatch(RomPatchAction.Create(imageDumpOffset, compressed, $"Write Medabot component {asset.MedabotId}:{asset.ComponentIndex} image"));
        session.ApplyPatch(RomPatchAction.Create(asset.PalettePointerOffset, [asset.PaletteFamily], $"Set Medabot component palette family {asset.MedabotId}:{asset.ComponentIndex}"));

        Span<byte> pointer = stackalloc byte[4];
        BitConverter.TryWriteBytes(pointer, GbaPointer.ToRomAddress(imageDumpOffset));
        session.ApplyPatch(RomPatchAction.Create(asset.ImagePointerOffset, pointer, $"Repoint Medabot component {asset.MedabotId}:{asset.ComponentIndex} image"));
    }

    public void ApplyPortrait(RomHackSession session, PortraitAsset asset, int imageDumpOffset, int paletteDumpOffset)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(asset);

        var packed = TileImageCodec.Pack4BppTiles(asset.Image.PixelIndices);
        var compressed = Malias2.Compress(packed);
        session.ApplyPatch(RomPatchAction.Create(imageDumpOffset, compressed, $"Write portrait {asset.CharacterId}:{asset.PortraitIndex} image"));
        session.ApplyPatch(RomPatchAction.Create(paletteDumpOffset, asset.Image.PaletteBytes, $"Write portrait {asset.CharacterId} palette"));

        Span<byte> pointer = stackalloc byte[4];
        BitConverter.TryWriteBytes(pointer, GbaPointer.ToRomAddress(imageDumpOffset));
        session.ApplyPatch(RomPatchAction.Create(asset.ImagePointerOffset, pointer, $"Repoint portrait {asset.CharacterId}:{asset.PortraitIndex} image"));
        BitConverter.TryWriteBytes(pointer, GbaPointer.ToRomAddress(paletteDumpOffset));
        session.ApplyPatch(RomPatchAction.Create(asset.PalettePointerOffset, pointer, $"Repoint portrait {asset.CharacterId} palette"));
    }

    public void ApplySprite(RomHackSession session, SpriteAsset asset, int imageDumpOffset, int paletteDumpOffset)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(asset);

        var packed = TileImageCodec.Pack4BppTiles(asset.Image.PixelIndices);
        var compressed = GbaLz77.Compress(packed);
        session.ApplyPatch(RomPatchAction.Create(imageDumpOffset, compressed, $"Write sprite {asset.SpriteId} image"));
        session.ApplyPatch(RomPatchAction.Create(paletteDumpOffset, asset.Image.PaletteBytes, $"Write sprite {asset.SpriteId} palette"));

        Span<byte> pointer = stackalloc byte[4];
        BitConverter.TryWriteBytes(pointer, GbaPointer.ToRomAddress(imageDumpOffset));
        session.ApplyPatch(RomPatchAction.Create(asset.ImagePointerOffset, pointer, $"Repoint sprite {asset.SpriteId} image"));
        BitConverter.TryWriteBytes(pointer, GbaPointer.ToRomAddress(paletteDumpOffset));
        session.ApplyPatch(RomPatchAction.Create(asset.PalettePointerOffset, pointer, $"Repoint sprite {asset.SpriteId} palette"));
    }

    public void ApplyPortraitSmart(RomHackSession session, PortraitAsset asset, int expansionStartOffset = DefaultExpansionStartOffset)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(asset);

        var packed = TileImageCodec.Pack4BppTiles(asset.Image.PixelIndices);
        var compressed = Malias2.Compress(packed);
        var imageOffset = ResolveWriteOffset(session.RomFile, asset.ImageOffset, compressed.Length, expansionStartOffset, Malias2.TryGetEncodedLength);

        // Reserve any newly allocated image space before choosing the palette destination
        // so both writes cannot collide in the expansion region.
        session.RomFile.EnsureCapacity(imageOffset + compressed.Length);

        var paletteExpansionStart = imageOffset == asset.ImageOffset
            ? expansionStartOffset
            : Align(imageOffset + compressed.Length, 4);
        var paletteOffset = ResolveWriteOffset(session.RomFile, asset.PaletteOffset, asset.Image.PaletteBytes.Length, paletteExpansionStart, null);
        ApplyPortrait(session, asset, imageOffset, paletteOffset);
    }

    public void ApplySpriteSmart(RomHackSession session, SpriteAsset asset, int expansionStartOffset = DefaultExpansionStartOffset)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(asset);

        var packed = TileImageCodec.Pack4BppTiles(asset.Image.PixelIndices);
        var compressed = GbaLz77.Compress(packed);
        var imageOffset = ResolveWriteOffset(session.RomFile, asset.ImageOffset, compressed.Length, expansionStartOffset, GbaLz77.TryGetEncodedLength);

        // Reserve any newly allocated image space before choosing the palette destination
        // so both writes cannot collide in the expansion region.
        session.RomFile.EnsureCapacity(imageOffset + compressed.Length);

        var paletteExpansionStart = imageOffset == asset.ImageOffset
            ? expansionStartOffset
            : Align(imageOffset + compressed.Length, 4);
        var paletteOffset = ResolveWriteOffset(session.RomFile, asset.PaletteOffset, asset.Image.PaletteBytes.Length, paletteExpansionStart, null);
        ApplySprite(session, asset, imageOffset, paletteOffset);
    }

    public void ApplyBattleCompositeSpriteComponentSmart(RomHackSession session, BattleCompositeSpriteComponentAsset asset, int expansionStartOffset = DefaultExpansionStartOffset)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(asset);

        var packed = TileImageCodec.Pack4BppTiles(asset.Image.PixelIndices);
        var compressed = Malias2.Compress(packed);
        var imageOffset = ResolveWriteOffset(session.RomFile, asset.ImageOffset, compressed.Length, expansionStartOffset, Malias2.TryGetEncodedLength);
        ApplyBattleCompositeSpriteComponent(session, asset, imageOffset);
    }

    public void ApplyLargePartDisplaySmart(RomHackSession session, LargePartDisplayAsset asset, int expansionStartOffset = DefaultExpansionStartOffset)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(asset);

        var nextExpansionOffset = expansionStartOffset;
        Span<byte> pointer = stackalloc byte[4];
        foreach (var piece in asset.Pieces)
        {
            if (piece.ImagePointerOffset <= 0)
            {
                continue;
            }

            var packed = TileImageCodec.Pack4BppTiles(piece.Image.PixelIndices);
            var compressed = GbaLz77.Compress(packed);
            var imageOffset = ResolveWriteOffset(session.RomFile, piece.ImageOffset, compressed.Length, nextExpansionOffset, GbaLz77.TryGetEncodedLength);
            nextExpansionOffset = imageOffset == piece.ImageOffset
                ? nextExpansionOffset
                : Align(imageOffset + compressed.Length, 4);

            session.ApplyPatch(RomPatchAction.Create(imageOffset, compressed, $"Write large part display {asset.PartId} variant {asset.VariantSelector} descriptor {piece.DescriptorId} image"));
            BitConverter.TryWriteBytes(pointer, GbaPointer.ToRomAddress(imageOffset));
            session.ApplyPatch(RomPatchAction.Create(piece.ImagePointerOffset, pointer, $"Repoint large part display {asset.PartId} variant {asset.VariantSelector} descriptor {piece.DescriptorId} image"));
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
            var paletteOffset = ResolveWriteOffset(session.RomFile, paletteWrite.Value.CurrentOffset, paletteWrite.Value.PaletteBytes.Length, nextExpansionOffset, null);
            nextExpansionOffset = paletteOffset == paletteWrite.Value.CurrentOffset
                ? nextExpansionOffset
                : Align(paletteOffset + paletteWrite.Value.PaletteBytes.Length, 4);

            session.ApplyPatch(RomPatchAction.Create(paletteOffset, paletteWrite.Value.PaletteBytes, $"Write large part display {asset.PartId} variant {asset.VariantSelector} descriptor {paletteWrite.Value.DescriptorId} palette"));
            BitConverter.TryWriteBytes(pointer, GbaPointer.ToRomAddress(paletteOffset));
            session.ApplyPatch(RomPatchAction.Create(paletteWrite.Key, pointer, $"Repoint large part display {asset.PartId} variant {asset.VariantSelector} descriptor {paletteWrite.Value.DescriptorId} palette"));
        }
    }

    private static int ResolveWriteOffset(RomFile romFile, int currentOffset, int newLength, int expansionStartOffset, Func<byte[], int, int?>? encodedLengthReader)
    {
        if (currentOffset > 0)
        {
            var existingLength = encodedLengthReader is null
                ? newLength
                : encodedLengthReader(romFile.Data, currentOffset);
            if (existingLength is int length && newLength <= length)
            {
                return currentOffset;
            }
        }

        var baseOffset = Math.Max(expansionStartOffset, romFile.Length);
        return Align(baseOffset, 4);
    }

    private static int Align(int value, int alignment)
    {
        var mask = alignment - 1;
        return (value + mask) & ~mask;
    }
}
