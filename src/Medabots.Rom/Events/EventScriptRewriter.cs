using Medabots.Rom.Metadata;

namespace Medabots.Rom.Events;

public sealed partial class EventScriptRewriter
{
    private readonly EventOperationRegistry _registry;
    private readonly EventScriptSerializer _serializer;

    public EventScriptRewriter(EventOperationRegistry? registry = null)
    {
        _registry = registry ?? EventOperationRegistry.LoadDefault();
        _serializer = new EventScriptSerializer(_registry);
    }

    public byte[] InsertNopBefore(RomFile romFile, EventScript script, IReadOnlyDictionary<int, string> labelMap, int targetOffset)
    {
        return InsertInstructionBefore(romFile, script, labelMap, targetOffset, CreateNopInstruction());
    }

    public byte[] InsertNopAfter(RomFile romFile, EventScript script, IReadOnlyDictionary<int, string> labelMap, int targetOffset)
    {
        return InsertInstructionAfter(romFile, script, labelMap, targetOffset, CreateNopInstruction());
    }

    public byte[] InsertInstructionBefore(RomFile romFile, EventScript script, IReadOnlyDictionary<int, string> labelMap, int targetOffset, EventOperationDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return InsertInstructionBefore(romFile, script, labelMap, targetOffset, CreateDefaultInstruction(definition));
    }

    public byte[] InsertInstructionAfter(RomFile romFile, EventScript script, IReadOnlyDictionary<int, string> labelMap, int targetOffset, EventOperationDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return InsertInstructionAfter(romFile, script, labelMap, targetOffset, CreateDefaultInstruction(definition));
    }

    public byte[] InsertInstructionBefore(RomFile romFile, EventScript script, IReadOnlyDictionary<int, string> labelMap, int targetOffset, EventInstruction instruction)
    {
        ArgumentNullException.ThrowIfNull(instruction);
        return Rewrite(romFile, script, labelMap, BuildEntriesWithInsertion(script, targetOffset, insertAfter: false, instruction));
    }

    public byte[] InsertInstructionAfter(RomFile romFile, EventScript script, IReadOnlyDictionary<int, string> labelMap, int targetOffset, EventInstruction instruction)
    {
        ArgumentNullException.ThrowIfNull(instruction);
        return Rewrite(romFile, script, labelMap, BuildEntriesWithInsertion(script, targetOffset, insertAfter: true, instruction));
    }

    public byte[] DeleteInstruction(RomFile romFile, EventScript script, IReadOnlyDictionary<int, string> labelMap, int targetOffset)
    {
        var entries = BuildEntries(script);
        var targetEntry = entries.FirstOrDefault(entry => entry.OriginalOffset == targetOffset)
            ?? throw new InvalidOperationException($"Could not find instruction at 0x{targetOffset:X}.");

        if (string.Equals(targetEntry.Label, "Start", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The first instruction of an event cannot be deleted.");
        }

        if (IsJumpTarget(script, targetOffset))
        {
            throw new InvalidOperationException("This instruction is the target of a jump. Rename or retarget the jump before deleting it.");
        }

        entries.Remove(targetEntry);
        return Rewrite(romFile, script, labelMap, entries);
    }

    public byte[] MoveInstructionUp(RomFile romFile, EventScript script, IReadOnlyDictionary<int, string> labelMap, int targetOffset)
    {
        return Rewrite(romFile, script, labelMap, BuildEntriesWithMove(script, targetOffset, moveUp: true));
    }

    public byte[] MoveInstructionDown(RomFile romFile, EventScript script, IReadOnlyDictionary<int, string> labelMap, int targetOffset)
    {
        return Rewrite(romFile, script, labelMap, BuildEntriesWithMove(script, targetOffset, moveUp: false));
    }

    private byte[] Rewrite(RomFile romFile, EventScript script, IReadOnlyDictionary<int, string> labelMap, List<EditableInstructionEntry> entries)
    {
        return CompileEntries(romFile, script, labelMap, entries);
    }
}
