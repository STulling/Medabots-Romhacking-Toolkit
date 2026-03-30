namespace Medabots.Rom.Maps;

public sealed class MapEncounterProjectEditor
{
    public void UpsertEncounterPatch(RomHackProject project, int mapId, byte battle1, byte battle2, byte battle3, byte battle4)
    {
        ArgumentNullException.ThrowIfNull(project);

        var patch = new MapEncounterPatch(mapId, battle1, battle2, battle3, battle4);
        var existing = project.MapEncounterPatches.FirstOrDefault(candidate => candidate.MapId == mapId);
        if (existing is null)
        {
            project.MapEncounterPatches.Add(patch);
            return;
        }

        var index = project.MapEncounterPatches.IndexOf(existing);
        project.MapEncounterPatches[index] = patch;
    }
}
