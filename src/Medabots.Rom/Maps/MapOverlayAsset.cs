namespace Medabots.Rom.Maps;

public sealed record MapOverlayAsset(
    int MapId,
    int WarpPointerOffset,
    int WarpDataOffset,
    int EntitySpawnPointerOffset,
    int EntitySpawnDataOffset,
    IReadOnlyList<MapWarpRecord> Warps,
    IReadOnlyList<MapEntitySpawnRecord> EntitySpawns);
