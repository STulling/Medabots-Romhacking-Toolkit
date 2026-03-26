namespace Medabots.Rom.Text;

public sealed record MessagePatch
{
    public MessagePatch(MessageId id, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        Id = id;
        Text = text;
    }

    public MessageId Id { get; }

    public string Text { get; }
}
