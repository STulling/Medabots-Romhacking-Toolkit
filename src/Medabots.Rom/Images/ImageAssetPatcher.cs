using Medabots.Rom.Compression;

namespace Medabots.Rom.Images;

public sealed class ImageAssetPatcher
{
    public const int DefaultExpansionStartOffset = 0x800000;

    public void ApplyPortrait(RomHackSession session, PortraitAsset asset, int imageDumpOffset, int paletteDumpOffset)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(asset);

        var packed = TileImageCodec.Pack4BppTiles(asset.Image.PixelIndices);
        var compressed = WrapPortraitPayload(packed);
        session.RomFile.WriteBytes(imageDumpOffset, compressed);
        session.RomFile.WriteBytes(paletteDumpOffset, asset.Image.PaletteBytes);

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
        session.RomFile.WriteBytes(imageDumpOffset, compressed);
        session.RomFile.WriteBytes(paletteDumpOffset, asset.Image.PaletteBytes);

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
        var wrapped = WrapPortraitPayload(packed);
        var imageOffset = ResolveWriteOffset(session.RomFile, 0, wrapped.Length, expansionStartOffset, null);
        var paletteOffset = ResolveWriteOffset(session.RomFile, asset.PaletteOffset, asset.Image.PaletteBytes.Length, expansionStartOffset, null);
        ApplyPortrait(session, asset, imageOffset, paletteOffset);
    }

    public void ApplySpriteSmart(RomHackSession session, SpriteAsset asset, int expansionStartOffset = DefaultExpansionStartOffset)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(asset);

        var packed = TileImageCodec.Pack4BppTiles(asset.Image.PixelIndices);
        var compressed = GbaLz77.Compress(packed);
        var imageOffset = ResolveWriteOffset(session.RomFile, asset.ImageOffset, compressed.Length, expansionStartOffset, GbaLz77.TryGetEncodedLength);
        var paletteOffset = ResolveWriteOffset(session.RomFile, asset.PaletteOffset, asset.Image.PaletteBytes.Length, expansionStartOffset, null);
        ApplySprite(session, asset, imageOffset, paletteOffset);
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

    private static byte[] WrapPortraitPayload(ReadOnlySpan<byte> data)
    {
        var result = new List<byte>(6 + data.Length + (data.Length / 4) + 4)
        {
            (byte)'L',
            (byte)'e',
            0x00,
            0x08,
            0x00,
            0x00
        };

        var index = 0;
        while (index < data.Length)
        {
            result.Add(0xAA);
            for (var i = 0; i < 4; i++)
            {
                result.Add(index < data.Length ? data[index++] : (byte)0x00);
            }
        }

        return result.ToArray();
    }
}
