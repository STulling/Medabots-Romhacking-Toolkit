using Medabots.Rom.Projects;

namespace Medabots.Rom.Maps;

public sealed class MapLayerProjectEditor
{
    public MapLayerPatch? StageLayer(RomHackProject project, int mapId, MapLayerAsset sourceLayer, ushort[] tileEntries)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(sourceLayer);
        ArgumentNullException.ThrowIfNull(tileEntries);

        var normalizedEntries = tileEntries.ToArray();
        var sourceEntries = sourceLayer.TileEntries;
        var matchesSource =
            sourceEntries.Length == normalizedEntries.Length &&
            sourceEntries.SequenceEqual(normalizedEntries);

        if (matchesSource)
        {
            ProjectEditCollection.Remove(project, ProjectEditAdapters.MapLayer, (mapId, sourceLayer.LayerIndex));
            return null;
        }

        var patch = new MapLayerPatch(
            mapId,
            sourceLayer.LayerIndex,
            sourceLayer.HeaderWidthInTiles,
            sourceLayer.HeaderHeightInTiles,
            sourceLayer.HeaderOriginX,
            sourceLayer.HeaderOriginY,
            sourceLayer.HeaderOriginX2,
            sourceLayer.HeaderOriginY2,
            normalizedEntries);
        ProjectEditCollection.Upsert(project, ProjectEditAdapters.MapLayer, patch);
        return patch;
    }
}
