namespace Medabots.Rom.Events;

public sealed partial class EventScriptRewriter
{
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
            var rewrittenJumpValues = multiJump.Arguments
                .Select(argument => ResolveNewJumpOffset(ResolveTargetLabel(labelMap, originalOffset, argument.RawValue), labelOffsets, currentOffset))
                .ToArray();
            var rewritten = EventInstructionFactory.CreateSpecialConditionalMultiJump(0, multiJump.Opcode, rewrittenJumpValues);
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
}
