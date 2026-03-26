using Medabots.Rom.Metadata;

namespace Medabots.Rom.Events;

public sealed class EventScriptReader
{
    private readonly EventOperationRegistry _registry;

    public EventScriptReader(EventOperationRegistry? registry = null)
    {
        _registry = registry ?? EventOperationRegistry.LoadDefault();
    }

    public EventScript ReadById(RomFile romFile, MedabotsRomTextProfile profile, short eventId)
    {
        ArgumentNullException.ThrowIfNull(romFile);
        ArgumentNullException.ThrowIfNull(profile);

        if (eventId < 0 || eventId >= profile.EventCount)
        {
            throw new ArgumentOutOfRangeException(nameof(eventId), $"Event id must be between 0 and {profile.EventCount - 1}.");
        }

        var startOffset = ResolveEventOffset(romFile.Data, profile, eventId);
        var instructions = new Dictionary<int, EventInstruction>();
        ParseBranch(romFile.Data, startOffset, instructions, []);
        var orderedInstructions = instructions
            .OrderBy(pair => pair.Key)
            .Select(pair => pair.Value)
            .ToArray();
        return new EventScript(eventId, startOffset, orderedInstructions);
    }

    private void ParseBranch(byte[] romData, int offset, Dictionary<int, EventInstruction> instructions, HashSet<int> visitedOffsets)
    {
        while (true)
        {
            if (!visitedOffsets.Add(offset))
            {
                return;
            }

            var opcode = romData[offset];
            if (opcode == MedabotsRomSchema.EventConditionalMultiJumpOpcode)
            {
                var jumps = ReadMultiJumpOffsets(romData, offset);
                instructions.TryAdd(offset, EventInstructionFactory.CreateSpecialConditionalMultiJump(offset, opcode, jumps));

                foreach (var jump in jumps)
                {
                    ParseBranch(romData, offset + jump + 1, instructions, visitedOffsets);
                }

                return;
            }

            if (opcode == MedabotsRomSchema.EventEndOpcode)
            {
                instructions.TryAdd(offset, EventInstructionFactory.CreateSpecialEnd(offset, opcode));
                return;
            }

            if (opcode == MedabotsRomSchema.EventGotoEventOpcode)
            {
                var targetEvent = (romData[offset + 1] << 8) + romData[offset + 2];
                instructions.TryAdd(offset, EventInstructionFactory.CreateSpecialGotoEvent(offset, opcode, (short)targetEvent));
                return;
            }

            if (_registry.TryGetDefinition(opcode, out var definition))
            {
                var arguments = ReadArguments(romData, offset + 1, definition.Arguments);
                instructions.TryAdd(offset, EventInstructionFactory.CreateDefined(offset, opcode, definition, arguments));

                if (definition.HasJumpArgument)
                {
                    var jumpArgument = arguments.First(argument => string.Equals(argument.Name, "jump", StringComparison.Ordinal));
                    ParseBranch(romData, offset + jumpArgument.RawValue + 1, instructions, visitedOffsets);
                }

                offset += definition.Size;
                continue;
            }

            if (opcode >= LegacyEventOpcodeTable.Lengths.Length)
            {
                instructions.TryAdd(offset, EventInstructionFactory.CreateInvalid(offset, opcode));
                return;
            }

            var fallbackLength = LegacyEventOpcodeTable.GetLength(opcode);
            var rawArguments = new List<EventArgumentValue>(Math.Max(0, fallbackLength - 1));
            for (var index = 1; index < fallbackLength; index++)
            {
                var value = romData[offset + index];
                rawArguments.Add(new EventArgumentValue($"arg{index}", EventArgumentType.Byte, value, value.ToString()));
            }

            instructions.TryAdd(offset, EventInstructionFactory.CreateUnknown(offset, opcode, rawArguments, FormatUnknown(opcode, rawArguments)));
            offset += fallbackLength;
        }
    }

