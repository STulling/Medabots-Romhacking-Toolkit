namespace Medabots.Rom.Images;

public static class OverworldSpriteFrameExtractor
{
    public const int FacingFrameGroupCount = 12;

    public static int GetFacingBaseFrameIndex(int facingVariant) => facingVariant switch
    {
        0 => 0,
        1 => 3,
        2 => 6,
        3 => 9,
        _ => 0
    };

    public static IndexedImage ExtractFacingFrame(IndexedImage source, int facingVariant)
    {
        ArgumentNullException.ThrowIfNull(source);

        var baseFrameIndex = GetFacingBaseFrameIndex(facingVariant);
        var frameTileHeight = Math.Max(1, source.TileHeight / FacingFrameGroupCount);
        if ((baseFrameIndex + 1) * frameTileHeight > source.TileHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(facingVariant));
        }

        return ExtractFrame(source, baseFrameIndex, frameTileHeight);
    }

    public static IndexedImage ExtractFrame(IndexedImage source, int frameIndex, int frameTileHeight)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegative(frameIndex);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frameTileHeight);

        var tileWidth = source.TileWidth;
        var tileHeight = frameTileHeight;
        var pixels = new byte[tileWidth * tileHeight * 64];
        var frameStartTileY = frameIndex * frameTileHeight;
        if ((frameStartTileY + frameTileHeight) > source.TileHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(frameIndex));
        }

        var destination = new IndexedImage(tileWidth, tileHeight, pixels, source.PaletteBytes);

        for (var y = 0; y < tileHeight * 8; y++)
        {
            for (var x = 0; x < tileWidth * 8; x++)
            {
                var sourceIndex = GetTileOrderedPixelIndex(source, x, y + frameStartTileY * 8);
                var destinationIndex = GetTileOrderedPixelIndex(destination, x, y);
                pixels[destinationIndex] = source.PixelIndices[sourceIndex];
            }
        }

        return destination;
    }

    private static int GetTileOrderedPixelIndex(IndexedImage image, int pixelX, int pixelY)
    {
        var tileX = pixelX / 8;
        var tileY = pixelY / 8;
        var localX = pixelX % 8;
        var localY = pixelY % 8;
        return (((tileY * image.TileWidth) + tileX) * 64) + (localY * 8) + localX;
    }
}
