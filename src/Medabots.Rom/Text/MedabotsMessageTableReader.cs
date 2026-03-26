using Medabots.Rom.Metadata;

namespace Medabots.Rom.Text;

public sealed class MedabotsMessageTableReader
{
    private readonly MedabotsTextCodec _codec;

    public MedabotsMessageTableReader(MedabotsTextCodec? codec = null)
    {
        _codec = codec ?? new MedabotsTextCodec();
    }

    public IReadOnlyDictionary<MessageId, string> ReadAll(RomFile romFile, int pointerTableOffset, int bankCount = MedabotsRomSchema.MessageBankCount)
    {
        ArgumentNullException.ThrowIfNull(romFile);
        ArgumentOutOfRangeException.ThrowIfNegative(pointerTableOffset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bankCount);

        var romData = romFile.Data;
        var bankMessages = new List<KeyValuePair<MessageId, string>>[bankCount];

        Parallel.For(0, bankCount, bank =>
        {
            if (!GbaPointer.TryReadFileOffset(romData.AsSpan(), pointerTableOffset + (bank * sizeof(uint)), out var bankTableOffset))
            {
                bankMessages[bank] = [];
                return;
            }

            var messagesForBank = new List<KeyValuePair<MessageId, string>>();
            for (var index = 0; ; index++)
            {
                if (!GbaPointer.TryReadFileOffset(romData.AsSpan(), bankTableOffset + (index * sizeof(uint)), out var messageOffset))
                {
                    break;
                }

                messagesForBank.Add(new KeyValuePair<MessageId, string>(new MessageId(bank, index), _codec.DecodeMessage(romData.AsSpan(), messageOffset)));
            }

            bankMessages[bank] = messagesForBank;
        });

        var totalCount = 0;
        for (var bank = 0; bank < bankCount; bank++)
        {
            totalCount += bankMessages[bank].Count;
        }

        var messages = new Dictionary<MessageId, string>(totalCount);
        for (var bank = 0; bank < bankCount; bank++)
        {
            foreach (var pair in bankMessages[bank])
            {
                messages[pair.Key] = pair.Value;
            }
        }

        return messages;
    }
}
