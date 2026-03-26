using Medabots.Rom.Metadata;

namespace Medabots.Rom.Battles;

public sealed class BattleTableReader
{
    public const int BattleSize = MedabotsRomSchema.BattleSize;

    public IReadOnlyList<BattleDefinition> ReadAll(RomFile romFile, MedabotsRomTextProfile profile)
    {
        ArgumentNullException.ThrowIfNull(romFile);
        ArgumentNullException.ThrowIfNull(profile);

        var battles = new List<BattleDefinition>(profile.BattleCount);

        for (var battleId = 0; battleId < profile.BattleCount; battleId++)
        {
            var pointerOffset = profile.BattlePointerTableOffset + (battleId * sizeof(uint));
            if (!GbaPointer.TryReadFileOffset(romFile.Data, pointerOffset, out var dataOffset))
            {
                throw new InvalidDataException($"Battle pointer {battleId} at 0x{pointerOffset:X} is invalid.");
            }

            battles.Add(ReadSingle(romFile, battleId, pointerOffset, dataOffset));
        }

        return battles;
    }

    public BattleDefinition ReadSingle(RomFile romFile, int battleId, int pointerOffset, int dataOffset)
    {
        ArgumentNullException.ThrowIfNull(romFile);

        var data = romFile.ReadBytes(dataOffset, BattleSize).Span;
        var bots = new List<BattleBot>(MedabotsRomSchema.BattleBotCount);

        for (var index = 0; index < MedabotsRomSchema.BattleBotCount; index++)
        {
            var botOffset = MedabotsRomSchema.BattleBotOffset + (index * MedabotsRomSchema.BattleBotSize);
            bots.Add(new BattleBot(
                data[botOffset],
                data[botOffset + 1],
                data[botOffset + 2],
                data[botOffset + 3],
                data[botOffset + 4],
                data[botOffset + 5],
                data[botOffset + 6],
                data[botOffset + 7],
                data[botOffset + 8],
                data[botOffset + 9],
                data[botOffset + 10],
                data[botOffset + 11]));
        }

        return new BattleDefinition(
            battleId,
            pointerOffset,
            dataOffset,
            data[0],
            data[1],
            data[2],
            bots,
            data[MedabotsRomSchema.BattleFooterOffset]);
    }

    public static byte[] Serialize(BattleDefinition battle)
    {
        ArgumentNullException.ThrowIfNull(battle);
        if (battle.Bots.Count != MedabotsRomSchema.BattleBotCount)
        {
            throw new InvalidOperationException($"A battle must contain exactly {MedabotsRomSchema.BattleBotCount} bot slots.");
        }

        var data = new byte[BattleSize];
        data[0] = battle.CharacterId;
        data[1] = battle.Unknown1;
        data[2] = battle.NumberOfBots;

        for (var index = 0; index < battle.Bots.Count; index++)
        {
            var bot = battle.Bots[index];
            var offset = MedabotsRomSchema.BattleBotOffset + (index * MedabotsRomSchema.BattleBotSize);
            data[offset] = bot.Unknown;
            data[offset + 1] = bot.HeadPartId;
            data[offset + 2] = bot.RightArmPartId;
            data[offset + 3] = bot.LeftArmPartId;
            data[offset + 4] = bot.LegsPartId;
            data[offset + 5] = bot.MedalId;
            data[offset + 6] = bot.MedalLevel;
            data[offset + 7] = bot.Unknown1;
            data[offset + 8] = bot.Unknown2;
            data[offset + 9] = bot.Unknown3;
            data[offset + 10] = bot.Unknown4;
            data[offset + 11] = bot.Unknown5;
        }

        data[MedabotsRomSchema.BattleFooterOffset] = battle.AlwaysZero;
        return data;
    }
}
