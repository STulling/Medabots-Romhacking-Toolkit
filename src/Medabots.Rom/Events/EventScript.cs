namespace Medabots.Rom.Events;

public sealed record EventScript(short EventId, int StartOffset, IReadOnlyList<EventInstruction> Instructions);
