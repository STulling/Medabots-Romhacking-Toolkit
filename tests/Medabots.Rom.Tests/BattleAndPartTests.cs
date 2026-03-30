using System.Buffers.Binary;
using Medabots.Rom.Battles;
using Medabots.Rom.Metadata;
using Medabots.Rom.Parts;
using Xunit;

namespace Medabots.Rom.Tests;

public sealed class BattleAndPartTests
{
    [Fact]
    public async Task BattleReader_LoadsRealBattlesFromLocalRokushoRom()
    {
        var rom = await RomFile.LoadAsync(FindWorkspaceRom());
        var profile = MedabotsRomTextProfiles.Detect(rom);

        Assert.NotNull(profile);

        var battles = new BattleTableReader().ReadAll(rom, profile!);

        Assert.Equal(profile!.BattleCount, battles.Count);
        Assert.All(battles, battle => Assert.Equal(3, battle.Bots.Count));
        Assert.Contains(battles, battle => battle.NumberOfBots > 0);
    }

    [Fact]
    public async Task BattleReader_ParsesBattleZeroUsingRealHeaderAndBotOffsets()
    {
        var rom = await RomFile.LoadAsync(FindWorkspaceRom());
        var profile = MedabotsRomTextProfiles.Detect(rom);

        Assert.NotNull(profile);

        var battle = new BattleTableReader().ReadSingle(rom, 0, profile!.BattlePointerTableOffset, 0x3BF530);

        Assert.Equal((byte)1, battle.CharacterId);
        Assert.Equal((byte)0, battle.InitializationMode);
        Assert.Equal((byte)1, battle.NumberOfBots);
        Assert.Equal((byte)1, battle.TemplateFlags);

        var bot = battle.Bots[0];
        Assert.Equal((byte)0, bot.HeadPartId);
        Assert.Equal((byte)0, bot.RightArmPartId);
        Assert.Equal((byte)0, bot.LeftArmPartId);
        Assert.Equal((byte)0, bot.LegsPartId);
        Assert.Equal((byte)15, bot.MedalId);
        Assert.Equal((byte)0, bot.MedalLevel);
        Assert.Equal((byte)18, bot.PackedSpecialitySeedByte0);
        Assert.Equal((byte)49, bot.PackedSpecialitySeedByte1);
        Assert.Equal((byte)35, bot.PackedSpecialitySeedByte2);
        Assert.Equal((byte)16, bot.PackedSpecialitySeedByte3);
        Assert.Equal((byte)5, bot.SpecialityCycleResetValue);
        Assert.Equal((byte)0, bot.ReservedZeroByte);
    }

    [Fact]
    public void BattleSpecialityTemplateHelper_UnpacksAndRepacksCycleEntries()
    {
        var bot = new BattleBot(0, 0, 0, 0, 15, 0, 0x12, 0x31, 0x23, 0x10, 5, 0);

        var entries = BattleSpecialityTemplateHelper.UnpackCycleEntries(bot);

        Assert.Equal(new byte[] { 1, 2, 3, 1, 2, 3, 1, 0 }, entries);
        Assert.Equal(new byte[] { 0x12, 0x31, 0x23, 0x10 }, BattleSpecialityTemplateHelper.PackCycleEntries(entries));
    }

    [Fact]
    public async Task BattleSpecialityTemplateHelper_ComputesScaledMedalSlotValuesFromRealRom()
    {
        var rom = await RomFile.LoadAsync(FindWorkspaceRom());

        var scaledValues = BattleSpecialityTemplateHelper.ComputeScaledMedalSlotValues(rom, 15, 30);

        Assert.Equal(new byte[] { 30, 30, 45, 45, 15, 30, 15, 30 }, scaledValues);
    }

    [Fact]
    public async Task PartReader_LoadsRealPartsFromLocalRokushoRom()
    {
        var rom = await RomFile.LoadAsync(FindWorkspaceRom());
        var parts = new PartTableReader().ReadAll(rom);

        Assert.Equal(PartTableReader.PartCount, parts.Count);
        Assert.Equal(PartKind.Head, parts[0].Kind);
        Assert.Equal(PartKind.RightArm, parts[1].Kind);
        Assert.Equal(PartKind.LeftArm, parts[2].Kind);
        Assert.Equal(PartKind.Legs, parts[3].Kind);
    }

