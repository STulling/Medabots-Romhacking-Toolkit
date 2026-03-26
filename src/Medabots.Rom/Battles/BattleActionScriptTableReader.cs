using Medabots.Rom.Metadata;

namespace Medabots.Rom.Battles;

public sealed class BattleActionScriptTableReader
{
    public IReadOnlyList<BattleActionScriptEntry> ReadAll(RomFile romFile)
    {
        ArgumentNullException.ThrowIfNull(romFile);

        var pointerEntries = new List<(byte scriptId, int pointerOffset, uint romAddress, int scriptOffset)>(MedabotsRomSchema.BattleActionScriptCount);
        for (var scriptId = 0; scriptId < MedabotsRomSchema.BattleActionScriptCount; scriptId++)
        {
            var pointerOffset = MedabotsRomSchema.BattleActionScriptTableOffset + (scriptId * sizeof(uint));
            if (!GbaPointer.TryReadFileOffset(romFile.Data, pointerOffset, out var scriptOffset))
            {
                throw new InvalidDataException($"Battle action script pointer 0x{scriptId:X2} at 0x{pointerOffset:X} is invalid.");
            }

            var romAddress = BitConverter.ToUInt32(romFile.Data, pointerOffset);
            pointerEntries.Add(((byte)scriptId, pointerOffset, romAddress, scriptOffset));
        }

        var uniqueOffsets = pointerEntries
            .Select(entry => entry.scriptOffset)
            .Distinct()
            .OrderBy(offset => offset)
            .ToArray();

        var lengthsByOffset = new Dictionary<int, int>(uniqueOffsets.Length);
        for (var i = 0; i < uniqueOffsets.Length; i++)
        {
            var start = uniqueOffsets[i];
            var end = i + 1 < uniqueOffsets.Length
                ? uniqueOffsets[i + 1]
                : FindTerminalScriptEnd(romFile.Data, start);
            lengthsByOffset[start] = end - start;
        }

        return pointerEntries
            .Select(entry => new BattleActionScriptEntry(
                entry.scriptId,
                entry.pointerOffset,
                entry.romAddress,
                entry.scriptOffset,
                lengthsByOffset[entry.scriptOffset],
                romFile.ReadBytes(entry.scriptOffset, lengthsByOffset[entry.scriptOffset]).ToArray()))
            .ToArray();
    }

    private static int FindTerminalScriptEnd(ReadOnlySpan<byte> data, int start)
    {
        const int searchLimit = 0x400;
        var end = Math.Min(data.Length, start + searchLimit);

        for (var i = start; i + 1 < end; i++)
        {
            if (data[i] == 0x00 && data[i + 1] == 0x00)
            {
                return i;
            }
        }

        return end;
    }
}
