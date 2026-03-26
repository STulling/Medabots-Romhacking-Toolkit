namespace Medabots.Rom.Events;

public record class EventInstruction(
    int Offset,
    byte Opcode,
    string Name,
    IReadOnlyList<EventArgumentValue> Arguments,
    string DisplayText,
    bool IsTerminal)
{
    public EventOperationDefinition? Definition { get; init; }

    public virtual string AstKind => nameof(EventInstruction);
}
