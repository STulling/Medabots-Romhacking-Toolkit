namespace Medabots.Rom.Battles;

public sealed record BattleActionScriptEntry(
    byte ActionScriptId,
    int PointerOffset,
    uint ScriptRomAddress,
    int ScriptOffset,
    int ScriptLength,
    IReadOnlyList<byte> ScriptBytes);
