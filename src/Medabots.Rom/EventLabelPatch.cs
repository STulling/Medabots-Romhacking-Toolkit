namespace Medabots.Rom;

public sealed record EventLabelPatch
{
    public EventLabelPatch(short eventId, int offset, string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        EventId = eventId;
        Offset = offset;
        Label = label;
    }

    public short EventId { get; }

    public int Offset { get; }

    public string Label { get; }
}
