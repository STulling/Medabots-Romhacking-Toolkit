namespace Medabots.Rom.Compression;

public static class GbaLz77
{
    private const int MaxMatchLength = 18;
    private const int MaxDisplacement = 0x1000;

    public static byte[]? Decompress(byte[] data, int offset)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        if (offset >= data.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        if (data[offset] != 0x10)
        {
            return null;
        }

        var length = data[offset + 1] | (data[offset + 2] << 8) | (data[offset + 3] << 16);
        if (length <= 0 || length >= 0x100000)
        {
            return null;
        }

        var output = new byte[length];
        var source = offset + 4;
        var written = 0;

        while (written < length)
        {
            var command = data[source++];
            for (var bit = 0; bit < 8 && written < length; bit++)
            {
                var compressed = (command & (0x80 >> bit)) != 0;
                if (!compressed)
                {
                    output[written++] = data[source++];
                    continue;
                }

                var value = (ushort)(data[source++] | (data[source++] << 8));
                var displacement = ((value & 0xF) << 8) | (value >> 8);
                var count = ((value >> 4) & 0xF) + 3;

                if (displacement > written)
                {
                    return null;
                }

                for (var i = 0; i < count && written < length; i++)
                {
                    output[written] = output[written - displacement - 1];
                    written++;
                }
            }
        }

        return output;
    }

    public static byte[] WrapUncompressed(ReadOnlySpan<byte> data)
    {
        var result = new List<byte>(4 + data.Length + (data.Length / 8) + 8)
        {
            0x10,
            (byte)(data.Length & 0xFF),
            (byte)((data.Length >> 8) & 0xFF),
            (byte)((data.Length >> 16) & 0xFF)
        };

        var index = 0;
        while (index < data.Length)
        {
            result.Add(0x00);
            for (var i = 0; i < 8; i++)
            {
                result.Add(index < data.Length ? data[index++] : (byte)0x00);
            }
        }

        return result.ToArray();
    }

    public static byte[] Compress(ReadOnlySpan<byte> data)
    {
        var result = new List<byte>(4 + data.Length);
        result.Add(0x10);
        result.Add((byte)(data.Length & 0xFF));
        result.Add((byte)((data.Length >> 8) & 0xFF));
        result.Add((byte)((data.Length >> 16) & 0xFF));

        var sourceIndex = 0;
        while (sourceIndex < data.Length)
        {
            var commandIndex = result.Count;
            result.Add(0);
            byte command = 0;

            for (var bit = 0; bit < 8 && sourceIndex < data.Length; bit++)
            {
                var (matchLength, displacement) = FindBestMatch(data, sourceIndex);
                if (matchLength >= 3)
                {
                    command |= (byte)(0x80 >> bit);
                    var encoded = (ushort)((((matchLength - 3) & 0xF) << 4) | (((displacement - 1) >> 8) & 0xF) | (((displacement - 1) & 0xFF) << 8));
                    result.Add((byte)(encoded & 0xFF));
                    result.Add((byte)(encoded >> 8));
                    sourceIndex += matchLength;
                }
                else
                {
                    result.Add(data[sourceIndex++]);
                }
            }

            result[commandIndex] = command;
        }

        return result.ToArray();
    }

    public static int? TryGetEncodedLength(byte[] data, int offset)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        if (offset + 4 > data.Length || data[offset] != 0x10)
        {
            return null;
        }

        var length = data[offset + 1] | (data[offset + 2] << 8) | (data[offset + 3] << 16);
        if (length <= 0 || length >= 0x100000)
        {
            return null;
        }

        var source = offset + 4;
        var written = 0;
        while (written < length)
        {
            if (source >= data.Length)
            {
                return null;
            }

            var command = data[source++];
            for (var bit = 0; bit < 8 && written < length; bit++)
            {
                var compressed = (command & (0x80 >> bit)) != 0;
                if (!compressed)
                {
                    if (source >= data.Length)
                    {
                        return null;
                    }

                    source++;
                    written++;
                    continue;
                }

                if (source + 1 >= data.Length)
                {
                    return null;
                }

                var value = (ushort)(data[source++] | (data[source++] << 8));
                var count = ((value >> 4) & 0xF) + 3;
                written += count;
            }
        }

        return source - offset;
    }

    private static (int Length, int Displacement) FindBestMatch(ReadOnlySpan<byte> data, int sourceIndex)
    {
        var bestLength = 0;
        var bestDisplacement = 0;
        var windowStart = Math.Max(0, sourceIndex - MaxDisplacement);

        for (var candidate = sourceIndex - 1; candidate >= windowStart; candidate--)
        {
            var length = 0;
            while (length < MaxMatchLength &&
                   sourceIndex + length < data.Length &&
                   data[candidate + length] == data[sourceIndex + length])
            {
                length++;
            }

            if (length > bestLength)
            {
                bestLength = length;
                bestDisplacement = sourceIndex - candidate;
                if (bestLength == MaxMatchLength)
                {
                    break;
                }
            }
        }

        return (bestLength, bestDisplacement);
    }
}
