namespace Medabots.Rom.Battles;

public sealed record BattleActionOpcodeEntry(
    byte Opcode,
    int PointerOffset,
    uint HandlerRomAddress,
    int HandlerOffset);
