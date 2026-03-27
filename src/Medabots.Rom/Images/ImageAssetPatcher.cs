namespace Medabots.Rom.Images;

public sealed partial class ImageAssetPatcher
{
    public const int DefaultExpansionStartOffset = 0x800000;

    private static int ResolveWriteOffset(RomFile romFile, int currentOffset, int newLength, int expansionStartOffset, Func<byte[], int, int?>? encodedLengthReader)
    {
        if (currentOffset > 0)
        {
            var existingLength = encodedLengthReader is null ? newLength : encodedLengthReader(romFile.Data, currentOffset);
            if (existingLength is int length && newLength <= length)
            {
                return currentOffset;
            }
        }

        var baseOffset = Math.Max(expansionStartOffset, romFile.Length);
        return Align(baseOffset, 4);
    }

    private static int Align(int value, int alignment)
    {
        var mask = alignment - 1;
        return (value + mask) & ~mask;
    }
}
