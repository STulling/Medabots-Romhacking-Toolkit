namespace Medabots.Rom.Events;

public sealed record EventArgumentDefinition(string Name, EventArgumentType Type)
{
    public int Size => Type == EventArgumentType.Short ? 2 : 1;
}
