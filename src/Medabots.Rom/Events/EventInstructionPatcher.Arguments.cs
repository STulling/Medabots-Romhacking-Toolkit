namespace Medabots.Rom.Events;

public sealed partial class EventInstructionPatcher
{
    private EventInstruction BuildReplacementInstruction(EventInstruction instruction, EventOperationDefinition targetDefinition, IReadOnlyDictionary<string, int> updatedArguments)
    {
        var replacementArguments = targetDefinition.Arguments
            .Select(argument =>
            {
                if (!updatedArguments.TryGetValue(argument.Name, out var replacementValue))
                {
                    throw new InvalidOperationException($"Missing value for argument '{argument.Name}'.");
                }

                ValidateArgumentValue(argument, replacementValue);
                return new EventArgumentValue(argument.Name, argument.Type, replacementValue, replacementValue.ToString());
            })
            .ToArray();

        return EventInstructionFactory.CreateDefined(instruction.Offset, targetDefinition.Opcode, targetDefinition, replacementArguments);
    }

    private static void ValidateArgumentValue(EventArgumentDefinition argument, int value)
    {
        switch (argument.Type)
        {
            case EventArgumentType.Short:
                if (value is < 0 or > ushort.MaxValue)
                {
                    throw new InvalidOperationException($"{argument.Name} must be between 0 and {ushort.MaxValue}.");
                }
                break;

            case EventArgumentType.EventBank:
                if (value is < 0 or > byte.MaxValue - Medabots.Rom.Metadata.MedabotsRomSchema.EventBankBias)
                {
                    throw new InvalidOperationException($"{argument.Name} must be between 0 and {byte.MaxValue - Medabots.Rom.Metadata.MedabotsRomSchema.EventBankBias}.");
                }
                break;

            default:
                if (value is < 0 or > byte.MaxValue)
                {
                    throw new InvalidOperationException($"{argument.Name} must be between 0 and {byte.MaxValue}.");
                }
                break;
        }
    }
}
