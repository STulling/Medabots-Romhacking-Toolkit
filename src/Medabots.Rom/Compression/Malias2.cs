namespace Medabots.Rom.Compression;

public static unsafe class Malias2
{
    public static int? TryGetEncodedLength(byte[] data, int offset)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);

        if (offset + 6 > data.Length || data[offset] != 0x4C || data[offset + 1] != 0x65)
        {
            return null;
        }

        var remaining = data[offset + 2] | (data[offset + 3] << 8) | (data[offset + 4] << 16);
        if (remaining <= 0 || remaining >= 0x100000)
        {
            return null;
        }

        var cursor = offset + 6;
        while (remaining > 0)
        {
            if (cursor >= data.Length)
            {
                return null;
            }

            var command = data[cursor++];
            for (var slot = 0; slot < 4 && remaining > 0; slot++)
            {
                switch (command & 0x3)
                {
                    case 0:
                        if (cursor + 2 > data.Length)
                        {
                            return null;
                        }

                        var longBackref = data[cursor] | (data[cursor + 1] << 8);
                        cursor += 2;
                        remaining -= (longBackref >> 12) + 3;
                        break;
                    case 1:
                        if (cursor + 1 > data.Length)
                        {
                            return null;
                        }

                        var shortBackref = data[cursor++];
                        remaining -= (shortBackref >> 2) + 2;
                        break;
                    case 2:
                        if (cursor + 1 > data.Length)
                        {
                            return null;
                        }

                        cursor++;
                        remaining--;
                        break;
                    case 3:
                        if (cursor + 3 > data.Length)
                        {
                            return null;
                        }

                        cursor += 3;
                        remaining -= 3;
                        break;
                }

                command >>= 2;
            }
        }

        return cursor - offset;
    }

    public static byte[] Compress(ReadOnlySpan<byte> data)
    {
        var result = new List<byte>(6 + data.Length + (data.Length / 2) + 8)
        {
            (byte)'L',
            (byte)'e',
            (byte)(data.Length & 0xFF),
            (byte)((data.Length >> 8) & 0xFF),
            (byte)((data.Length >> 16) & 0xFF),
            0x00
        };

        var sourceIndex = 0;
        while (sourceIndex < data.Length)
        {
            var commandIndex = result.Count;
            result.Add(0x00);
            byte command = 0;

            for (var slot = 0; slot < 4 && sourceIndex < data.Length; slot++)
            {
                var remaining = data.Length - sourceIndex;
                if (remaining >= 3)
                {
                    command |= (byte)(0x3 << (slot * 2));
                    result.Add(data[sourceIndex++]);
                    result.Add(data[sourceIndex++]);
                    result.Add(data[sourceIndex++]);
                }
                else
                {
                    command |= (byte)(0x2 << (slot * 2));
                    result.Add(data[sourceIndex++]);
                }
            }

            result[commandIndex] = command;
        }

        return result.ToArray();
    }

    public static byte[]? Decompress(byte[] data, int offset)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);

        fixed (byte* ptr = &data[offset])
        {
            return Decompress(ptr);
        }
    }

    public static byte[]? Decompress(byte* source)
    {
        byte[]? uncompressedData = null;
        if (*source == 0x4C && *(source + 1) == 0x65)
        {
            var length = *(source + 2) | (*(source + 3) << 8) | (*(source + 4) << 16);
            if (length > 0 && length < 0x100000)
            {
                uncompressedData = new byte[length];
                fixed (byte* destination = &uncompressedData[0])
                {
                    if (!DecompressInternal(source, destination))
                    {
                        uncompressedData = null;
                    }
                }
            }
        }

        return uncompressedData;
    }

    private static bool DecompressInternal(byte* source, byte* target)
    {
        var length = *(source + 2) | (*(source + 3) << 8) | (*(source + 4) << 16);
        source += 6;

        while (length > 0)
        {
            var command = *source++;
            var numCommands = 0;

            while (numCommands < 4 && length > 0)
            {
                switch (command & 3)
                {
                    case 0:
                    {
                        var src = *source++ | (*source++ << 8);
                        var copySrc = target - ((src & 0xFFF) + 5);
                        length -= (src >> 12) + 3;
                        var copyLength = (src >> 12) + 2;
                        while (copyLength-- > -1)
                        {
                            *target++ = *copySrc++;
                        }
                        break;
                    }
                    case 1:
                    {
                        var src = *source++;
                        var copySrc = target - ((src & 0x3) + 1);
                        length -= (src >> 2) + 2;
                        var copyLength = (src >> 2) + 1;
                        while (copyLength-- > -1)
                        {
                            *target++ = *copySrc++;
                        }
                        break;
                    }
                    case 2:
                        *target++ = *source++;
                        length--;
                        break;
                    case 3:
                        *target++ = *source++;
                        *target++ = *source++;
                        *target++ = *source++;
                        length -= 3;
                        break;
                }

                numCommands++;
                command >>= 2;
            }
        }

        return true;
    }
}
