namespace Medabots.Rom.Parts;

public sealed record LegPartStats(
    byte LegType,
    byte Propulsion,
    byte Evasion,
    byte Defense,
    byte Conceal);
