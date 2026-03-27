using Medabots.Rom.Maps;

namespace Medabots.Rom.WPFEditor.Models;

public sealed class MapOverlayRecordItem
{
    public required string Key { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public int TileX { get; init; }
    public int TileY { get; init; }
    public MapWarpRecord? Warp { get; init; }
    public MapEntitySpawnRecord? Spawn { get; init; }
}