    private static int ResolveEventOffset(byte[] romData, MedabotsRomTextProfile profile, short eventId)
    {
        var eventTableOffset = profile.EventTableOffset;
        var bankOffset = romData[eventTableOffset + MedabotsRomSchema.EventBankTableOffset + eventId] * MedabotsRomSchema.EventBankSize;
        var addressInBank = ((romData[eventTableOffset + eventId * 2 + 1] << 8) + romData[eventTableOffset + eventId * 2]) - MedabotsRomSchema.EventBankBaseAddress;
        return (eventTableOffset - MedabotsRomSchema.EventBankBaseAddress) + addressInBank + bankOffset;
    }

    private static IReadOnlyList<int> ReadMultiJumpOffsets(byte[] romData, int offset)
    {
        List<int> jumps = [romData[offset + 1], romData[offset + 2]];
        for (var index = 2; index < MedabotsRomSchema.EventMultiJumpMaxEntries; index++)
        {
            var value = romData[offset + 1 + index];
            var currentMin = int.MaxValue;
            for (var jumpIndex = 0; jumpIndex < jumps.Count; jumpIndex++)
            {
                if (jumps[jumpIndex] < currentMin)
                {
                    currentMin = jumps[jumpIndex];
                }
            }

            if (currentMin == jumps.Count)
            {
                return jumps.Take(currentMin).ToArray();
            }

            jumps.Add(value);
        }

        return jumps.ToArray();
    }

    private static IReadOnlyList<EventArgumentValue> ReadArguments(byte[] romData, int offset, IReadOnlyList<EventArgumentDefinition> definitions)
    {
        var arguments = new List<EventArgumentValue>(definitions.Count);
        var cursor = offset;

        foreach (var definition in definitions)
        {
            var rawValue = definition.Type switch
            {
                EventArgumentType.Short => (romData[cursor] << 8) + romData[cursor + 1],
                EventArgumentType.EventBank => romData[cursor] - MedabotsRomSchema.EventBankBias,
                _ => romData[cursor]
            };

            arguments.Add(new EventArgumentValue(definition.Name, definition.Type, rawValue, FormatArgument(definition.Type, rawValue)));
            cursor += definition.Size;
        }

        return arguments;
    }

    private static string FormatArgument(EventArgumentType type, int value) =>
        type switch
        {
            EventArgumentType.PackedTrackedObjectId => FormatPackedTrackedObjectId(value),
            EventArgumentType.TrackedObjectSlot => value.ToString(),
            EventArgumentType.Direction => value switch
            {
                0 => "north",
                1 => "south",
                2 => "west",
                3 => "east",
                _ => value.ToString()
            },
            EventArgumentType.Part => value switch
            {
                0 => "head",
                1 => "right",
                2 => "left",
                3 => "legs",
                _ => value.ToString()
            },
            EventArgumentType.Move => FormatMove(value),
            EventArgumentType.BattleId => value.ToString(),
            EventArgumentType.BattleModeFlags => $"0x{value:X2}",
            EventArgumentType.PostBattleModeFlags => $"0x{value:X2}",
            EventArgumentType.MapSceneVariant => value.ToString(),
            _ => value.ToString()
        };

    private static string FormatPackedTrackedObjectId(int value)
    {
        var packed = new PackedTrackedObjectId((byte)value);
        return packed.Flags == 0
            ? $"slot {packed.TrackedObjectSlot}"
            : $"slot {packed.TrackedObjectSlot}, flags 0x{packed.Flags:X2}";
    }

    private static string FormatMove(int value)
    {
        if (value == MedabotsRomSchema.EventMoveNone)
        {
            return "-";
        }

        var direction = (value & MedabotsRomSchema.EventMoveMask) switch
        {
            MedabotsRomSchema.EventMoveNorth => "north",
            MedabotsRomSchema.EventMoveSouth => "south",
            MedabotsRomSchema.EventMoveWest => "west",
            MedabotsRomSchema.EventMoveEast => "east",
            _ => "?"
        };

        return $"({direction}, {value & MedabotsRomSchema.EventMoveDistanceMask})";
    }

    private static string FormatUnknown(byte opcode, IReadOnlyList<EventArgumentValue> arguments)
    {
        return arguments.Count == 0
            ? $"<UNKNOWN_{opcode:X2}>"
            : $"<UNKNOWN_{opcode:X2}: {string.Join(", ", arguments.Select(argument => argument.RawValue))}>";
    }
}
