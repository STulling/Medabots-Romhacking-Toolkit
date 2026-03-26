namespace Medabots.Rom.Events;

internal static class EventInstructionFactory
{
    public static EventInstruction CreateSpecialConditionalMultiJump(int offset, byte opcode, IReadOnlyList<int> jumps)
    {
        var jumpArguments = jumps
            .Select((jump, index) => new EventArgumentValue($"jump{index + 1}", EventArgumentType.Jump, jump, jump.ToString()))
            .ToArray();
        return new ConditionalMultiJumpInstruction(offset, opcode, jumpArguments, $"<Conditional_Multijump: {string.Join(", ", jumps)}>", true);
    }

    public static EventInstruction CreateSpecialEnd(int offset, byte opcode)
        => new EndInstruction(offset, opcode);

    public static EventInstruction CreateSpecialGotoEvent(int offset, byte opcode, short targetEvent)
        => new GotoEventInstruction(offset, opcode, targetEvent);

    public static EventInstruction CreateInvalid(int offset, byte opcode)
        => new InvalidOpcodeInstruction(offset, opcode);

    public static EventInstruction CreateUnknown(int offset, byte opcode, IReadOnlyList<EventArgumentValue> arguments, string displayText)
        => new UnknownOpcodeInstruction(offset, opcode, arguments, displayText, false);

    public static EventInstruction CreateDefined(int offset, byte opcode, EventOperationDefinition definition, IReadOnlyList<EventArgumentValue> arguments)
    {
        var display = $"{definition.Name}({string.Join(", ", arguments.Select(argument => $"{argument.Name}: {argument.DisplayValue}"))})";

        EventInstruction instruction = opcode switch
        {
            0x01 or 0x02 => new ShowMessageInstruction(
                offset,
                opcode,
                definition.Name,
                arguments,
                display,
                false,
                GetArgument(arguments, "bank").RawValue,
                GetArgument(arguments, "id").RawValue),
            0x1F or 0x20 or 0x21 => new InitiateActorInstruction(
                offset,
                opcode,
                definition.Name,
                arguments,
                display,
                false,
                new((byte)GetArgument(arguments, arguments[0].Name).RawValue),
                GetArgument(arguments, "sprite_id").RawValue,
                GetArgument(arguments, "x").RawValue,
                GetArgument(arguments, "y").RawValue),
            0x22 => new RotateActorInstruction(
                offset,
                opcode,
                arguments,
                display,
                false,
                new((byte)GetArgument(arguments, "tracked_object_slot").RawValue),
                new((byte)GetArgument(arguments, "dir").RawValue)),
            0x23 or 0x24 => new MoveActorInstruction(
                offset,
                opcode,
                definition.Name,
                arguments,
                display,
                false,
                new((byte)GetArgument(arguments, "tracked_object_slot").RawValue),
                new((byte)GetArgument(arguments, "move").RawValue)),
            0x25 => new UnloadActorInstruction(
                offset,
                opcode,
                arguments,
                display,
                false,
                new((byte)GetArgument(arguments, "packed_actor_id").RawValue)),
            0x26 => new FlickerActorInstruction(
                offset,
                opcode,
                arguments,
                display,
                false,
                new((byte)GetArgument(arguments, "tracked_object_slot").RawValue),
                GetArgument(arguments, "frames").RawValue),
            0x27 => new ActorAnimationInstruction(
                offset,
                opcode,
                arguments,
                display,
                false,
                new((byte)GetArgument(arguments, "tracked_object_slot").RawValue),
                GetArgument(arguments, "animation_id").RawValue,
                GetArgument(arguments, "frames").RawValue),
            0x28 => new HopActorInstruction(
                offset,
                opcode,
                arguments,
                display,
                false,
                new((byte)GetArgument(arguments, "tracked_object_slot").RawValue)),
            0x33 => new StartBattleInstruction(
                offset,
                opcode,
                arguments,
                display,
                false,
                new((byte)GetArgument(arguments, "battle").RawValue),
                new((byte)GetArgument(arguments, "battle_mode_flags").RawValue),
                new((byte)GetArgument(arguments, "post_battle_mode_flags").RawValue)),
            0x69 => new SetMapSceneVariantInstruction(
                offset,
                opcode,
                arguments,
                display,
                false,
                GetArgument(arguments, "variant").RawValue,
                GetArgument(arguments, "skip_full_reload").RawValue),
            _ => new EventInstruction(offset, opcode, definition.Name, arguments, display, false)
        };

        return instruction with { Definition = definition };
    }

    private static EventArgumentValue GetArgument(IReadOnlyList<EventArgumentValue> arguments, string name)
        => arguments.First(argument => string.Equals(argument.Name, name, StringComparison.Ordinal));
}
