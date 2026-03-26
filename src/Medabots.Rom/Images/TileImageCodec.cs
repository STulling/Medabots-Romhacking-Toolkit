namespace Medabots.Rom.Images;

public static class TileImageCodec
{
    public static byte[] Split4BppTiles(ReadOnlySpan<byte> packedData)
    {
        var result = new byte[packedData.Length * 2];
        var target = 0;
        foreach (var value in packedData)
        {
            result[target++] = (byte)(value & 0x0F);
            result[target++] = (byte)((value >> 4) & 0x0F);
        }

        return result;
    }

    public static byte[] Pack4BppTiles(ReadOnlySpan<byte> unpackedData)
    {
        if (unpackedData.Length % 2 != 0)
        {
            throw new ArgumentException("4bpp unpacked tile data must contain an even number of pixels.", nameof(unpackedData));
        }

        var result = new byte[unpackedData.Length / 2];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = (byte)((unpackedData[i * 2] & 0x0F) | ((unpackedData[(i * 2) + 1] & 0x0F) << 4));
        }

        return result;
    }

    public static byte[] ConvertBgr555PaletteToRgb888(ReadOnlySpan<byte> paletteBytes)
    {
        if (paletteBytes.Length % 2 != 0)
        {
            throw new ArgumentException("Palette byte count must be even.", nameof(paletteBytes));
        }

        var result = new byte[(paletteBytes.Length / 2) * 3];
        for (var i = 0; i < paletteBytes.Length / 2; i++)
        {
            var color = (ushort)(paletteBytes[i * 2] | (paletteBytes[(i * 2) + 1] << 8));
            var red = (byte)(((color >> 10) & 0x1F) << 3);
            var green = (byte)(((color >> 5) & 0x1F) << 3);
            var blue = (byte)((color & 0x1F) << 3);

            result[i * 3] = red;
            result[(i * 3) + 1] = green;
            result[(i * 3) + 2] = blue;
        }

        return result;
    }
}
