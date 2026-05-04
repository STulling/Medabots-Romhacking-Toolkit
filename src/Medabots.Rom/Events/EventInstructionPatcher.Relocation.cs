using Medabots.Rom.Metadata;
using Medabots.Rom.Projects;

namespace Medabots.Rom.Events;

public sealed partial class EventInstructionPatcher
{
    private IReadOnlyList<RomPatchAction> BuildRelocatedEventActions(RomFile romFile, MedabotsRomTextProfile profile, short eventId, byte[] serializedScript, string instructionName, FreeSpaceAllocator allocator)
    {
        var destination = ReserveEventSpace(romFile, eventId, serializedScript.Length, allocator);
        var actions = new List<RomPatchAction>
        {
            RomPatchAction.Create(destination, serializedScript, $"Rewrite event {eventId} for {instructionName}")
        };

        var (pointerBytes, bankByte) = BuildEventTableEntry(profile, destination);
        var pointerOffset = profile.EventTableOffset + (eventId * 2);
        var bankOffset = profile.EventTableOffset + (profile.EventCount * 2) + eventId;

        actions.Add(RomPatchAction.Create(pointerOffset, pointerBytes, $"Update event pointer for {eventId}"));
        actions.Add(RomPatchAction.Create(bankOffset, [bankByte], $"Update event bank for {eventId}"));
        return actions;
    }

    private static (byte[] PointerBytes, byte BankByte) BuildEventTableEntry(MedabotsRomTextProfile profile, int destination)
    {
        var eventBankBase = profile.EventTableOffset - MedabotsRomSchema.EventBankBaseAddress;
        var relativeOffset = destination - eventBankBase;
        var bank = relativeOffset / MedabotsRomSchema.EventBankSize;
        if (bank > byte.MaxValue)
        {
            throw new InvalidOperationException("Relocated event exceeds the addressable event bank range.");
        }

        var addressInBank = relativeOffset % MedabotsRomSchema.EventBankSize;
        var storedAddress = MedabotsRomSchema.EventBankBaseAddress + addressInBank;
        return ([(byte)(storedAddress & 0xFF), (byte)((storedAddress >> 8) & 0xFF)], (byte)bank);
    }

    private int ReserveEventSpace(RomFile romFile, short eventId, int requiredLength, FreeSpaceAllocator allocator)
    {
        if (_eventAllocations.TryGetValue(eventId, out var allocation) && requiredLength <= allocation.Length)
        {
            return allocation.Offset;
        }

        var nextOffset = allocator.Reserve(requiredLength, 4);
        _eventAllocations[eventId] = (nextOffset, requiredLength);
        return nextOffset;
    }
}
