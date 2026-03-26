namespace Medabots.Rom.Images;

public sealed record IndexedImage(int TileWidth, int TileHeight, byte[] PixelIndices, byte[] PaletteBytes)
{
    public int Width => TileWidth * 8;
    public int Height => TileHeight * 8;
}
