using System.Globalization;
using System.Text;

namespace Medabots.Rom.Text;

public sealed class MedabotsTextCodec
{
    private static readonly char[] CharacterTable =
    [
        ' ', 'A', 'B', 'C', 'D', 'E', 'F', 'G',
        'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O',
        'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W',
        'X', 'Y', 'Z', 'a', 'b', 'c', 'd', 'e',
        'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm',
        'n', 'o', 'p', 'q', 'r', 's', 't', 'u',
        'v', 'w', 'x', 'y', 'z', '0', '1', '2',
        '3', '4', '5', '6', '7', '8', '9', '·',
        '.', ',', '\'', '-', '/', ':', '?', '!',
        '"', '(', ')', '♥', '£', '&', '%'
    ];

    private static readonly IReadOnlyDictionary<char, byte> CharacterMap = CharacterTable
        .Select((character, index) => new KeyValuePair<char, byte>(character, (byte)index))
        .GroupBy(pair => pair.Key)
        .ToDictionary(group => group.Key, group => group.First().Value);

    public byte[] Encode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var encodedBytes = new List<byte>(text.Length);
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (character == '<')
            {
                var closingBracket = text.IndexOf('>', index + 1);
                if (closingBracket < 0)
                {
                    throw new FormatException("Encountered an unterminated message command.");
                }

                var command = text[(index + 1)..closingBracket];
                EncodeCommand(command, encodedBytes);
                index = closingBracket;
                continue;
            }

            if (!CharacterMap.TryGetValue(character, out var value))
            {
                throw new FormatException($"Character '{character}' is not supported by the Medabots text encoding.");
            }

