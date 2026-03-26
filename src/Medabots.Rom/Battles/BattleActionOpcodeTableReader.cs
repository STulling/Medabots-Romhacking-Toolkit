using Medabots.Rom.Metadata;

namespace Medabots.Rom.Battles;

public sealed class BattleActionOpcodeTableReader
{
    public IReadOnlyList<BattleActionOpcodeEntry> ReadAll(RomFile romFile)
    {
        ArgumentNullException.ThrowIfNull(romFile);

        var entries = new List<BattleActionOpcodeEntry>(MedabotsRomSchema.BattleActionOpcodeCount);

        for (var opcode = 0; opcode < MedabotsRomSchema.BattleActionOpcodeCount; opcode++)
        {
            var pointerOffset = MedabotsRomSchema.BattleActionOpcodeHandlerTableOffset + (opcode * sizeof(uint));
            if (!GbaPointer.TryReadFileOffset(romFile.Data, pointerOffset, out var handlerOffset))
            {
                throw new InvalidDataException($"Battle action opcode pointer 0x{opcode:X2} at 0x{pointerOffset:X} is invalid.");
            }

            var handlerRomAddress = BitConverter.ToUInt32(romFile.Data, pointerOffset);
            entries.Add(new BattleActionOpcodeEntry((byte)opcode, pointerOffset, handlerRomAddress, handlerOffset));
        }

        return entries;
    }
}
