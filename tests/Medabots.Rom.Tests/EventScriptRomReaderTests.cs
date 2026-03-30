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
    public async Task Reader_ParsesRandomEncounterModeFlagsAsSingleByteOpcode()
    {
        var romPath = TestRomLocator.FindWorkspaceRom();
        var rom = await RomFile.LoadAsync(romPath);
        var profile = MedabotsRomTextProfiles.Detect(rom);

        Assert.NotNull(profile);

        var reader = new EventScriptReader();
        var script = reader.ReadById(rom, profile!, 40);
        var encounterFlagsInstruction = script.Instructions.Single(instruction => instruction.Offset == 0x78560C);
        var gotoInstruction = script.Instructions.Single(instruction => instruction.Offset == 0x78560E);

        Assert.Equal("Set_Random_Encounter_Mode_Flags", encounterFlagsInstruction.Name);
        Assert.Collection(
            encounterFlagsInstruction.Arguments,
            argument => Assert.Equal("flags", argument.Name));
        Assert.Equal(0, encounterFlagsInstruction.Arguments[0].RawValue);

        Assert.Equal("GOTO_EVENT", gotoInstruction.Name);
        Assert.Equal(43, gotoInstruction.Arguments[0].RawValue);
    }

    [Fact]
    public async Task Reader_ParsesFocusEventObjectWithPackedTargetId()
    {
        var romPath = TestRomLocator.FindWorkspaceRom();
        var rom = await RomFile.LoadAsync(romPath);
        var profile = MedabotsRomTextProfiles.Detect(rom);

        Assert.NotNull(profile);

        var reader = new EventScriptReader();
        var script = reader.ReadById(rom, profile!, 293);
        var focusInstruction = script.Instructions.Single(instruction => instruction.Offset == 0x782375);

        Assert.Equal(0x60, focusInstruction.Opcode);
        Assert.Equal("Focus_Event_Object", focusInstruction.Name);
        Assert.Collection(
            focusInstruction.Arguments,
            argument => Assert.Equal("target_mode", argument.Name),
            argument => Assert.Equal("target_packed_object_id", argument.Name));
        Assert.Equal(0, focusInstruction.Arguments[0].RawValue);
        Assert.Equal(0, focusInstruction.Arguments[1].RawValue);
    }

    [Fact]
    public async Task Reader_ParsesRestoreFocusedEventModeFlagsAsZeroArgumentOpcode()
    {
        var romPath = TestRomLocator.FindWorkspaceRom();
        var rom = await RomFile.LoadAsync(romPath);
        var profile = MedabotsRomTextProfiles.Detect(rom);

        Assert.NotNull(profile);

        var reader = new EventScriptReader();
        var script = reader.ReadById(rom, profile!, 315);
        var restoreInstruction = script.Instructions.Single(instruction => instruction.Offset == 0x782446);

        Assert.Equal(0x61, restoreInstruction.Opcode);
        Assert.Equal("Restore_Focused_Event_Mode_Flags", restoreInstruction.Name);
        Assert.Empty(restoreInstruction.Arguments);
    }

    [Fact]
    public async Task Reader_ParsesCompletePartSetSceneCommandGate()
    {
        var romPath = TestRomLocator.FindWorkspaceRom();
        var rom = await RomFile.LoadAsync(romPath);
        var profile = MedabotsRomTextProfiles.Detect(rom);

        Assert.NotNull(profile);

        var reader = new EventScriptReader();
        var script = reader.ReadById(rom, profile!, 56);
        var instruction = script.Instructions.Single(eventInstruction => eventInstruction.Offset == 0x77BB6A);

        Assert.Equal(0x78, instruction.Opcode);
        Assert.Equal("Run_Scene_Command_10_If_At_Least_Three_Complete_Part_Sets", instruction.Name);
        Assert.Collection(
            instruction.Arguments,
            argument => Assert.Equal("scene_arg", argument.Name),
            argument => Assert.Equal("jump_if_fewer_than_three_complete_sets", argument.Name));
        Assert.Equal(4, instruction.Arguments[0].RawValue);
        Assert.Equal(0x41, instruction.Arguments[1].RawValue);
    }

    [Fact]
    public async Task Reader_ParsesBatchedObjectCommandBlock()
    {
        var romPath = TestRomLocator.FindWorkspaceRom();
        var rom = await RomFile.LoadAsync(romPath);
        var profile = MedabotsRomTextProfiles.Detect(rom);

        Assert.NotNull(profile);

        var reader = new EventScriptReader();
        var script = reader.ReadById(rom, profile!, 877);
        var beginInstruction = script.Instructions.Single(eventInstruction => eventInstruction.Offset == 0x76FFC7);
        var queueInstruction = script.Instructions.Single(eventInstruction => eventInstruction.Offset == 0x76FFC8);
        var executeInstruction = script.Instructions.Single(eventInstruction => eventInstruction.Offset == 0x76FFDA);

        Assert.Equal("Begin_Batched_Object_Command_Block", beginInstruction.Name);
        Assert.Empty(beginInstruction.Arguments);

        Assert.Equal("Queue_Batched_Object_Command", queueInstruction.Name);
        Assert.Collection(
            queueInstruction.Arguments,
            argument => Assert.Equal("record_type", argument.Name),
            argument => Assert.Equal("object_id_high", argument.Name),
            argument => Assert.Equal("object_id_low", argument.Name),
            argument => Assert.Equal("arg4", argument.Name),
            argument => Assert.Equal("arg5", argument.Name),
            argument => Assert.Equal("arg6", argument.Name),
            argument => Assert.Equal("arg7", argument.Name),
            argument => Assert.Equal("arg8", argument.Name));
        Assert.Equal(1, queueInstruction.Arguments[0].RawValue);
        Assert.Equal(0, queueInstruction.Arguments[1].RawValue);
        Assert.Equal(3, queueInstruction.Arguments[2].RawValue);

        Assert.Equal("Execute_Batched_Object_Command_Block", executeInstruction.Name);
        Assert.Empty(executeInstruction.Arguments);
    }

    [Fact]
    public async Task Reader_ParsesOpcode83AsSetObjectRenderMode()
    {
        var romPath = TestRomLocator.FindWorkspaceRom();
        var rom = await RomFile.LoadAsync(romPath);
        var profile = MedabotsRomTextProfiles.Detect(rom);

        Assert.NotNull(profile);

        var reader = new EventScriptReader();
        var script = reader.ReadById(rom, profile!, 876);
        var renderModeInstruction = script.Instructions.Single(eventInstruction => eventInstruction.Offset == 0x76FDFF);

        Assert.Equal(0x83, renderModeInstruction.Opcode);
        Assert.Equal("Set_Object_Render_Mode", renderModeInstruction.Name);
        Assert.Collection(
            renderModeInstruction.Arguments,
            argument => Assert.Equal("target_flags", argument.Name),
            argument => Assert.Equal("render_mode", argument.Name));
        Assert.Equal(1, renderModeInstruction.Arguments[0].RawValue);
        Assert.Equal(1, renderModeInstruction.Arguments[1].RawValue);
    }

    [Theory]
    [InlineData((short)882, 0x770743, 1)]
    [InlineData((short)2237, 0x76F70A, 0)]
    [InlineData((short)2239, 0x76F6F2, 1)]
    public async Task Reader_ParsesOpcode84AsDelayedMapSceneVariantSetter(short eventId, int offset, int expectedVariant)
    {
        var romPath = TestRomLocator.FindWorkspaceRom();
        var rom = await RomFile.LoadAsync(romPath);
        var profile = MedabotsRomTextProfiles.Detect(rom);

        Assert.NotNull(profile);

        var reader = new EventScriptReader();
        var script = reader.ReadById(rom, profile!, eventId);
        var instruction = script.Instructions.Single(eventInstruction => eventInstruction.Offset == offset);

        Assert.Equal(0x84, instruction.Opcode);
        Assert.Equal("Set_Map_Scene_Variant_When_Player_Faces_Up", instruction.Name);
        Assert.Single(instruction.Arguments);
        Assert.Equal("variant", instruction.Arguments[0].Name);
        Assert.Equal(expectedVariant, instruction.Arguments[0].RawValue);
    }

    [Fact]
    public async Task Reader_ParsesOpcode4FAsOverworldSceneStateJump()
    {
        var romPath = TestRomLocator.FindWorkspaceRom();
        var rom = await RomFile.LoadAsync(romPath);
        var profile = MedabotsRomTextProfiles.Detect(rom);

        Assert.NotNull(profile);

        var reader = new EventScriptReader();
        var script = reader.ReadById(rom, profile!, 56);
        var sceneStateJump = script.Instructions.Single(eventInstruction => eventInstruction.Offset == 0x77BB6D);

        Assert.Equal(0x4F, sceneStateJump.Opcode);
        Assert.Equal("Jump_If_Previous_Scene_Command_Failed", sceneStateJump.Name);
        Assert.Single(sceneStateJump.Arguments);
        Assert.Equal("jump", sceneStateJump.Arguments[0].Name);
        Assert.Equal(0x2D, sceneStateJump.Arguments[0].RawValue);
    }

    [Fact]
    public async Task Reader_ParsesOpcode5AAsSecondaryMarkerFacingVariant()
    {
        var romPath = TestRomLocator.FindWorkspaceRom();
        var rom = await RomFile.LoadAsync(romPath);
        var profile = MedabotsRomTextProfiles.Detect(rom);

        Assert.NotNull(profile);

        var reader = new EventScriptReader();
        var script = reader.ReadById(rom, profile!, 296);
        var markerVariantInstruction = script.Instructions.Single(eventInstruction => eventInstruction.Offset == 0x78248F);

        Assert.Equal(0x5A, markerVariantInstruction.Opcode);
        Assert.Equal("Set_Secondary_Marker_Facing_Variant", markerVariantInstruction.Name);
        Assert.Single(markerVariantInstruction.Arguments);
        Assert.Equal("variant", markerVariantInstruction.Arguments[0].Name);
        Assert.Equal(0, markerVariantInstruction.Arguments[0].RawValue);
    }

    [Fact]
    public async Task Reader_ParsesOpcode5BAsCompletePartSetJump()
    {
        var romPath = TestRomLocator.FindWorkspaceRom();
        var rom = await RomFile.LoadAsync(romPath);
        var profile = MedabotsRomTextProfiles.Detect(rom);

        Assert.NotNull(profile);

        var reader = new EventScriptReader();
        var script = reader.ReadById(rom, profile!, 56);
        var jumpInstruction = script.Instructions.Single(eventInstruction => eventInstruction.Offset == 0x77BB53);

        Assert.Equal(0x5B, jumpInstruction.Opcode);
        Assert.Equal("Jump_If_Fewer_Than_Three_Complete_Part_Sets", jumpInstruction.Name);
        Assert.Single(jumpInstruction.Arguments);
        Assert.Equal("jump", jumpInstruction.Arguments[0].Name);
        Assert.Equal(0x58, jumpInstruction.Arguments[0].RawValue);
    }

    [Fact]
    public async Task Reader_ParsesOpcode5CAsMissingMonochromePartSetJump()
    {
        var romPath = TestRomLocator.FindWorkspaceRom();
        var rom = await RomFile.LoadAsync(romPath);
        var profile = MedabotsRomTextProfiles.Detect(rom);

        Assert.NotNull(profile);

        var reader = new EventScriptReader();
        var script = reader.ReadById(rom, profile!, 235);
        var jumpInstruction = script.Instructions.Single(eventInstruction => eventInstruction.Offset == 0x776ADF);

        Assert.Equal(0x5C, jumpInstruction.Opcode);
        Assert.Equal("Jump_If_Missing_Complete_Monochrome_Part_Set", jumpInstruction.Name);
        Assert.Collection(
            jumpInstruction.Arguments,
            argument => Assert.Equal("part_id", argument.Name),
            argument => Assert.Equal("jump", argument.Name));
        Assert.Equal(11, jumpInstruction.Arguments[0].RawValue);
        Assert.Equal(5, jumpInstruction.Arguments[1].RawValue);
    }

    [Fact]
    public async Task Reader_ParsesOpcode7BAsNoSparePartJump()
    {
        var romPath = TestRomLocator.FindWorkspaceRom();
        var rom = await RomFile.LoadAsync(romPath);
        var profile = MedabotsRomTextProfiles.Detect(rom);

        Assert.NotNull(profile);

        var reader = new EventScriptReader();
        var script = reader.ReadById(rom, profile!, 969);
        var jumpInstruction = script.Instructions.Single(eventInstruction => eventInstruction.Offset == 0x77D66E);

        Assert.Equal(0x7B, jumpInstruction.Opcode);
        Assert.Equal("Jump_If_No_Spare_Part", jumpInstruction.Name);
        Assert.Collection(
            jumpInstruction.Arguments,
            argument => Assert.Equal("bot", argument.Name),
            argument => Assert.Equal("part", argument.Name),
            argument => Assert.Equal("jump", argument.Name));
        Assert.Equal(32, jumpInstruction.Arguments[0].RawValue);
        Assert.Equal(1, jumpInstruction.Arguments[1].RawValue);
        Assert.Equal(46, jumpInstruction.Arguments[2].RawValue);
    }

    [Fact]
    public async Task Reader_ParsesOpcode7EAsTrackedActorMovementVariant()
    {
        var romPath = TestRomLocator.FindWorkspaceRom();
        var rom = await RomFile.LoadAsync(romPath);
        var profile = MedabotsRomTextProfiles.Detect(rom);

        Assert.NotNull(profile);

        var reader = new EventScriptReader();
        var script = reader.ReadById(rom, profile!, 1393);
        var moveInstruction = script.Instructions.Single(eventInstruction => eventInstruction.Offset == 0x7758C2);

        Assert.Equal(0x7E, moveInstruction.Opcode);
        Assert.Equal("Move_All_Tracked_Actors_C", moveInstruction.Name);
        Assert.Single(moveInstruction.Arguments);
        Assert.Equal("move_pattern", moveInstruction.Arguments[0].Name);
        Assert.Equal(17, moveInstruction.Arguments[0].RawValue);
    }

    [Fact]
    public async Task Reader_ParsesOpcode3EAsIncreaseCappedEventCounter()
    {
        var romPath = TestRomLocator.FindWorkspaceRom();
        var rom = await RomFile.LoadAsync(romPath);
        var profile = MedabotsRomTextProfiles.Detect(rom);

        Assert.NotNull(profile);

        var reader = new EventScriptReader();
        var script = reader.ReadById(rom, profile!, 40);
        var instruction = script.Instructions.Single(eventInstruction => eventInstruction.Offset == 0x785578);

        Assert.Equal(0x3E, instruction.Opcode);
        Assert.Equal("Increase_Capped_Event_Counter", instruction.Name);
        Assert.Collection(
            instruction.Arguments,
            argument => Assert.Equal("counter_slot", argument.Name),
            argument => Assert.Equal("amount", argument.Name));
        Assert.Equal(0, instruction.Arguments[0].RawValue);
        Assert.Equal(1, instruction.Arguments[1].RawValue);
    }

    [Fact]
    public async Task Reader_ParsesOpcode3FAsDecreaseCappedEventCounter()
    {
        var romPath = TestRomLocator.FindWorkspaceRom();
        var rom = await RomFile.LoadAsync(romPath);
        var profile = MedabotsRomTextProfiles.Detect(rom);

        Assert.NotNull(profile);

        var reader = new EventScriptReader();
        var script = reader.ReadById(rom, profile!, 114);
        var instruction = script.Instructions.Single(eventInstruction => eventInstruction.Offset == 0x78231F);

        Assert.Equal(0x3F, instruction.Opcode);
        Assert.Equal("Decrease_Capped_Event_Counter", instruction.Name);
        Assert.Collection(
            instruction.Arguments,
            argument => Assert.Equal("counter_slot", argument.Name),
            argument => Assert.Equal("amount", argument.Name));
        Assert.Equal(0, instruction.Arguments[0].RawValue);
        Assert.Equal(1, instruction.Arguments[1].RawValue);
    }

    [Fact]
    public async Task Reader_ParsesOpcode85AsBeginEventObjectFacingCycleBlock()
    {
        var romPath = TestRomLocator.FindWorkspaceRom();
        var rom = await RomFile.LoadAsync(romPath);
        var profile = MedabotsRomTextProfiles.Detect(rom);

        Assert.NotNull(profile);

        var reader = new EventScriptReader();
        var script = reader.ReadById(rom, profile!, 302);
        var instruction = script.Instructions.Single(eventInstruction => eventInstruction.Offset == 0x785983);

        Assert.Equal(0x85, instruction.Opcode);
        Assert.Equal("Begin_Event_Object_Facing_Cycle_Block", instruction.Name);
        Assert.Empty(instruction.Arguments);
    }

    [Fact]
    public async Task Reader_ParsesOpcode86AsQueuedEventObjectFacingCycleRecord()
    {
        var romPath = TestRomLocator.FindWorkspaceRom();
        var rom = await RomFile.LoadAsync(romPath);
        var profile = MedabotsRomTextProfiles.Detect(rom);

        Assert.NotNull(profile);

        var reader = new EventScriptReader();
        var script = reader.ReadById(rom, profile!, 302);
        var instruction = script.Instructions.Single(eventInstruction => eventInstruction.Offset == 0x785984);

        Assert.Equal(0x86, instruction.Opcode);
        Assert.Equal("Queue_Event_Object_Facing_Cycle", instruction.Name);
        Assert.Collection(
            instruction.Arguments,
            argument => Assert.Equal("cycle_type", argument.Name),
            argument => Assert.Equal("packed_object_id_high", argument.Name),
            argument => Assert.Equal("packed_object_id_low", argument.Name),
            argument => Assert.Equal("arg4", argument.Name),
            argument => Assert.Equal("arg5", argument.Name),
            argument => Assert.Equal("arg6", argument.Name),
            argument => Assert.Equal("arg7", argument.Name),
            argument => Assert.Equal("arg8", argument.Name));
        Assert.Equal(1, instruction.Arguments[0].RawValue);
        Assert.Equal(1, instruction.Arguments[1].RawValue);
        Assert.Equal(46, instruction.Arguments[2].RawValue);
    }

    [Fact]
    public async Task Reader_ParsesOpcode87AsExecuteEventObjectFacingCycleBlock()
    {
        var romPath = TestRomLocator.FindWorkspaceRom();
        var rom = await RomFile.LoadAsync(romPath);
        var profile = MedabotsRomTextProfiles.Detect(rom);

        Assert.NotNull(profile);

        var reader = new EventScriptReader();
        var script = reader.ReadById(rom, profile!, 302);
        var instruction = script.Instructions.Single(eventInstruction => eventInstruction.Offset == 0x78598F);

        Assert.Equal(0x87, instruction.Opcode);
        Assert.Equal("Execute_Event_Object_Facing_Cycle_Block", instruction.Name);
        Assert.Empty(instruction.Arguments);
    }

    [Fact]
    public async Task Reader_ParsesEvent2021WithoutDesyncAfterOpcode66()
    {
        var romPath = TestRomLocator.FindWorkspaceRom();
        var rom = await RomFile.LoadAsync(romPath);
        var profile = MedabotsRomTextProfiles.Detect(rom);

        Assert.NotNull(profile);

        var reader = new EventScriptReader();
        var script = reader.ReadById(rom, profile!, 2021);

        var transition = script.Instructions.Single(eventInstruction => eventInstruction.Offset == 0x77D123);
        var sound = script.Instructions.Single(eventInstruction => eventInstruction.Offset == 0x77D126);
        var shake = script.Instructions.Single(eventInstruction => eventInstruction.Offset == 0x77D128);
        var firstMessage = script.Instructions.Single(eventInstruction => eventInstruction.Offset == 0x77D12B);
        var battle = script.Instructions.Single(eventInstruction => eventInstruction.Offset == 0x77D12F);

        Assert.Equal("Animate_Tracked_Object_Transition", transition.Name);
        Assert.Collection(
            transition.Arguments,
            argument => Assert.Equal("tracked_object_index", argument.Name),
            argument => Assert.Equal("transition_mode", argument.Name));
        Assert.Equal(0, transition.Arguments[0].RawValue);
        Assert.Equal(1, transition.Arguments[1].RawValue);

        Assert.Equal("Play_Sound", sound.Name);
        Assert.Equal(23, sound.Arguments[0].RawValue);

        Assert.Equal("Shake_Camera", shake.Name);
        Assert.Equal(1, shake.Arguments[0].RawValue);
        Assert.Equal(10, shake.Arguments[1].RawValue);

        Assert.Equal("Show_Message_A", firstMessage.Name);
        Assert.Equal(13, firstMessage.Arguments[0].RawValue);
        Assert.Equal(182, firstMessage.Arguments[1].RawValue);

        Assert.Equal("Start_Battle", battle.Name);
        Assert.Equal(184, battle.Arguments[0].RawValue);

        Assert.DoesNotContain(script.Instructions, eventInstruction => eventInstruction.Name.StartsWith("INVALID_", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Reader_FormatsUnusedMovePlayerStepsAsUnused()
    {
        var romPath = TestRomLocator.FindWorkspaceRom();
        var rom = await RomFile.LoadAsync(romPath);
        var profile = MedabotsRomTextProfiles.Detect(rom);

        Assert.NotNull(profile);

        var reader = new EventScriptReader();
        var script = reader.ReadById(rom, profile!, 2021);
        var movePlayer = script.Instructions.Single(eventInstruction => eventInstruction.Offset == 0x77D114);

        Assert.Equal("Move_Player", movePlayer.Name);
        Assert.Equal(49, movePlayer.Arguments[0].RawValue);
        Assert.Equal("unused", movePlayer.Arguments[1].DisplayValue);
        Assert.Equal("unused", movePlayer.Arguments[2].DisplayValue);
        Assert.Equal("unused", movePlayer.Arguments[3].DisplayValue);
    }
}
