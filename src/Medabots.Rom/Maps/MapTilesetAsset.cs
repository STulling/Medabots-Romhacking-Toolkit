using Medabots.Rom.Images;

namespace Medabots.Rom.Maps;

public sealed record MapTilesetAsset(
    int MapId,
    string Name,
    int WidthInTiles,
    int HeightInTiles,
    int GraphicsPointerOffset,
    int GraphicsDataOffset,
    int PalettePointerOffset,
    int PaletteDataOffset,
    int ColorAttributePointerOffset,
    int ColorAttributeDataOffset,
    byte[] PaletteBytes,
    byte[]? ColorAttributeBytes,
    IndexedImage TilesetSheet,
    IReadOnlyList<MapLayerAsset> Layers)
{
    public int WidthInMetaTiles => WidthInTiles / 2;
    public int HeightInMetaTiles => HeightInTiles / 2;
}
