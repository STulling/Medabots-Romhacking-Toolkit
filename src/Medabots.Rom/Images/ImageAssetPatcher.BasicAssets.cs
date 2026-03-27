using Medabots.Rom.Compression;

namespace Medabots.Rom.Images;

public sealed partial class ImageAssetPatcher
{
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
        session.RomFile.EnsureCapacity(imageOffset + compressed.Length);

        var paletteExpansionStart = imageOffset == asset.ImageOffset ? expansionStartOffset : Align(imageOffset + compressed.Length, 4);
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
        session.RomFile.EnsureCapacity(imageOffset + compressed.Length);

        var paletteExpansionStart = imageOffset == asset.ImageOffset ? expansionStartOffset : Align(imageOffset + compressed.Length, 4);
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
}
