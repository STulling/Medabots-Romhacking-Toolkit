using Medabots.Rom.Metadata;

namespace Medabots.Rom.Events;

public sealed partial class EventInstructionPatcher
{
    private void WriteRelocatedEvent(RomHackSession session, MedabotsRomTextProfile profile, short eventId, byte[] serializedScript, string instructionName)
    {
        var destination = ReserveEventSpace(session.RomFile, eventId, serializedScript.Length);
        session.ApplyPatch(RomPatchAction.Create(destination, serializedScript, $"Rewrite event {eventId} for {instructionName}"));

        var (pointerBytes, bankByte) = BuildEventTableEntry(profile, destination);
        var pointerOffset = profile.EventTableOffset + (eventId * 2);
        var bankOffset = profile.EventTableOffset + (profile.EventCount * 2) + eventId;

        session.ApplyPatch(RomPatchAction.Create(pointerOffset, pointerBytes, $"Update event pointer for {eventId}"));
        session.ApplyPatch(RomPatchAction.Create(bankOffset, [bankByte], $"Update event bank for {eventId}"));
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
