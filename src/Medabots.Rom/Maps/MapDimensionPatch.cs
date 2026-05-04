namespace Medabots.Rom.Maps;

public sealed record MapDimensionPatch(
    int MapId,
    byte WidthInTiles,
    byte HeightInTiles);
