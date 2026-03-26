namespace Medabots.Rom.Parts;

public sealed record CombatPartStats(
    byte Technique,
    byte Success,
    byte Power,
    byte ChargeOrChainReaction,
    byte Uses);
