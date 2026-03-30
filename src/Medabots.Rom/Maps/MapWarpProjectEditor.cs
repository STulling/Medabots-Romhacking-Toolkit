namespace Medabots.Rom.Maps;

public sealed class MapWarpProjectEditor
{
    public MapWarpPatch AddWarpRecord(RomHackProject project, MapOverlayAsset overlay, MapWarpRecord record)
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

    public MapWarpPatch DeleteExistingWarpRecord(RomHackProject project, MapOverlayAsset overlay, int originalIndex)
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

    public MapWarpPatch UpsertWarpPatch(RomHackProject project, int mapId, IEnumerable<MapWarpRecord> records, IEnumerable<int>? deletedOriginalIndices = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentOutOfRangeException.ThrowIfNegative(mapId);

        var existing = project.MapWarpPatches.FirstOrDefault(patch => patch.MapId == mapId);
        if (existing is not null)
        {
            project.MapWarpPatches.Remove(existing);
        }

        var replacement = new MapWarpPatch(mapId, records, deletedOriginalIndices);
        project.MapWarpPatches.Add(replacement);
        return replacement;
    }

    private MapWarpPatch GetOrCreatePatch(RomHackProject project, MapOverlayAsset overlay)
    {
        var existing = project.MapWarpPatches.FirstOrDefault(patch => patch.MapId == overlay.MapId);
        if (existing is not null)
        {
            return existing;
        }

        var created = new MapWarpPatch(overlay.MapId, overlay.Warps);
        project.MapWarpPatches.Add(created);
        return created;
    }

    private static void NormalizeDeletedIndices(MapWarpPatch patch)
    {
        var normalized = patch.DeletedOriginalIndices.Distinct().OrderBy(index => index).ToArray();
        patch.DeletedOriginalIndices.Clear();
        foreach (var index in normalized)
        {
            patch.DeletedOriginalIndices.Add(index);
        }
    }
}
