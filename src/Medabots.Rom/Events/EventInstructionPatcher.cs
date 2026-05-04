using Medabots.Rom.Metadata;
using Medabots.Rom.Projects;

namespace Medabots.Rom.Events;

public sealed partial class EventInstructionPatcher
{
    private readonly EventOperationRegistry _registry;
    private readonly EventScriptSerializer _serializer;
    private readonly Dictionary<short, (int Offset, int Length)> _eventAllocations = [];

    public EventInstructionPatcher(EventOperationRegistry? registry = null)
    {
        _registry = registry ?? EventOperationRegistry.LoadDefault();
        _serializer = new EventScriptSerializer(_registry);
    }

    public void Apply(RomHackSession session, MedabotsRomTextProfile profile, EventScript script, EventInstruction instruction, EventOperationDefinition targetDefinition, IReadOnlyDictionary<string, int> updatedArguments)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(instruction);
        ArgumentNullException.ThrowIfNull(targetDefinition);
        ArgumentNullException.ThrowIfNull(updatedArguments);

        var replacementInstruction = BuildReplacementInstruction(instruction, targetDefinition, updatedArguments);
        var serializedScript = _serializer.Serialize(session.RomFile, script, replacementInstruction);
        RewriteEvent(session, profile, script.EventId, serializedScript, targetDefinition.Name);
    }

    public void RewriteEvent(RomHackSession session, MedabotsRomTextProfile profile, short eventId, byte[] serializedScript, string description)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(serializedScript);

        session.ApplyPatches(BuildRewriteActions(session.RomFile, profile, eventId, serializedScript, description, new FreeSpaceAllocator(FreeSpaceAllocator.AlignUp(Math.Max(session.RomFile.Length, 0x800000), 4))));
    }

    public IReadOnlyList<RomPatchAction> BuildRewriteActions(RomFile romFile, MedabotsRomTextProfile profile, short eventId, byte[] serializedScript, string description, FreeSpaceAllocator allocator)
    {
        ArgumentNullException.ThrowIfNull(romFile);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(serializedScript);
        ArgumentNullException.ThrowIfNull(allocator);

        return BuildRelocatedEventActions(romFile, profile, eventId, serializedScript, description, allocator);
    }
}
