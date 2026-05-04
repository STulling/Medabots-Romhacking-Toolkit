using Medabots.Rom.Metadata;
using Medabots.Rom.Projects;

namespace Medabots.Rom.Maps;

internal sealed class MapSpawnProjectEditSystem : IProjectEditSystem
{
    private readonly MapOverlayPatcher _overlayPatcher;

    public MapSpawnProjectEditSystem(MapOverlayPatcher overlayPatcher) => _overlayPatcher = overlayPatcher;

    public string DisplayName => "Map Spawn";

    public IEnumerable<string> DescribeChanges(RomHackProject project) =>
        project.MapEntitySpawnPatches.Select(patch => $"Map {patch.MapId:D3} ({patch.Records.Count} records)");

    public IEnumerable<ProjectChange> BuildChanges(RomHackProject project, ProjectBuildContext context)
    {
        return project.MapEntitySpawnPatches
            .OrderBy(patch => patch.MapId)
            .Select(patch => new ProjectChange(DisplayName, $"Map {patch.MapId} spawn patch", _overlayPatcher.BuildEntitySpawnActions(context.SourceRom, patch, $"Apply map {patch.MapId} entity spawn patch", context.Allocator)))
            .ToArray();
    }
}

internal sealed class MapWarpProjectEditSystem : IProjectEditSystem
{
    private readonly MapOverlayPatcher _overlayPatcher;

    public MapWarpProjectEditSystem(MapOverlayPatcher overlayPatcher) => _overlayPatcher = overlayPatcher;

    public string DisplayName => "Map Warp";

    public IEnumerable<string> DescribeChanges(RomHackProject project) =>
        project.MapWarpPatches.Select(patch => $"Map {patch.MapId:D3} ({patch.Records.Count} records)");

    public IEnumerable<ProjectChange> BuildChanges(RomHackProject project, ProjectBuildContext context)
    {
        return project.MapWarpPatches
            .OrderBy(patch => patch.MapId)
            .Select(patch => new ProjectChange(DisplayName, $"Map {patch.MapId} warp patch", _overlayPatcher.BuildWarpActions(context.SourceRom, patch, $"Apply map {patch.MapId} warp patch", context.Allocator)))
            .ToArray();
    }
}

internal sealed class MapCollisionProjectEditSystem : IProjectEditSystem
{
    private readonly MapOverlayPatcher _overlayPatcher;

    public MapCollisionProjectEditSystem(MapOverlayPatcher overlayPatcher) => _overlayPatcher = overlayPatcher;

    public string DisplayName => "Map Collision";

    public IEnumerable<string> DescribeChanges(RomHackProject project) =>
        project.MapCollisionPatches.Select(patch => $"Map {patch.MapId:D3} ({patch.ColorAttributeBytes.Length} bytes)");

    public IEnumerable<ProjectChange> BuildChanges(RomHackProject project, ProjectBuildContext context)
    {
        return project.MapCollisionPatches
            .OrderBy(patch => patch.MapId)
            .Select(patch => new ProjectChange(DisplayName, $"Map {patch.MapId} collision patch", _overlayPatcher.BuildCollisionActions(context.SourceRom, patch, $"Apply map {patch.MapId} collision patch", context.Allocator)))
            .ToArray();
    }
}

internal sealed class MapEncounterStateProjectEditSystem : IProjectEditSystem
{
    public string DisplayName => "Map Encounter State";

    public IEnumerable<string> DescribeChanges(RomHackProject project) =>
        project.MapEncounterStatePatches.Select(patch => $"Map {patch.MapId:D3} -> {(patch.EncounterEnabledByte != 0 ? "enabled" : "disabled")}");

    public IEnumerable<ProjectChange> BuildChanges(RomHackProject project, ProjectBuildContext context)
    {
        return project.MapEncounterStatePatches
            .OrderBy(patch => patch.MapId)
            .Select(patch =>
            {
                var offset = MedabotsRomSchema.MapEncounterSettingsTableOffset + (patch.MapId * 8);
                return new ProjectChange(DisplayName, $"Map {patch.MapId} encounter state patch", [RomPatchAction.Create(offset, [patch.EncounterEnabledByte], $"Apply map {patch.MapId} encounter enable patch")]);
            })
            .ToArray();
    }
}

internal sealed class MapEncounterProjectEditSystem : IProjectEditSystem
{
    private readonly Encounters.EncounterTableReader _encounterTableReader;

    public MapEncounterProjectEditSystem(Encounters.EncounterTableReader encounterTableReader) => _encounterTableReader = encounterTableReader;

    public string DisplayName => "Map Encounter";

    public IEnumerable<string> DescribeChanges(RomHackProject project) =>
        project.MapEncounterPatches.Select(patch => $"Map {patch.MapId:D3} [{patch.Battle1:D3}, {patch.Battle2:D3}, {patch.Battle3:D3}, {patch.Battle4:D3}]");

    public IEnumerable<ProjectChange> BuildChanges(RomHackProject project, ProjectBuildContext context)
    {
        if (project.MapEncounterPatches.Count == 0)
        {
            return [];
        }

        var encounterTableOffset = _encounterTableReader.FindTableOffset(context.SourceRom);
        return project.MapEncounterPatches
            .OrderBy(patch => patch.MapId)
            .Select(patch =>
            {
                var offset = encounterTableOffset + (patch.MapId * Encounters.EncounterTableReader.EncounterSize);
                return new ProjectChange(DisplayName, $"Map {patch.MapId} encounter patch", [RomPatchAction.Create(offset, [patch.Battle1, patch.Battle2, patch.Battle3, patch.Battle4], $"Apply map {patch.MapId} encounter patch")]);
            })
            .ToArray();
    }
}

internal sealed class MapMusicProjectEditSystem : IProjectEditSystem
{
    public string DisplayName => "Map Music";

    public IEnumerable<string> DescribeChanges(RomHackProject project) =>
        project.MapMusicPatches.Select(patch => $"Map {patch.MapId:D3} -> {patch.MusicId}");

    public IEnumerable<ProjectChange> BuildChanges(RomHackProject project, ProjectBuildContext context)
    {
        return project.MapMusicPatches
            .OrderBy(patch => patch.MapId)
            .Select(patch =>
            {
                var offset = MedabotsRomSchema.MapMusicTableOffset + patch.MapId;
                return new ProjectChange(DisplayName, $"Map {patch.MapId} music patch", [RomPatchAction.Create(offset, [patch.MusicId], $"Apply map {patch.MapId} music patch")]);
            })
            .ToArray();
    }
}

internal sealed class MapSpriteSlotProjectEditSystem : IProjectEditSystem
{
    private readonly MapOverlayPatcher _overlayPatcher;

    public MapSpriteSlotProjectEditSystem(MapOverlayPatcher overlayPatcher) => _overlayPatcher = overlayPatcher;

    public string DisplayName => "Map Sprite Slot";

    public IEnumerable<string> DescribeChanges(RomHackProject project) =>
        project.MapEventObjectResourcePatches.Select(patch => $"Map {patch.MapId:D3} ({patch.ResourceIds.Count} slots)");

    public IEnumerable<ProjectChange> BuildChanges(RomHackProject project, ProjectBuildContext context)
    {
        return project.MapEventObjectResourcePatches
            .OrderBy(patch => patch.MapId)
            .Select(patch => new ProjectChange(DisplayName, $"Map {patch.MapId} sprite slot patch", _overlayPatcher.BuildEventObjectResourceActions(context.SourceRom, patch, $"Apply map {patch.MapId} sprite slot patch", context.Allocator)))
            .ToArray();
    }
}
