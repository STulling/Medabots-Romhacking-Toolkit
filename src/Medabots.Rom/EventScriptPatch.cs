namespace Medabots.Rom;

public sealed record EventScriptPatch
{
    public EventScriptPatch(short eventId, byte[] scriptBytes)
    {
        ArgumentNullException.ThrowIfNull(scriptBytes);
        EventId = eventId;
        ScriptBytes = scriptBytes.ToArray();
    }

    public short EventId { get; }

    public byte[] ScriptBytes { get; }
}
