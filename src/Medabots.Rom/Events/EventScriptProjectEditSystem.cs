using Medabots.Rom.Metadata;
using Medabots.Rom.Projects;

namespace Medabots.Rom.Events;

internal sealed class EventScriptProjectEditSystem : IProjectEditSystem
{
    private readonly EventInstructionPatcher _eventInstructionPatcher;

    public EventScriptProjectEditSystem(EventInstructionPatcher eventInstructionPatcher)
    {
        _eventInstructionPatcher = eventInstructionPatcher;
    }

    public string DisplayName => "Event Script";

    public IEnumerable<string> DescribeChanges(RomHackProject project) =>
        project.EventScriptPatches.Select(patch => $"Event {patch.EventId:D4}")
            .Concat(project.DeletedEventScriptIds.Select(id => $"Deleted Event {id:D4}"))
            .Concat(project.EventLabels.Select(label => $"Label Event {label.EventId:D4} @ {label.Offset:X} -> {label.Label}"));

    public IEnumerable<ProjectChange> BuildChanges(RomHackProject project, ProjectBuildContext context)
    {
        if (project.EventScriptPatches.Count == 0 && project.DeletedEventScriptIds.Count == 0)
        {
            return [];
        }

        var resolvedProfile = context.Layout.RequireTextAndEventProfile();
        var actions = new List<RomPatchAction>();
        resolvedProfile = EnsureExpandedEventScriptDatabase(project, context.SourceRom, context.Allocator, resolvedProfile, actions);
        context.Layout.ReplaceTextAndEventProfile(resolvedProfile);
        foreach (var deletedEventId in project.DeletedEventScriptIds.Distinct().OrderBy(id => id))
        {
            actions.AddRange(_eventInstructionPatcher.BuildRewriteActions(context.SourceRom, resolvedProfile, deletedEventId, [MedabotsRomSchema.EventEndOpcode], $"Delete project event patch {deletedEventId}", context.Allocator));
        }

        foreach (var patch in project.EventScriptPatches)
        {
            actions.AddRange(_eventInstructionPatcher.BuildRewriteActions(context.SourceRom, resolvedProfile, patch.EventId, patch.ScriptBytes, $"Apply project event patch {patch.EventId}", context.Allocator));
        }

        return [new ProjectChange(DisplayName, "Event script patches", actions)];
    }

    private static MedabotsRomTextProfile EnsureExpandedEventScriptDatabase(RomHackProject project, RomFile romFile, FreeSpaceAllocator allocator, MedabotsRomTextProfile profile, ICollection<RomPatchAction> actions)
    {
        var requiredEventCount = Math.Max(
            profile.EventCount,
            Math.Max(
                project.EventScriptPatches.Count == 0 ? 0 : project.EventScriptPatches.Max(patch => patch.EventId + 1),
                project.DeletedEventScriptIds.Count == 0 ? 0 : project.DeletedEventScriptIds.Max(id => id + 1)));
        if (requiredEventCount <= profile.EventCount)
        {
            return profile;
        }

        var newBankTableRelativeOffset = MedabotsRomSchema.EventBankBaseAddress + (requiredEventCount * 2);
        var originalPointerTableLength = profile.EventCount * 2;
        var originalBankTableOffset = profile.EventTableOffset + originalPointerTableLength;
        var newDatabaseBaseOffset = allocator.Reserve(newBankTableRelativeOffset + requiredEventCount, 4);
        var newPointerTableOffset = newDatabaseBaseOffset + MedabotsRomSchema.EventBankBaseAddress;
        var newDatabaseLength = newBankTableRelativeOffset + requiredEventCount;
        var databaseBytes = new byte[newDatabaseLength];

        databaseBytes[0] = 0x00;
        databaseBytes[1] = 0x40;
        databaseBytes[2] = (byte)(newBankTableRelativeOffset & 0xFF);
        databaseBytes[3] = (byte)((newBankTableRelativeOffset >> 8) & 0xFF);
        Array.Copy(romFile.Data, profile.EventTableOffset, databaseBytes, MedabotsRomSchema.EventBankBaseAddress, originalPointerTableLength);
        Array.Copy(romFile.Data, originalBankTableOffset, databaseBytes, newBankTableRelativeOffset, profile.EventCount);
        actions.Add(RomPatchAction.Create(newDatabaseBaseOffset, databaseBytes, $"Expand event script database to {requiredEventCount} slots"));

        var originalDatabaseBaseOffset = profile.EventTableOffset - MedabotsRomSchema.EventBankBaseAddress;
        var originalDatabaseBaseAddress = GbaPointer.ToRomAddress(originalDatabaseBaseOffset);
        var newDatabaseBaseAddress = GbaPointer.ToRomAddress(newDatabaseBaseOffset);
        var oldPointerBytes = BitConverter.GetBytes(originalDatabaseBaseAddress);
        var newPointerBytes = BitConverter.GetBytes(newDatabaseBaseAddress);
        var romData = romFile.Data;
        for (var offset = 0; offset <= romData.Length - 4; offset++)
        {
            if (romData[offset] == oldPointerBytes[0] &&
                romData[offset + 1] == oldPointerBytes[1] &&
                romData[offset + 2] == oldPointerBytes[2] &&
                romData[offset + 3] == oldPointerBytes[3])
            {
                actions.Add(RomPatchAction.Create(offset, newPointerBytes, $"Repoint event script database literal at 0x{offset:X}"));
            }
        }

        var addresses = profile.Addresses;
        return new MedabotsRomTextProfile(
            profile.Id,
            profile.Name,
            profile.HeaderSignature,
            new MedabotsRomAddresses(
                addresses.TextPointerTableOffset,
                addresses.TextDumpOffset,
                addresses.StarterOffset,
                addresses.BattlePointerTableOffset,
                addresses.BattleCount,
                newPointerTableOffset,
                requiredEventCount));
    }

}
