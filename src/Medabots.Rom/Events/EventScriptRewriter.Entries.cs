namespace Medabots.Rom.Events;

public sealed partial class EventScriptRewriter
{
    private List<EditableInstructionEntry> BuildEntriesWithInsertion(EventScript script, int targetOffset, bool insertAfter, EventInstruction instructionToInsert)
    {
        var entries = BuildEntries(script);
        var targetIndex = entries.FindIndex(entry => entry.OriginalOffset == targetOffset);
        if (targetIndex < 0)
        {
            throw new InvalidOperationException($"Could not find instruction at 0x{targetOffset:X}.");
        }

        entries.Insert(insertAfter ? targetIndex + 1 : targetIndex, new EditableInstructionEntry(instructionToInsert, null, null));
        return entries;
    }

    private List<EditableInstructionEntry> BuildEntriesWithMove(EventScript script, int targetOffset, bool moveUp)
    {
        var entries = BuildEntries(script);
        var targetIndex = entries.FindIndex(entry => entry.OriginalOffset == targetOffset);
        if (targetIndex < 0)
        {
            throw new InvalidOperationException($"Could not find instruction at 0x{targetOffset:X}.");
        }

        if (targetIndex == 0)
        {
            throw new InvalidOperationException("The first instruction of an event cannot be reordered.");
        }

        if (moveUp)
        {
            if (targetIndex == 1)
            {
                throw new InvalidOperationException("This instruction cannot move ahead of the event start.");
            }

            (entries[targetIndex - 1], entries[targetIndex]) = (entries[targetIndex], entries[targetIndex - 1]);
        }
        else
        {
            if (targetIndex >= entries.Count - 1)
            {
                throw new InvalidOperationException("This instruction is already the last instruction in the event.");
            }

            (entries[targetIndex], entries[targetIndex + 1]) = (entries[targetIndex + 1], entries[targetIndex]);
        }

        return entries;
    }

    private List<EditableInstructionEntry> BuildEntries(EventScript script)
    {
        return script.Instructions
            .OrderBy(instruction => instruction.Offset)
            .Select(instruction => new EditableInstructionEntry(instruction, null, instruction.Offset))
            .ToList();
    }

    private sealed record EditableInstructionEntry(EventInstruction Instruction, string? Label, int? OriginalOffset)
    {
        public string? Label { get; set; } = Label;
    }
}
