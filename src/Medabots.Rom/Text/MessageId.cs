namespace Medabots.Rom.Text;

public readonly record struct MessageId
{
    public MessageId(int bank, int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bank);
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        Bank = bank;
        Index = index;
    }

    public int Bank { get; }

    public int Index { get; }

    public override string ToString() => $"{Bank}:{Index}";
}
