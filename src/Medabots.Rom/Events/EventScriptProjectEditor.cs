using Medabots.Rom.Metadata;
using Medabots.Rom.Maps;

namespace Medabots.Rom.Events;

public sealed class EventScriptProjectEditor
{
    public short AddFreshEventScript(RomHackProject project, RomFile romFile, MedabotsRomTextProfile profile, byte[] scriptBytes)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(romFile);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(scriptBytes);

        var eventId = AllocateFreshEventId(project, profile);
        UpsertEventScript(project, eventId, scriptBytes);
        return eventId;
    }

    public short AddNewEventScript(RomHackProject project, RomFile romFile, MedabotsRomTextProfile profile, byte[] scriptBytes)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(romFile);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(scriptBytes);

        var eventId = AllocateEventId(project, romFile, profile);
        UpsertEventScript(project, eventId, scriptBytes);
        return eventId;
    }

    public void UpsertEventScript(RomHackProject project, short eventId, byte[] scriptBytes)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(scriptBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(eventId);

        var existing = project.EventScriptPatches.FirstOrDefault(patch => patch.EventId == eventId);
        if (existing is not null)
        {
            project.EventScriptPatches.Remove(existing);
        }

        RemoveDeletedEventId(project, eventId);
        project.EventScriptPatches.Add(new EventScriptPatch(eventId, scriptBytes));
    }

    public void DeleteEventScript(RomHackProject project, short eventId)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentOutOfRangeException.ThrowIfNegative(eventId);

        var existing = project.EventScriptPatches.FirstOrDefault(patch => patch.EventId == eventId);
        if (existing is not null)
        {
            project.EventScriptPatches.Remove(existing);
        }

        if (!project.DeletedEventScriptIds.Contains(eventId))
        {
            project.DeletedEventScriptIds.Add(eventId);
        }
    }

    private static short AllocateEventId(RomHackProject project, RomFile romFile, MedabotsRomTextProfile profile)
    {
        foreach (var deletedId in project.DeletedEventScriptIds.OrderBy(id => id))
        {
            return deletedId;
        }

        foreach (var unreferencedId in GetMapUnreferencedEventIds(romFile, profile))
        {
            if (project.EventScriptPatches.Any(patch => patch.EventId == unreferencedId))
            {
                continue;
            }

            return unreferencedId;
        }

        for (short eventId = 0; eventId < profile.EventCount; eventId++)
        {
            if (project.EventScriptPatches.Any(patch => patch.EventId == eventId))
            {
                continue;
            }

            if (IsUnusedEventSlot(romFile, profile, eventId))
            {
                return eventId;
            }
        }

        var nextEventId = (short)Math.Max(
            profile.EventCount,
            Math.Max(
                project.EventScriptPatches.Count == 0 ? -1 : project.EventScriptPatches.Max(patch => patch.EventId) + 1,
                project.DeletedEventScriptIds.Count == 0 ? -1 : project.DeletedEventScriptIds.Max() + 1));

        if (nextEventId < 0 || nextEventId > 0x0FFF)
        {
            throw new InvalidOperationException("No reusable, unused, or expandable event script slots are available.");
        }

        return nextEventId;
    }

    private static short AllocateFreshEventId(RomHackProject project, MedabotsRomTextProfile profile)
    {
        var nextEventId = (short)Math.Max(
            profile.EventCount,
            Math.Max(
                project.EventScriptPatches.Count == 0 ? -1 : project.EventScriptPatches.Max(patch => patch.EventId) + 1,
                project.DeletedEventScriptIds.Count == 0 ? -1 : project.DeletedEventScriptIds.Max() + 1));

        if (nextEventId < 0 || nextEventId > 0x0FFF)
        {
            throw new InvalidOperationException("No expandable event script slots are available.");
        }

        return nextEventId;
    }

    private static IEnumerable<short> GetMapUnreferencedEventIds(RomFile romFile, MedabotsRomTextProfile profile)
    {
        var overlayRepository = new MapOverlayRepository();
        var referencedEventIds = new HashSet<short>();

        for (var mapId = 0; mapId < MedabotsRomSchema.MapCount; mapId++)
        {
            var overlay = overlayRepository.ReadMap(romFile, mapId);
            foreach (var spawn in overlay.EntitySpawns)
            {
                if (spawn.EventOrObjectId >= 0 &&
                    spawn.EventOrObjectId < profile.EventCount &&
                    spawn.RecordKind is 0 or 2 or 4 or 6 or 8)
                {
                    referencedEventIds.Add((short)spawn.EventOrObjectId);
                }
            }
        }

        for (short eventId = 0; eventId < profile.EventCount; eventId++)
        {
            if (!referencedEventIds.Contains(eventId))
            {
                yield return eventId;
            }
        }
    }

    private static bool IsUnusedEventSlot(RomFile romFile, MedabotsRomTextProfile profile, short eventId)
    {
        var pointerOffset = profile.EventTableOffset + (eventId * 2);
        var bankOffset = profile.EventTableOffset + MedabotsRomSchema.EventBankTableOffset + eventId;
        return romFile.Data[pointerOffset] == 0 &&
               romFile.Data[pointerOffset + 1] == 0 &&
               romFile.Data[bankOffset] == 0;
    }

    private static void RemoveDeletedEventId(RomHackProject project, short eventId)
    {
        for (var index = project.DeletedEventScriptIds.Count - 1; index >= 0; index--)
        {
            if (project.DeletedEventScriptIds[index] == eventId)
            {
                project.DeletedEventScriptIds.RemoveAt(index);
            }
        }
    }
}
