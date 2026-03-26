using System.Buffers.Binary;

namespace Medabots.Rom.Text;

public sealed class MedabotsTextPatcher
{
    private readonly MedabotsTextCodec _codec;

    public MedabotsTextPatcher(MedabotsTextCodec? codec = null)
    {
        _codec = codec ?? new MedabotsTextCodec();
    }

    public IReadOnlyList<RomPatchAction> BuildPatchActions(
        RomFile romFile,
        int pointerTableOffset,
        int dumpOffset,
        IEnumerable<MessagePatch> patches)
    {
        ArgumentNullException.ThrowIfNull(romFile);
        ArgumentOutOfRangeException.ThrowIfNegative(pointerTableOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(dumpOffset);
        ArgumentNullException.ThrowIfNull(patches);

        var orderedPatches = patches
            .GroupBy(patch => patch.Id)
            .Select(group =>
            {
                if (group.Count() > 1)
                {
                    throw new InvalidOperationException($"Message '{group.Key}' is defined more than once.");
                }

                return group.Single();
            })
            .OrderBy(patch => patch.Id.Bank)
            .ThenBy(patch => patch.Id.Index)
            .ToArray();

        var actions = new List<RomPatchAction>(orderedPatches.Length * 2);
        var currentDumpOffset = dumpOffset;
        var pointerBytes = new byte[sizeof(uint)];

        foreach (var patch in orderedPatches)
        {
            var encodedText = _codec.Encode(patch.Text);
            EnsureRangeFits(romFile.Length, currentDumpOffset, encodedText.Length);

            actions.Add(RomPatchAction.Create(currentDumpOffset, encodedText, $"Write message {patch.Id}"));

            var pointerSlotOffset = ResolveMessagePointerOffset(romFile, pointerTableOffset, patch.Id);
            BinaryPrimitives.WriteUInt32LittleEndian(pointerBytes, GbaPointer.ToRomAddress(currentDumpOffset));
            actions.Add(RomPatchAction.Create(pointerSlotOffset, pointerBytes, $"Repoint message {patch.Id}"));

            currentDumpOffset += encodedText.Length;
        }

        return actions;
    }

    public void Apply(RomHackSession session, int pointerTableOffset, int dumpOffset, IEnumerable<MessagePatch> patches)
    {
        ArgumentNullException.ThrowIfNull(session);

        foreach (var action in BuildPatchActions(session.RomFile, pointerTableOffset, dumpOffset, patches))
        {
            session.ApplyPatch(action);
        }
    }

    private static int ResolveMessagePointerOffset(RomFile romFile, int pointerTableOffset, MessageId messageId)
    {
        var romData = romFile.Data.AsSpan();
        if (!GbaPointer.TryReadFileOffset(romData, pointerTableOffset + (messageId.Bank * sizeof(uint)), out var bankTableOffset))
        {
            throw new InvalidOperationException($"The message bank '{messageId.Bank}' does not exist.");
        }

        var pointerSlotOffset = bankTableOffset + (messageId.Index * sizeof(uint));
        EnsureRangeFits(romFile.Length, pointerSlotOffset, sizeof(uint));
        return pointerSlotOffset;
    }

    private static void EnsureRangeFits(int romLength, int offset, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        if (offset + length > romLength)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "The patch exceeds the ROM size.");
        }
    }
}
