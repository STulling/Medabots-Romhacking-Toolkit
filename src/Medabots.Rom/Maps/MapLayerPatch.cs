namespace Medabots.Rom.Maps;

public sealed record MapLayerPatch(
    int MapId,
    int LayerIndex,
    ushort HeaderWidthInTiles,
    ushort HeaderHeightInTiles,
    ushort HeaderOriginX,
    ushort HeaderOriginY,
    ushort HeaderOriginX2,
    ushort HeaderOriginY2,
    ushort[] TileEntries);
