namespace Medabots.Rom.Events;

public sealed partial class EventScriptRewriter
{
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
}
