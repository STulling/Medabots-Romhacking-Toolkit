using Medabots.Rom.Events;
using Medabots.Rom.Metadata;
using Xunit;

namespace Medabots.Rom.Tests;

public sealed class EventScriptRomReaderTests
{
    [Fact]
    public async Task Reader_CanParseRealEventFromLocalRokushoRom()
    {
        var romPath = TestRomLocator.FindWorkspaceRom();
        var rom = await RomFile.LoadAsync(romPath);
        var profile = MedabotsRomTextProfiles.Detect(rom);

        Assert.NotNull(profile);

        var reader = new EventScriptReader();
        var script = reader.ReadById(rom, profile!, 0);

        Assert.NotEmpty(script.Instructions);
        Assert.True(script.StartOffset > 0);
        Assert.Contains(script.Instructions, instruction => instruction.IsTerminal || instruction.Name.Contains("Message", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Reader_UsesDecodedStartBattleArgumentNames()
    {
        var romPath = TestRomLocator.FindWorkspaceRom();
        var rom = await RomFile.LoadAsync(romPath);
        var profile = MedabotsRomTextProfiles.Detect(rom);

        Assert.NotNull(profile);

        var reader = new EventScriptReader();
        var script = reader.ReadById(rom, profile!, 40);
        var startBattles = script.Instructions.Where(instruction => instruction.Name == "Start_Battle").ToArray();

        Assert.Equal(3, startBattles.Length);

        foreach (var startBattle in startBattles)
        {
            Assert.Equal(0x33, startBattle.Opcode);
            Assert.Collection(
                startBattle.Arguments,
                argument => Assert.Equal("battle", argument.Name),
                argument => Assert.Equal("battle_mode_flags", argument.Name),
                argument => Assert.Equal("post_battle_mode_flags", argument.Name));
        }

        Assert.Equal(new[] { 18, 15, 12 }, startBattles.Select(instruction => instruction.Arguments[0].RawValue).ToArray());
        Assert.All(startBattles, instruction => Assert.Equal(1, instruction.Arguments[1].RawValue));
        Assert.All(startBattles, instruction => Assert.Equal(0, instruction.Arguments[2].RawValue));
    }

    [Fact]
    public async Task Reader_PreservesObservedStartBattleSpecialPostBattleModes()
    {
        var romPath = TestRomLocator.FindWorkspaceRom();
        var rom = await RomFile.LoadAsync(romPath);
        var profile = MedabotsRomTextProfiles.Detect(rom);

        Assert.NotNull(profile);

        var reader = new EventScriptReader();

        var event780 = reader.ReadById(rom, profile!, 780);
        var event916 = reader.ReadById(rom, profile!, 916);
        var event15 = reader.ReadById(rom, profile!, 15);

        Assert.Contains(event780.Instructions, instruction =>
            instruction.Name == "Start_Battle" &&
            instruction.Arguments[2].Name == "post_battle_mode_flags" &&
            instruction.Arguments[2].RawValue == 0x10);

        Assert.Contains(event916.Instructions, instruction =>
            instruction.Name == "Start_Battle" &&
            instruction.Arguments[2].Name == "post_battle_mode_flags" &&
            instruction.Arguments[2].RawValue == 0x21);

        Assert.Contains(event15.Instructions, instruction =>
            instruction.Name == "Start_Battle" &&
            instruction.Arguments[2].Name == "post_battle_mode_flags" &&
            instruction.Arguments[2].RawValue == 0x01);
    }

    [Fact]
    public async Task Reader_DistinguishesPackedActorIdsFromTrackedObjectSlots()
    {
        var romPath = TestRomLocator.FindWorkspaceRom();
        var rom = await RomFile.LoadAsync(romPath);
        var profile = MedabotsRomTextProfiles.Detect(rom);

        Assert.NotNull(profile);

        var reader = new EventScriptReader();
        var script = reader.ReadById(rom, profile!, 151);

        var initiateActor = script.Instructions[8];
        var moveActor = script.Instructions[9];
        var unloadActor = script.Instructions[18];

        Assert.Equal("Initiate_Actor", initiateActor.Name);
        Assert.Equal("packed_actor_id", initiateActor.Arguments[0].Name);
        Assert.Equal(0x80, initiateActor.Arguments[0].RawValue);

        Assert.Equal("Move_Actor_A", moveActor.Name);
        Assert.Equal("tracked_object_slot", moveActor.Arguments[0].Name);
        Assert.Equal(0, moveActor.Arguments[0].RawValue);

        Assert.Equal("Unload_Actor", unloadActor.Name);
        Assert.Equal("packed_actor_id", unloadActor.Arguments[0].Name);
        Assert.Equal(0x80, unloadActor.Arguments[0].RawValue);
    }

    [Fact]
    public async Task Reader_EmitsTypedAstInstructionsForKnownOpcodeFamilies()
    {
        var romPath = TestRomLocator.FindWorkspaceRom();
        var rom = await RomFile.LoadAsync(romPath);
        var profile = MedabotsRomTextProfiles.Detect(rom);

        Assert.NotNull(profile);

        var reader = new EventScriptReader();

        var event151 = reader.ReadById(rom, profile!, 151);
        Assert.IsType<ShowMessageInstruction>(event151.Instructions[1]);
        Assert.IsType<InitiateActorInstruction>(event151.Instructions[8]);
        Assert.IsType<MoveActorInstruction>(event151.Instructions[9]);
        Assert.IsType<UnloadActorInstruction>(event151.Instructions[18]);

        var event40 = reader.ReadById(rom, profile!, 40);
        Assert.Contains(event40.Instructions, instruction => instruction is StartBattleInstruction);
    }

    [Fact]
    public async Task Reader_ParsesFocusEventObjectWithPackedTargetId()
    {
        var romPath = TestRomLocator.FindWorkspaceRom();
        var rom = await RomFile.LoadAsync(romPath);
        var profile = MedabotsRomTextProfiles.Detect(rom);

        Assert.NotNull(profile);

        var reader = new EventScriptReader();
        var script = reader.ReadById(rom, profile!, 40);
        var focusInstruction = script.Instructions[56];

        Assert.Equal("Focus_Event_Object", focusInstruction.Name);
        Assert.Collection(
            focusInstruction.Arguments,
            argument => Assert.Equal("target_mode", argument.Name),
            argument => Assert.Equal("target_packed_object_id", argument.Name));
        Assert.Equal(0, focusInstruction.Arguments[0].RawValue);
        Assert.Equal(0x1900, focusInstruction.Arguments[1].RawValue);
    }
}
