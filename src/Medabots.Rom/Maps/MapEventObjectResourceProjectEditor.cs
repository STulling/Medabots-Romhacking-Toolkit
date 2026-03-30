namespace Medabots.Rom.Maps;

public sealed class MapEventObjectResourceProjectEditor
{
    public void UpsertResourcePatch(RomHackProject project, int mapId, IEnumerable<byte> resourceIds)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(resourceIds);

        var normalized = resourceIds.Take(16).ToArray();
        var patch = new MapEventObjectResourcePatch(mapId, normalized);
        var existing = project.MapEventObjectResourcePatches.FirstOrDefault(candidate => candidate.MapId == mapId);
        if (existing is null)
        {
            project.MapEventObjectResourcePatches.Add(patch);
            return;
        }

        var index = project.MapEventObjectResourcePatches.IndexOf(existing);
        project.MapEventObjectResourcePatches[index] = patch;
    }
}
