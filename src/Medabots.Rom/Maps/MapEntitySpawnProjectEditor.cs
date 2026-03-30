namespace Medabots.Rom.Maps;

public sealed class MapEntitySpawnProjectEditor
{
    public MapEntitySpawnPatch AddEntitySpawnRecord(RomHackProject project, MapOverlayAsset overlay, MapEntitySpawnRecord record)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(overlay);

        var patch = GetOrCreatePatch(project, overlay);
        if (patch.DeletedOriginalIndices.Count > 0)
        {
            var reusedIndex = patch.DeletedOriginalIndices.Min();
            patch.DeletedOriginalIndices.Remove(reusedIndex);
            var insertIndex = Math.Clamp(reusedIndex, 0, patch.Records.Count);
            patch.Records.Insert(insertIndex, record);
            return patch;
        }

        patch.Records.Add(record);
        return patch;
    }

    public MapEntitySpawnPatch DeleteExistingEntitySpawnRecord(RomHackProject project, MapOverlayAsset overlay, int originalIndex)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(overlay);
        ArgumentOutOfRangeException.ThrowIfNegative(originalIndex);

        var patch = GetOrCreatePatch(project, overlay);
        if (originalIndex >= patch.Records.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(originalIndex));
        }

        patch.Records.RemoveAt(originalIndex);
        if (!patch.DeletedOriginalIndices.Contains(originalIndex))
        {
            patch.DeletedOriginalIndices.Add(originalIndex);
        }

        NormalizeDeletedIndices(patch);
        return patch;
    }

    public MapEntitySpawnPatch UpsertEntitySpawnPatch(RomHackProject project, int mapId, IEnumerable<MapEntitySpawnRecord> records, IEnumerable<int>? deletedOriginalIndices = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentOutOfRangeException.ThrowIfNegative(mapId);

        var existing = project.MapEntitySpawnPatches.FirstOrDefault(patch => patch.MapId == mapId);
        if (existing is not null)
        {
            project.MapEntitySpawnPatches.Remove(existing);
        }

        var replacement = new MapEntitySpawnPatch(mapId, records, deletedOriginalIndices);
        project.MapEntitySpawnPatches.Add(replacement);
        return replacement;
    }

    private MapEntitySpawnPatch GetOrCreatePatch(RomHackProject project, MapOverlayAsset overlay)
    {
        var existing = project.MapEntitySpawnPatches.FirstOrDefault(patch => patch.MapId == overlay.MapId);
        if (existing is not null)
        {
            return existing;
        }

        var created = new MapEntitySpawnPatch(overlay.MapId, overlay.EntitySpawns);
        project.MapEntitySpawnPatches.Add(created);
        return created;
    }

    private static void NormalizeDeletedIndices(MapEntitySpawnPatch patch)
    {
        var normalized = patch.DeletedOriginalIndices.Distinct().OrderBy(index => index).ToArray();
        patch.DeletedOriginalIndices.Clear();
        foreach (var index in normalized)
        {
            patch.DeletedOriginalIndices.Add(index);
        }
    }
}
