using Medabots.Rom.Metadata;

namespace Medabots.Rom.Battles;

public static class BattleSpecialityTemplateHelper
{
    public const int CycleSlotCount = 8;

    public static byte[] UnpackCycleEntries(BattleBot bot)
    {
        ArgumentNullException.ThrowIfNull(bot);

        return
        [
            (byte)(bot.PackedSpecialitySeedByte0 >> 4),
            (byte)(bot.PackedSpecialitySeedByte0 & 0x0F),
            (byte)(bot.PackedSpecialitySeedByte1 >> 4),
            (byte)(bot.PackedSpecialitySeedByte1 & 0x0F),
            (byte)(bot.PackedSpecialitySeedByte2 >> 4),
            (byte)(bot.PackedSpecialitySeedByte2 & 0x0F),
            (byte)(bot.PackedSpecialitySeedByte3 >> 4),
            (byte)(bot.PackedSpecialitySeedByte3 & 0x0F)
        ];
    }

    public static byte[] PackCycleEntries(IReadOnlyList<byte> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count != CycleSlotCount)
        {
            throw new ArgumentException($"Exactly {CycleSlotCount} cycle entries are required.", nameof(entries));
        }

        return
        [
            PackByte(entries[0], entries[1]),
            PackByte(entries[2], entries[3]),
            PackByte(entries[4], entries[5]),
            PackByte(entries[6], entries[7])
        ];
    }

    public static byte[] ComputeScaledMedalSlotValues(RomFile romFile, byte medalId, byte medalLevel)
    {
        ArgumentNullException.ThrowIfNull(romFile);
        if (medalId >= MedabotsRomSchema.MedalCount)
        {
            return Enumerable.Repeat((byte)1, CycleSlotCount).ToArray();
        }

        var medalInfoOffset = MedabotsRomSchema.MedalInfoTableOffset + (medalId * MedabotsRomSchema.MedalInfoSize);
        var medalClass = romFile.Data[medalInfoOffset + 1];
        var baseOffset = MedabotsRomSchema.MedalClassSpecialityBaseTableOffset + (medalClass * CycleSlotCount);
        var multiplier = medalLevel >> 1;
        var values = new byte[CycleSlotCount];

        for (var index = 0; index < values.Length; index++)
        {
            var baseValue = romFile.Data[baseOffset + index];
            var scaled = multiplier != 0 ? baseValue * multiplier : baseValue;
            if (scaled == 0)
            {
                scaled = 1;
            }
            else if (scaled > 100)
            {
                scaled = 100;
            }

            values[index] = (byte)scaled;
        }

        return values;
    }

    private static byte PackByte(byte highNibble, byte lowNibble)
    {
        if (highNibble > 0x0F || lowNibble > 0x0F)
        {
            throw new ArgumentOutOfRangeException(nameof(highNibble), "Cycle entries must fit in 4 bits.");
        }

        return (byte)((highNibble << 4) | lowNibble);
    }
}
