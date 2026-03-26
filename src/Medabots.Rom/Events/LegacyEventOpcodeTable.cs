namespace Medabots.Rom.Events;

internal static class LegacyEventOpcodeTable
{
    public static ReadOnlySpan<byte> Lengths =>
    [
        1,4,4,1,2,1,1,2,2,2,2,6,6,2,1,2,
        5,2,3,3,1,1,3,4,3,3,3,1,2,1,2,6,
        6,5,3,3,3,2,3,4,2,2,2,6,2,3,4,5,
        3,2,2,4,4,2,3,3,4,4,3,3,2,2,3,3,
        2,2,1,3,2,2,1,1,2,1,3,4,4,3,4,2,
        4,3,3,3,1,1,1,1,1,2,2,2,3,2,2,2,
        4,1,1,4,1,1,3,3,3,3,1,2,6,3,6,3,
        1,1,1,1,1,1,1,1,3,2,1,4,1,2,2,2,
        1,9,1,3,2,1,9,1,3
    ];

    public static int GetLength(byte opcode)
    {
        if (opcode >= Lengths.Length)
        {
            throw new InvalidDataException($"Opcode 0x{opcode:X2} is outside the known legacy opcode table.");
        }

        return Lengths[opcode];
    }
}
