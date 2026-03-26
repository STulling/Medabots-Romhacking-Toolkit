using Medabots.Rom.Metadata;
using Medabots.Rom.Compression;

namespace Medabots.Rom.Images;

public sealed class ImageAssetRepository
{
    public const int PortraitPointerTableOffset = MedabotsRomSchema.PortraitPointerTableOffset;
    public const int PortraitPaletteTableOffset = MedabotsRomSchema.PortraitPaletteTableOffset;
    public const int PortraitsPerCharacter = MedabotsRomSchema.PortraitsPerCharacter;
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

    private static int ReadRequiredPointer(RomFile romFile, int pointerOffset)
    {
        if (!GbaPointer.TryReadFileOffset(romFile.Data, pointerOffset, out var fileOffset))
        {
            throw new InvalidDataException($"Invalid pointer at 0x{pointerOffset:X}.");
        }

        return fileOffset;
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
}
