namespace Medabots.Rom;

public sealed record RomPatchAction
{
    public RomPatchAction(int offset, byte[] data, string description)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        Offset = offset;
        Data = data.ToArray();
        Description = description;
    }

    public int Offset { get; }

    public byte[] Data { get; }

    public string Description { get; }

    public static RomPatchAction Create(int offset, ReadOnlySpan<byte> data, string description)
    {
        return new RomPatchAction(offset, data.ToArray(), description);
    }
}
