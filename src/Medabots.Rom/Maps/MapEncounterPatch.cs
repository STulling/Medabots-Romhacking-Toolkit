namespace Medabots.Rom.Maps;

public sealed record MapEncounterPatch(
    int MapId,
    byte Battle1,
    byte Battle2,
    byte Battle3,
    byte Battle4);
