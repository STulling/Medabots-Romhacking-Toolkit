using Medabots.Rom.Metadata;

namespace Medabots.Rom.Events;

public sealed class EventInstructionPatcher
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

        var bytes = new List<byte>(targetDefinition.Size - 1);
        foreach (var argument in targetDefinition.Arguments)
        {
            if (!updatedArguments.TryGetValue(argument.Name, out var value))
            {
                throw new InvalidOperationException($"Missing value for argument '{argument.Name}'.");
            }

            switch (argument.Type)
            {
                case EventArgumentType.Short:
                    if (value is < 0 or > ushort.MaxValue)
                    {
                        throw new InvalidOperationException($"{argument.Name} must be between 0 and {ushort.MaxValue}.");
                    }

                    bytes.Add((byte)((value >> 8) & 0xFF));
                    bytes.Add((byte)(value & 0xFF));
                    break;

                case EventArgumentType.EventBank:
                    if (value is < 0 or > byte.MaxValue - MedabotsRomSchema.EventBankBias)
                    {
                        throw new InvalidOperationException($"{argument.Name} must be between 0 and {byte.MaxValue - MedabotsRomSchema.EventBankBias}.");
                    }

                    bytes.Add((byte)(value + MedabotsRomSchema.EventBankBias));
                    break;

                default:
                    if (value is < 0 or > byte.MaxValue)
                    {
                        throw new InvalidOperationException($"{argument.Name} must be between 0 and {byte.MaxValue}.");
                    }

                    bytes.Add((byte)value);
                    break;
            }
        }

        var replacementArguments = targetDefinition.Arguments
            .Select(argument =>
            {
                var replacementValue = updatedArguments[argument.Name];
                return new EventArgumentValue(argument.Name, argument.Type, replacementValue, replacementValue.ToString());
            })
            .ToArray();
        var replacementInstruction = EventInstructionFactory.CreateDefined(instruction.Offset, targetDefinition.Opcode, targetDefinition, replacementArguments);
        var serializedScript = _serializer.Serialize(session.RomFile, script, replacementInstruction);
        RewriteEvent(session, profile, script.EventId, serializedScript, targetDefinition.Name);
    }

    public void RewriteEvent(RomHackSession session, MedabotsRomTextProfile profile, short eventId, byte[] serializedScript, string description)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(serializedScript);

        WriteRelocatedEvent(session, profile, eventId, serializedScript, description);
    }

    private void WriteRelocatedEvent(RomHackSession session, MedabotsRomTextProfile profile, short eventId, byte[] serializedScript, string instructionName)
    {
        var destination = ReserveEventSpace(session.RomFile, eventId, serializedScript.Length);
        session.ApplyPatch(RomPatchAction.Create(destination, serializedScript, $"Rewrite event {eventId} for {instructionName}"));

        var eventBankBase = profile.EventTableOffset - MedabotsRomSchema.EventBankBaseAddress;
        var relativeOffset = destination - eventBankBase;
        var bank = relativeOffset / MedabotsRomSchema.EventBankSize;
        if (bank > byte.MaxValue)
        {
            throw new InvalidOperationException("Relocated event exceeds the addressable event bank range.");
        }

        var addressInBank = relativeOffset % MedabotsRomSchema.EventBankSize;
        var storedAddress = MedabotsRomSchema.EventBankBaseAddress + addressInBank;
        var pointerOffset = profile.EventTableOffset + (eventId * 2);
        var bankOffset = profile.EventTableOffset + MedabotsRomSchema.EventBankTableOffset + eventId;

        session.ApplyPatch(RomPatchAction.Create(pointerOffset, [(byte)(storedAddress & 0xFF), (byte)((storedAddress >> 8) & 0xFF)], $"Update event pointer for {eventId}"));
        session.ApplyPatch(RomPatchAction.Create(bankOffset, [(byte)bank], $"Update event bank for {eventId}"));
    }

    private int ReserveEventSpace(RomFile romFile, short eventId, int requiredLength)
    {
        if (_eventAllocations.TryGetValue(eventId, out var allocation) && requiredLength <= allocation.Length)
        {
            return allocation.Offset;
        }

        var nextOffset = AlignUp(Math.Max(romFile.Length, 0x800000), 4);
        _eventAllocations[eventId] = (nextOffset, requiredLength);
        return nextOffset;
    }

    private static int AlignUp(int value, int alignment)
    {
        var remainder = value % alignment;
        return remainder == 0 ? value : value + (alignment - remainder);
    }
}
