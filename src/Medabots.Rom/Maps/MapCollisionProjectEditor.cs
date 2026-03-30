namespace Medabots.Rom.Maps;

public sealed class MapCollisionProjectEditor
{
    public MapCollisionPatch UpsertCollisionPatch(RomHackProject project, int mapId, IEnumerable<byte> colorAttributeBytes)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(colorAttributeBytes);

        var existing = project.MapCollisionPatches.FirstOrDefault(patch => patch.MapId == mapId);
        if (existing is not null)
        {
            project.MapCollisionPatches.Remove(existing);
        }

        var replacement = new MapCollisionPatch(mapId, colorAttributeBytes);
        project.MapCollisionPatches.Add(replacement);
        return replacement;
    }
}
