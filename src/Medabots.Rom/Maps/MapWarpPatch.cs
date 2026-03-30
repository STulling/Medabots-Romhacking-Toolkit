namespace Medabots.Rom.Maps;

public sealed class MapWarpPatch
{
    public MapWarpPatch(int mapId, IEnumerable<MapWarpRecord>? records = null, IEnumerable<int>? deletedOriginalIndices = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(mapId);
        MapId = mapId;

        if (records is not null)
        {
            foreach (var record in records)
            {
                Records.Add(record);
            }
        }

        if (deletedOriginalIndices is not null)
        {
            foreach (var index in deletedOriginalIndices.Distinct().OrderBy(index => index))
            {
                DeletedOriginalIndices.Add(index);
            }
        }
    }

    public int MapId { get; }

    public IList<MapWarpRecord> Records { get; } = new List<MapWarpRecord>();

    public IList<int> DeletedOriginalIndices { get; } = new List<int>();
}
