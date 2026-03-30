namespace Medabots.Rom.Maps;

public sealed class MapCollisionPatch
{
    public MapCollisionPatch(int mapId, IEnumerable<byte>? colorAttributeBytes = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(mapId);
        MapId = mapId;
        ColorAttributeBytes = colorAttributeBytes?.ToArray() ?? [];
    }

    public int MapId { get; }

    public byte[] ColorAttributeBytes { get; }
}
