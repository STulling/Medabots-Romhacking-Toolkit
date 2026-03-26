namespace Medabots.Rom.Metadata;

public sealed class RomPatternLocator
{
    public RomPatternLocator(string name, IReadOnlyList<byte> signature, int resultAdjustment = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(signature);

        if (signature.Count == 0)
        {
            throw new ArgumentException("A ROM signature must contain at least one byte.", nameof(signature));
        }

        Name = name;
        Signature = signature.ToArray();
        ResultAdjustment = resultAdjustment;
    }

    public string Name { get; }

    public byte[] Signature { get; }

    public int ResultAdjustment { get; }

    public int Locate(byte[] romData)
    {
        ArgumentNullException.ThrowIfNull(romData);

        var offset = Search(romData, Signature);
        if (offset < 0)
        {
            throw new InvalidDataException($"Could not locate {Name} in the ROM.");
        }

        var result = offset + ResultAdjustment;
        if (result < 0)
        {
            throw new InvalidDataException($"{Name} resolved to an invalid negative offset.");
        }

        return result;
    }

    private static int Search(byte[] haystack, byte[] needle)
    {
        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return i;
            }
        }

        return -1;
    }
}
