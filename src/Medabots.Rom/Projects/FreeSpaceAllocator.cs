namespace Medabots.Rom.Projects;

public sealed class FreeSpaceAllocator
{
    private int _nextOffset;

    public FreeSpaceAllocator(int initialOffset)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(initialOffset);
        _nextOffset = initialOffset;
    }

    public int CurrentOffset => _nextOffset;

    public int Reserve(int length, int alignment = 4)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(alignment);

        var offset = AlignUp(_nextOffset, alignment);
        _nextOffset = offset + length;
        return offset;
    }

    public void EnsureAtLeast(int offset)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        if (offset > _nextOffset)
        {
            _nextOffset = offset;
        }
    }

    public static int AlignUp(int value, int alignment)
    {
        var remainder = value % alignment;
        return remainder == 0 ? value : value + (alignment - remainder);
    }
}
