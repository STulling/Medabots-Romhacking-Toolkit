namespace Medabots.Rom;

public sealed class RomFile
{
    private byte[] _data;

    public RomFile(string filePath, byte[] data)
    {
        FilePath = filePath;
        _data = data;
    }

    public string FilePath { get; }

    public byte[] Data => _data;

    public int Length => _data.Length;

    public static async Task<RomFile> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var data = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
        return new RomFile(filePath, data);
    }

    public ReadOnlyMemory<byte> ReadBytes(int offset, int length)
    {
        ValidateRange(offset, length);
        return _data.AsMemory(offset, length);
    }

    public void WriteBytes(int offset, ReadOnlySpan<byte> bytes)
    {
        EnsureCapacity(offset + bytes.Length);
        bytes.CopyTo(_data.AsSpan(offset, bytes.Length));
    }

    public void EnsureCapacity(int minimumLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minimumLength);

        if (minimumLength <= _data.Length)
        {
            return;
        }

        Array.Resize(ref _data, minimumLength);
    }

    public async Task SaveAsync(string? destinationPath = null, CancellationToken cancellationToken = default)
    {
        var outputPath = string.IsNullOrWhiteSpace(destinationPath) ? FilePath : destinationPath;
        await File.WriteAllBytesAsync(outputPath, _data, cancellationToken).ConfigureAwait(false);
    }

    private void ValidateRange(int offset, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        if (offset + length > _data.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "The requested byte range exceeds the ROM size.");
        }
    }
}