            encodedBytes.Add(value);
        }

        return encodedBytes.ToArray();
    }

    public string Decode(ReadOnlySpan<byte> encodedText)
    {
        var builder = new StringBuilder(encodedText.Length);
        DecodeIntoBuilder(encodedText, builder, stopAtTerminator: false);
        return builder.ToString();
    }

    public byte[] ReadEncodedMessage(ReadOnlySpan<byte> romData, int fileOffset)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(fileOffset);
        if (fileOffset >= romData.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(fileOffset), "The message offset is outside the ROM.");
        }

        var bytes = new List<byte>();
        var cursor = fileOffset;

        while (cursor < romData.Length)
        {
            var value = romData[cursor];
            var commandLength = GetEncodedLength(value);
            EnsureRemainingBytes(romData, cursor, commandLength);

            for (var i = 0; i < commandLength; i++)
            {
                bytes.Add(romData[cursor + i]);
            }

            cursor += commandLength;
            if (value is 0xFE or 0xFF)
            {
                return bytes.ToArray();
            }
        }

        throw new InvalidDataException("Reached the end of the ROM before a message terminator was found.");
    }

    public string DecodeMessage(ReadOnlySpan<byte> romData, int fileOffset)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(fileOffset);
        if (fileOffset >= romData.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(fileOffset), "The message offset is outside the ROM.");
        }

        var builder = new StringBuilder(32);
        DecodeIntoBuilder(romData[fileOffset..], builder, stopAtTerminator: true);
        return builder.ToString();
    }

    private static void DecodeIntoBuilder(ReadOnlySpan<byte> encodedText, StringBuilder builder, bool stopAtTerminator)
    {
        for (var index = 0; index < encodedText.Length; index++)
        {
            var value = encodedText[index];
            if (value < CharacterTable.Length)
            {
                builder.Append(CharacterTable[value]);
                continue;
            }

            switch (value)
            {
                case 0xF7:
                    EnsureRemainingBytes(encodedText, index, 2);
                    builder.Append(CultureInfo.InvariantCulture, $"<SPEED:{encodedText[index + 1]}>");
                    index += 1;
                    break;
                case 0xF8:
                    builder.Append("<I>");
                    break;
                case 0xF9:
                    EnsureRemainingBytes(encodedText, index, 2);
                    builder.Append(CultureInfo.InvariantCulture, $"<MEM:{encodedText[index + 1]}>");
                    index += 1;
                    break;
                case 0xFA:
                    EnsureRemainingBytes(encodedText, index, 2);
                    builder.Append("<?>");
                    index += 1;
                    break;
                case 0xFB:
                    EnsureRemainingBytes(encodedText, index, 4);
                    builder.Append(CultureInfo.InvariantCulture, $"<PORTRAIT:{encodedText[index + 1]}, {encodedText[index + 2]}, {encodedText[index + 3]}>");
                    index += 3;
                    break;
                case 0xFC:
                    builder.Append("<NB>");
                    break;
                case 0xFD:
                    builder.Append("<NL>");
                    break;
                case 0xFE:
                    EnsureRemainingBytes(encodedText, index, 2);
                    builder.Append("<ENDLST>");
                    index += 1;
                    if (stopAtTerminator)
                    {
                        return;
                    }

                    break;
                case 0xFF:
                    EnsureRemainingBytes(encodedText, index, 2);
                    builder.Append(CultureInfo.InvariantCulture, $"<END:{encodedText[index + 1]}>");
                    index += 1;
                    if (stopAtTerminator)
                    {
                        return;
                    }

                    break;
                default:
                    builder.Append(CultureInfo.InvariantCulture, $"<0x{value:X2}>");
                    break;
            }
        }

        if (stopAtTerminator)
        {
            throw new InvalidDataException("Reached the end of the ROM before a message terminator was found.");
        }
    }

    private static void EncodeCommand(string command, ICollection<byte> output)
    {
        if (command.StartsWith("SPEED:", StringComparison.Ordinal))
        {
            output.Add(0xF7);
            output.Add(ParseSingleByteArgument(command));
            return;
        }

        if (string.Equals(command, "I", StringComparison.Ordinal))
        {
            output.Add(0xF8);
            return;
        }

        if (command.StartsWith("MEM:", StringComparison.Ordinal))
        {
            output.Add(0xF9);
            output.Add(ParseSingleByteArgument(command));
            return;
        }

        if (string.Equals(command, "?", StringComparison.Ordinal))
        {
            output.Add(0xFA);
            output.Add(0x00);
            return;
        }

        if (command.StartsWith("PORTRAIT:", StringComparison.Ordinal))
        {
            var arguments = command["PORTRAIT:".Length..]
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            if (arguments.Length != 3)
            {
                throw new FormatException("The PORTRAIT command requires exactly three numeric arguments.");
            }

            output.Add(0xFB);
            foreach (var argument in arguments)
            {
                output.Add(ParseByte(argument));
            }

            return;
        }

        if (string.Equals(command, "NB", StringComparison.Ordinal))
        {
            output.Add(0xFC);
            return;
        }

        if (string.Equals(command, "NL", StringComparison.Ordinal))
        {
            output.Add(0xFD);
            return;
        }

        if (string.Equals(command, "ENDLST", StringComparison.Ordinal))
        {
            output.Add(0xFE);
            output.Add(0x00);
            return;
        }

        if (command.StartsWith("END:", StringComparison.Ordinal))
        {
            output.Add(0xFF);
            output.Add(ParseSingleByteArgument(command));
            return;
        }

        throw new FormatException($"Command '{command}' is not recognized.");
    }

    private static byte ParseSingleByteArgument(string command)
    {
        var separatorIndex = command.IndexOf(':');
        if (separatorIndex < 0 || separatorIndex == command.Length - 1)
        {
            throw new FormatException($"Command '{command}' requires a numeric argument.");
        }

        return ParseByte(command[(separatorIndex + 1)..]);
    }

    private static byte ParseByte(string value)
    {
        if (!byte.TryParse(value.Trim(), CultureInfo.InvariantCulture, out var parsed))
        {
            throw new FormatException($"'{value}' is not a valid byte value.");
        }

        return parsed;
    }

    private static int GetEncodedLength(byte value) =>
        value switch
        {
            < 0x4F => 1,
            0xF7 or 0xF9 or 0xFA or 0xFE or 0xFF => 2,
            0xF8 or 0xFC or 0xFD => 1,
            0xFB => 4,
            _ => 1
        };

    private static void EnsureRemainingBytes(ReadOnlySpan<byte> data, int offset, int requiredLength)
    {
        if (offset + requiredLength > data.Length)
        {
            throw new InvalidDataException("The message data ended unexpectedly.");
        }
    }
}
