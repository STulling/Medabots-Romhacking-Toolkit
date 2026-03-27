using Medabots.Rom.Images;

namespace Medabots.Rom.Maps;

public sealed record MapLayerAsset(
    int LayerIndex,
    int PointerOffset,
    int DataOffset,
    ushort HeaderWidthInTiles,
    ushort HeaderHeightInTiles,
    ushort HeaderOriginX,
    ushort HeaderOriginY,
    ushort HeaderOriginX2,
    ushort HeaderOriginY2,
    ushort[] TileEntries,
    IndexedImage Image);
