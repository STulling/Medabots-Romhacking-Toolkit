namespace Medabots.Rom.Starter;

public sealed record StarterDefinition(int PartsOffset, int MedalOffset, byte PartId, byte MedalId, bool IsFemale);
