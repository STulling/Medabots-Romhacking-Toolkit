namespace Medabots.Rom.Events;

public sealed class EventOperationRegistry
{
    private readonly IReadOnlyDictionary<byte, EventOperationDefinition> _definitions;

    private EventOperationRegistry(IReadOnlyDictionary<byte, EventOperationDefinition> definitions)
    {
        _definitions = definitions;
    }

    public static EventOperationRegistry LoadDefault()
    {
        return new EventOperationRegistry(EventOpcodeDefinitions.Create());
    }

    public bool TryGetDefinition(byte opcode, out EventOperationDefinition definition)
    {
        return _definitions.TryGetValue(opcode, out definition!);
    }

    public IReadOnlyCollection<EventOperationDefinition> Definitions => _definitions.Values.ToArray();
}
