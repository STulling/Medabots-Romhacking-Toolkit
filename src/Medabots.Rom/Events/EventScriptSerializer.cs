using Medabots.Rom.Metadata;

namespace Medabots.Rom.Events;

public sealed class EventScriptSerializer
{
    private readonly EventOperationRegistry _registry;

    public EventScriptSerializer(EventOperationRegistry? registry = null)
    {
        _registry = registry ?? EventOperationRegistry.LoadDefault();
    }

    public byte[] Serialize(RomFile romFile, EventScript script, EventInstruction? replacementInstruction = null)
    {
        ArgumentNullException.ThrowIfNull(romFile);
        ArgumentNullException.ThrowIfNull(script);

        var instructions = script.Instructions.OrderBy(instruction => instruction.Offset).ToArray();
        if (instructions.Length == 0)
        {
            return [];
        }

        var output = new List<byte>();
        var cursor = script.StartOffset;

        foreach (var instruction in instructions)
        {
            if (instruction.Offset < cursor)
            {
                throw new InvalidOperationException("Event instructions are not ordered consistently.");
            }

            if (instruction.Offset > cursor)
            {
                var gapLength = instruction.Offset - cursor;
                output.AddRange(romFile.ReadBytes(cursor, gapLength).Span.ToArray());
                cursor += gapLength;
            }

            var encodedInstruction = replacementInstruction is not null && replacementInstruction.Offset == instruction.Offset
                ? EncodeInstruction(replacementInstruction)
                : romFile.ReadBytes(instruction.Offset, GetInstructionLength(instruction)).Span.ToArray();
            output.AddRange(encodedInstruction);
            cursor += encodedInstruction.Length;
        }

        return output.ToArray();
    }

    public byte[] SerializeInstruction(EventInstruction instruction)
    {
        return EncodeInstruction(instruction);
    }

    private byte[] EncodeInstruction(EventInstruction instruction)
    {
        var bytes = new List<byte> { instruction.Opcode };

        switch (instruction)
        {
            case ConditionalMultiJumpInstruction:
                foreach (var argument in instruction.Arguments)
                {
                    bytes.Add(checked((byte)argument.RawValue));
                }

                return bytes.ToArray();

            case EndInstruction:
                return bytes.ToArray();

            case GotoEventInstruction gotoEvent:
                bytes.Add((byte)((gotoEvent.EventId >> 8) & 0xFF));
                bytes.Add((byte)(gotoEvent.EventId & 0xFF));
                return bytes.ToArray();
        }

        var definition = instruction.Definition;
        if (definition is null && !_registry.TryGetDefinition(instruction.Opcode, out definition))
        {
            if (instruction is UnknownOpcodeInstruction)
            {
                foreach (var argument in instruction.Arguments)
                {
                    bytes.Add(checked((byte)argument.RawValue));
                }

                return bytes.ToArray();
            }

            throw new InvalidOperationException($"Opcode 0x{instruction.Opcode:X2} cannot be serialized.");
        }

        foreach (var argument in definition.Arguments)
        {
            var value = instruction.Arguments.FirstOrDefault(candidate => string.Equals(candidate.Name, argument.Name, StringComparison.Ordinal))
                ?? throw new InvalidOperationException($"Instruction '{instruction.Name}' is missing argument '{argument.Name}'.");

            switch (argument.Type)
            {
                case EventArgumentType.Short:
                    bytes.Add((byte)((value.RawValue >> 8) & 0xFF));
                    bytes.Add((byte)(value.RawValue & 0xFF));
                    break;
                case EventArgumentType.EventBank:
                    bytes.Add((byte)(value.RawValue + MedabotsRomSchema.EventBankBias));
                    break;
                default:
                    bytes.Add(checked((byte)value.RawValue));
                    break;
            }
        }

        return bytes.ToArray();
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

        if (instruction is UnknownOpcodeInstruction)
        {
            return 1 + instruction.Arguments.Count;
        }

        if (instruction.Opcode < LegacyEventOpcodeTable.Lengths.Length)
        {
            return LegacyEventOpcodeTable.GetLength(instruction.Opcode);
        }

        return 1;
    }
}
