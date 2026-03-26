using Medabots.Rom.Metadata;

namespace Medabots.Rom.Events;

public sealed class EventScriptRewriter
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

    private byte[] Rewrite(RomFile romFile, EventScript script, IReadOnlyDictionary<int, string> labelMap, List<EditableInstructionEntry> entries)
    {
        var compiled = CompileEntries(romFile, script, labelMap, entries);
        return compiled;
    }

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

    private List<EditableInstructionEntry> BuildEntries(EventScript script)
    {
        return script.Instructions
            .OrderBy(instruction => instruction.Offset)
            .Select(instruction => new EditableInstructionEntry(instruction, null, instruction.Offset))
            .ToList();
    }

    private byte[] CompileEntries(RomFile romFile, EventScript script, IReadOnlyDictionary<int, string> labelMap, List<EditableInstructionEntry> entries)
    {
        foreach (var entry in entries)
        {
            if (entry.OriginalOffset is int originalOffset && labelMap.TryGetValue(originalOffset, out var label))
            {
                entry.Label = label;
            }
        }

        var localOffsets = new Dictionary<EditableInstructionEntry, int>();
        var labelOffsets = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var currentOffset = 0;

        foreach (var entry in entries)
        {
            localOffsets[entry] = currentOffset;
            if (!string.IsNullOrWhiteSpace(entry.Label))
            {
                labelOffsets[entry.Label] = currentOffset;
            }

            currentOffset += GetInstructionLength(entry.Instruction);
        }

        var output = new List<byte>(currentOffset);
        foreach (var entry in entries)
        {
            var encoded = EncodeEntry(romFile, script, labelMap, entry, localOffsets[entry], labelOffsets);
            output.AddRange(encoded);
        }

        return output.ToArray();
    }

    private byte[] EncodeEntry(
        RomFile romFile,
        EventScript script,
        IReadOnlyDictionary<int, string> labelMap,
        EditableInstructionEntry entry,
        int currentOffset,
        IReadOnlyDictionary<string, int> labelOffsets)
    {
        if (entry.OriginalOffset is not int originalOffset)
        {
            return _serializer.SerializeInstruction(entry.Instruction);
        }

        if (!HasJump(entry.Instruction))
        {
            return romFile.ReadBytes(originalOffset, GetInstructionLength(entry.Instruction)).Span.ToArray();
        }

        if (entry.Instruction is ConditionalMultiJumpInstruction multiJump)
        {
            var jumpArguments = multiJump.Arguments.Select(argument =>
            {
                var labelName = ResolveTargetLabel(labelMap, originalOffset, argument.RawValue);
                var newJump = ResolveNewJumpOffset(labelName, labelOffsets, currentOffset);
                return new EventArgumentValue(argument.Name, argument.Type, newJump, newJump.ToString());
            }).ToArray();
            var rewritten = EventInstructionFactory.CreateSpecialConditionalMultiJump(0, multiJump.Opcode, jumpArguments.Select(argument => argument.RawValue).ToArray());
            return _serializer.SerializeInstruction(rewritten);
        }

        var definition = entry.Instruction.Definition ?? throw new InvalidOperationException($"Instruction '{entry.Instruction.Name}' is missing a definition.");
        var rewrittenArguments = entry.Instruction.Arguments.Select(argument =>
        {
            if (argument.Type != EventArgumentType.Jump && !string.Equals(argument.Name, "jump", StringComparison.Ordinal))
            {
                return argument;
            }

            var labelName = ResolveTargetLabel(labelMap, originalOffset, argument.RawValue);
            var newJump = ResolveNewJumpOffset(labelName, labelOffsets, currentOffset);
            return new EventArgumentValue(argument.Name, argument.Type, newJump, newJump.ToString());
        }).ToArray();

        var rewrittenInstruction = EventInstructionFactory.CreateDefined(0, entry.Instruction.Opcode, definition, rewrittenArguments);
        return _serializer.SerializeInstruction(rewrittenInstruction);
    }

    private string ResolveTargetLabel(IReadOnlyDictionary<int, string> labelMap, int sourceOffset, int jumpOffset)
    {
        var targetOffset = sourceOffset + jumpOffset + 1;
        if (labelMap.TryGetValue(targetOffset, out var label))
        {
            return label;
        }

        throw new InvalidOperationException($"Could not resolve the jump target at 0x{targetOffset:X}.");
    }

    private static int ResolveNewJumpOffset(string labelName, IReadOnlyDictionary<string, int> labelOffsets, int currentOffset)
    {
        if (!labelOffsets.TryGetValue(labelName, out var targetOffset))
        {
            throw new InvalidOperationException($"Jump target label '{labelName}' no longer exists.");
        }

        return targetOffset - currentOffset - 1;
    }

    private bool HasJump(EventInstruction instruction)
    {
        if (instruction is ConditionalMultiJumpInstruction)
        {
            return true;
        }

        return instruction.Arguments.Any(argument =>
            argument.Type == EventArgumentType.Jump ||
            string.Equals(argument.Name, "jump", StringComparison.Ordinal));
    }

    private int GetInstructionLength(EventInstruction instruction)
    {
        if (instruction is ConditionalMultiJumpInstruction)
        {
            return 1 + instruction.Arguments.Count;
        }

        if (instruction is EndInstruction)
        {
            return 1;
        }

        if (instruction is GotoEventInstruction)
        {
            return 3;
        }

        if (instruction.Definition is not null)
        {
            return instruction.Definition.Size;
        }

        if (_registry.TryGetDefinition(instruction.Opcode, out var definition))
        {
            return definition.Size;
        }

        return instruction.Opcode < LegacyEventOpcodeTable.Lengths.Length
            ? LegacyEventOpcodeTable.GetLength(instruction.Opcode)
            : 1 + instruction.Arguments.Count;
    }

    private EventInstruction CreateNopInstruction()
    {
        if (!_registry.TryGetDefinition(0x00, out var definition))
        {
            throw new InvalidOperationException("Could not resolve the Nop event opcode definition.");
        }

        return CreateDefaultInstruction(definition);
    }

    private EventInstruction CreateDefaultInstruction(EventOperationDefinition definition)
    {
        var arguments = definition.Arguments
            .Select(argument =>
            {
                var value = string.Equals(argument.Name, "jump", StringComparison.Ordinal)
                    ? definition.Size - 1
                    : 0;
                return new EventArgumentValue(argument.Name, argument.Type, value, value.ToString());
            })
            .ToArray();

        return EventInstructionFactory.CreateDefined(0, definition.Opcode, definition, arguments);
    }

    private bool IsJumpTarget(EventScript script, int targetOffset)
    {
        foreach (var instruction in script.Instructions)
        {
            if (instruction is ConditionalMultiJumpInstruction)
            {
                if (instruction.Arguments.Any(argument => instruction.Offset + argument.RawValue + 1 == targetOffset))
                {
                    return true;
                }

                continue;
            }

            var jump = instruction.Arguments.FirstOrDefault(argument =>
                argument.Type == EventArgumentType.Jump ||
                string.Equals(argument.Name, "jump", StringComparison.Ordinal));
            if (jump is not null && instruction.Offset + jump.RawValue + 1 == targetOffset)
            {
                return true;
            }
        }

        return false;
    }

    private sealed record EditableInstructionEntry(EventInstruction Instruction, string? Label, int? OriginalOffset)
    {
        public string? Label { get; set; } = Label;
    }
}
