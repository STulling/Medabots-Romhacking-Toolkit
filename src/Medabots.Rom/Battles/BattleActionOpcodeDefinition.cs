namespace Medabots.Rom.Battles;

public sealed record BattleActionOpcodeDefinition(
    byte Opcode,
    string Name,
    string HandlerName,
    string Summary,
    int InlineArgumentCount);