    [Fact]
    public async Task PartReader_UsesRealTechniqueBytesForSacrificeSet()
    {
        var rom = await RomFile.LoadAsync(FindWorkspaceRom());
        var parts = new PartTableReader().ReadAll(rom);

        Assert.Equal("Sacrifice Head", MedabotsMetadata.Default.GetPartName(460));
        Assert.Equal("Sacrifice Hand", MedabotsMetadata.Default.GetPartName(461));
        Assert.Equal("Sacrifice Arm", MedabotsMetadata.Default.GetPartName(462));

        Assert.Equal((byte)0x3B, parts[460].TechniqueOrLegType);
        Assert.Equal((byte)0x3B, parts[461].TechniqueOrLegType);
        Assert.Equal((byte)0x3B, parts[462].TechniqueOrLegType);
        Assert.Equal("HealChange", MedabotsMetadata.Default.GetTechniqueName(parts[462].TechniqueOrLegType));
    }

    [Fact]
    public async Task PartReader_Unknown3_IsUniformAcrossEachMedabotQuartet()
    {
        var rom = await RomFile.LoadAsync(FindWorkspaceRom());
        var parts = new PartTableReader().ReadAll(rom);

        for (var botId = 0; botId < parts.Count / 4; botId++)
        {
            var quartet = parts.Skip(botId * 4).Take(4).ToArray();
            Assert.Equal(4, quartet.Length);
            Assert.All(quartet, part => Assert.Equal(quartet[0].Unknown3, part.Unknown3));
        }
    }

    [Fact]
    public async Task PartReader_TailBytes_AreZeroExceptDummyNoHeadRecord()
    {
        var rom = await RomFile.LoadAsync(FindWorkspaceRom());
        var parts = new PartTableReader().ReadAll(rom);

        Assert.Equal("No Head", MedabotsMetadata.Default.GetPartName(480));

        for (var i = 0; i < parts.Count; i++)
        {
            var part = parts[i];
            if (i == 480)
            {
                Assert.Equal((byte)5, part.Unknown6);
                Assert.Equal((byte)18, part.Unknown7);
                Assert.Equal((byte)0, part.Unknown8);
                continue;
            }

            Assert.Equal((byte)0, part.Unknown6);
            Assert.Equal((byte)0, part.Unknown7);
            Assert.Equal((byte)0, part.Unknown8);
        }
    }

    [Fact]
    public async Task BattleActionOpcodeTableReader_LoadsOpcodeHandlersFromLocalRokushoRom()
    {
        var rom = await RomFile.LoadAsync(FindWorkspaceRom());

        var opcodes = new BattleActionOpcodeTableReader().ReadAll(rom);

        Assert.Equal(MedabotsRomSchema.BattleActionOpcodeCount, opcodes.Count);
        Assert.Equal((byte)0x01, opcodes[1].Opcode);
        Assert.Equal(MedabotsRomSchema.BattleActionOpcodeHandlerTableOffset + sizeof(uint), opcodes[1].PointerOffset);
        Assert.NotEqual(0u, opcodes[1].HandlerRomAddress);
        Assert.True(opcodes[1].HandlerOffset > 0);
    }

    [Fact]
    public async Task BattleActionScriptTableReader_LoadsRealScriptPointersFromLocalRokushoRom()
    {
        var rom = await RomFile.LoadAsync(FindWorkspaceRom());

        var scripts = new BattleActionScriptTableReader().ReadAll(rom);

        Assert.Equal(MedabotsRomSchema.BattleActionScriptCount, scripts.Count);
        Assert.Equal((byte)0x00, scripts[0].ActionScriptId);
        Assert.Equal(0x083C6A24u, scripts[0].ScriptRomAddress);
        Assert.Equal(0x3C6A24, scripts[0].ScriptOffset);
        Assert.True(scripts[0].ScriptLength > 0);
    }

