namespace Medabots.Rom.Maps;

public sealed class MapMusicProjectEditor
{
    public void UpsertMusicPatch(RomHackProject project, int mapId, byte musicId)
    {
        ArgumentNullException.ThrowIfNull(project);

        var patch = new MapMusicPatch(mapId, musicId);
        var existing = project.MapMusicPatches.FirstOrDefault(candidate => candidate.MapId == mapId);
        if (existing is null)
        {
            project.MapMusicPatches.Add(patch);
            return;
        }

        var index = project.MapMusicPatches.IndexOf(existing);
        project.MapMusicPatches[index] = patch;
    }
}
