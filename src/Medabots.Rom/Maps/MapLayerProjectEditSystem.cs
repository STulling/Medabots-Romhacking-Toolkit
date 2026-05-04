using Medabots.Rom.Metadata;
using Medabots.Rom.Projects;

namespace Medabots.Rom.Maps;

internal sealed class MapLayerProjectEditSystem : IProjectEditSystem
{
    private readonly MapLayerPatcher _patcher;

    public MapLayerProjectEditSystem(MapLayerPatcher patcher)
    {
        _patcher = patcher;
    }

    public string DisplayName => "Map Layer";

    public IEnumerable<string> DescribeChanges(RomHackProject project) =>
        project.MapLayerPatches.Select(patch => $"Map {patch.MapId:D3}, Layer {patch.LayerIndex + 1} ({patch.TileEntries.Length} tiles)");

    public IEnumerable<ProjectChange> BuildChanges(RomHackProject project, ProjectBuildContext context)
    {
        return project.MapLayerPatches
            .OrderBy(patch => patch.MapId)
            .ThenBy(patch => patch.LayerIndex)
            .Select(patch => new ProjectChange(DisplayName, $"Map {patch.MapId} layer {patch.LayerIndex + 1} patch", _patcher.BuildRewriteActions(context.SourceRom, patch, $"Apply map {patch.MapId} layer {patch.LayerIndex + 1} patch", context.Allocator)))
            .ToArray();
    }
}