    [Fact]
    public void BattleActionRegistry_LoadsKnownRoutesAndOpcodes()
    {
        var registry = BattleActionRegistry.LoadDefault();

        Assert.True(registry.TryGetOpcodeDefinition(0x05, out var opcode));
        Assert.Equal("DispatchPrimaryActionFamilyTable", opcode.Name);
        Assert.Equal(0, opcode.InlineArgumentCount);

        Assert.True(registry.TryGetOpcodeDefinition(0x03, out var choiceOpcode));
        Assert.Equal(4, choiceOpcode.InlineArgumentCount);

        Assert.True(registry.TryGetRoute(0x24, out var route));
        Assert.Equal("ActionFamily_Scout_Handler", route.FamilyHandler);
        Assert.Contains((byte)0x1F, route.KnownOpcodeSequence);

        Assert.True(registry.TryGetRoute(0x04, out var laserRoute));
        Assert.Equal("NoOpActionHandler", laserRoute.FamilyHandler);

        Assert.True(registry.TryGetRoute(0x01, out var hammerRoute));
        Assert.Equal("gSharedDirectAttackActionScript", hammerRoute.SharedScriptName);
        Assert.Contains(hammerRoute.ActualFlow, line => line.Contains("Berserk", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BattleActionRegistry_ParsesSharedDirectAttackScript()
    {
        var rom = await RomFile.LoadAsync(FindWorkspaceRom());
        var registry = BattleActionRegistry.LoadDefault();
        var opcodeTable = new BattleActionOpcodeTableReader().ReadAll(rom);
        var scriptTable = new BattleActionScriptTableReader().ReadAll(rom);

        var analysis = registry.Analyze(0x01, "Hammer", opcodeTable, scriptTable);

        Assert.NotNull(analysis.Script);
        Assert.Equal(0x083C6A24u, analysis.Script!.ScriptRomAddress);
        Assert.NotEmpty(analysis.ScriptNodes);
        Assert.Equal(0x04, analysis.ScriptNodes[0].Value);
        Assert.False(analysis.ScriptNodes[0].IsLabel);
        Assert.Equal("AdvanceCursor", analysis.ScriptNodes[0].DisplayName);
        Assert.Contains(analysis.ScriptNodes, node => node.IsLabel && node.Value == 0x80);
        Assert.Contains(analysis.ScriptNodes, node => !node.IsLabel && node.Value == 0x03 && node.InlineArguments.Count == 4);
    }

    [Fact]
    public void BattlePatcher_WritesSerializedBattleBytes()
    {
        var romBytes = new byte[512];
        BinaryPrimitives.WriteUInt32LittleEndian(romBytes.AsSpan(0x20, 4), GbaPointer.ToRomAddress(0x80));

        var battle = new BattleDefinition(
            0,
            0x20,
            0x80,
            4,
            0,
            2,
            1,
            [
                new BattleBot(1, 2, 3, 4, 5, 6, 21, 22, 23, 24, 25, 26),
                new BattleBot(7, 8, 9, 10, 11, 12, 31, 32, 33, 34, 35, 36),
                new BattleBot(13, 14, 15, 16, 17, 18, 41, 42, 43, 44, 45, 46)
            ]);

        var session = CreateSession(new RomFile("test.gba", romBytes));
        new BattlePatcher().Apply(session, battle);

        Assert.Equal(4, romBytes[0x80]);
        Assert.Equal(2, romBytes[0x82]);
        Assert.Equal(1, romBytes[0x83]);
        Assert.Equal(1, romBytes[0x84]);
        Assert.Equal(18, romBytes[0x80 + 4 + 24 + 5]);
        Assert.Equal(45, romBytes[0x80 + 4 + 24 + 10]);
        Assert.Equal(46, romBytes[0x80 + 4 + 24 + 11]);
    }

    [Fact]
    public void PartPatcher_WritesSerializedPartBytes()
    {
        var romBytes = new byte[256];
        var part = new PartDefinition(0, 0, PartKind.Head, 0x40, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16);

        var session = CreateSession(new RomFile("test.gba", romBytes));
        new PartPatcher().Apply(session, part);

        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 }, romBytes.Skip(0x40).Take(16).ToArray());
    }

    private static RomHackSession CreateSession(RomFile romFile)
    {
        var constructor = typeof(RomHackSession).GetConstructors(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Single(constructor => constructor.GetParameters() is [{ ParameterType: var parameterType }] && parameterType == typeof(RomFile));

        return (RomHackSession)constructor.Invoke([romFile]);
    }

    private static string FindWorkspaceRom()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "Medabots Rokusho Version (E).gba");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not find the local Rokusho ROM used for integration testing.");
    }
}
