namespace Medabots.Rom.Maps;

public sealed class MapEncounterStateProjectEditor
{
    public void UpsertEncounterStatePatch(RomHackProject project, int mapId, byte encounterEnabledByte)
    {
        ArgumentNullException.ThrowIfNull(project);

        var patch = new MapEncounterStatePatch(mapId, encounterEnabledByte);
        var existing = project.MapEncounterStatePatches.FirstOrDefault(candidate => candidate.MapId == mapId);
        if (existing is null)
        {
            project.MapEncounterStatePatches.Add(patch);
            return;
        }

        var index = project.MapEncounterStatePatches.IndexOf(existing);
        project.MapEncounterStatePatches[index] = patch;
    }
}
