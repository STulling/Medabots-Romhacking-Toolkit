using Medabots.Rom.Events;
using Medabots.Rom.Metadata;
using Xunit;

namespace Medabots.Rom.Tests;

public sealed class EventScriptReaderTests
{
    [Fact]
    public void Registry_LoadsKnownOperations()
    {
        var registry = EventOperationRegistry.LoadDefault();

        Assert.True(registry.TryGetDefinition(0x01, out var definition));
        Assert.Equal("Show_Message_A", definition.Name);
        Assert.Equal(2, definition.Arguments.Count);
        Assert.Equal(EventArgumentType.EventBank, definition.Arguments[0].Type);
        Assert.Equal(EventArgumentType.Short, definition.Arguments[1].Type);
    }

    [Fact]
    public async Task Reader_CanParseRealEventFromLocalRokushoRom()
    {
        var romPath = FindWorkspaceRom();
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
        var romPath = FindWorkspaceRom();
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
        var romPath = FindWorkspaceRom();
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
        var romPath = FindWorkspaceRom();
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
        var romPath = FindWorkspaceRom();
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
        var romPath = FindWorkspaceRom();
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

    [Fact]
    public async Task Patcher_RelocatesEditedEventIntoExpansionSpace()
    {
        var romPath = FindWorkspaceRom();
        var session = await RomHackSession.OpenAsync(romPath);
        var profile = MedabotsRomTextProfiles.Detect(session.RomFile);

        Assert.NotNull(profile);

        var reader = new EventScriptReader();
        var patcher = new EventInstructionPatcher();
        var originalScript = reader.ReadById(session.RomFile, profile!, 40);
        var startBattle = Assert.IsType<StartBattleInstruction>(originalScript.Instructions.First(instruction => instruction is StartBattleInstruction));

        patcher.Apply(
            session,
            profile,
            originalScript,
            startBattle,
            AssertDefinition(EventOperationRegistry.LoadDefault(), 0x33),
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["battle"] = 0x1A,
                ["battle_mode_flags"] = startBattle.BattleModeFlags.Value,
                ["post_battle_mode_flags"] = startBattle.PostBattleModeFlags.Value
            });

        var relocatedScript = reader.ReadById(session.RomFile, profile, 40);
        var relocatedStartBattle = Assert.IsType<StartBattleInstruction>(relocatedScript.Instructions.First(instruction => instruction is StartBattleInstruction));

        Assert.True(relocatedScript.StartOffset >= 0x800000);
        Assert.Equal(0x1A, relocatedStartBattle.Battle.Value);
    }

    [Fact]
    public void Rewriter_InsertNopBeforeJumpTarget_RecalculatesJumpOffsets()
    {
        var registry = EventOperationRegistry.LoadDefault();
        var rewriter = new EventScriptRewriter(registry);
        var rom = new RomFile("synthetic.gba", [0x30, 0x00, 0x02, 0x00, 0x06]);
        var jumpDefinition = AssertDefinition(registry, 0x30);
        var nopDefinition = AssertDefinition(registry, 0x00);
        var jumpInstruction = new EventInstruction(0, 0x30, jumpDefinition.Name, [new EventArgumentValue("jump", EventArgumentType.Short, 2, "2")], "Relative_Long_Jump(jump: 2)", false)
        {
            Definition = jumpDefinition
        };
        var nopInstruction = new EventInstruction(3, 0x00, nopDefinition.Name, [], "Nop()", false)
        {
            Definition = nopDefinition
        };
        var endInstruction = new EndInstruction(4, 0x06);
        var script = new EventScript(0, 0, [jumpInstruction, nopInstruction, endInstruction]);
        var labelMap = new Dictionary<int, string>
        {
            [0] = "Start",
            [3] = "Target"
        };

        var rewrittenBytes = rewriter.InsertNopBefore(rom, script, labelMap, 3);

        Assert.Equal([0x30, 0x00, 0x03, 0x00, 0x00, 0x06], rewrittenBytes);
    }

    [Fact]
    public void Rewriter_DeleteJumpTarget_Throws()
    {
        var registry = EventOperationRegistry.LoadDefault();
        var rewriter = new EventScriptRewriter(registry);
        var rom = new RomFile("synthetic.gba", [0x30, 0x00, 0x02, 0x00, 0x06]);
        var jumpDefinition = AssertDefinition(registry, 0x30);
        var nopDefinition = AssertDefinition(registry, 0x00);
        var jumpInstruction = new EventInstruction(0, 0x30, jumpDefinition.Name, [new EventArgumentValue("jump", EventArgumentType.Short, 2, "2")], "Relative_Long_Jump(jump: 2)", false)
        {
            Definition = jumpDefinition
        };
        var nopInstruction = new EventInstruction(3, 0x00, nopDefinition.Name, [], "Nop()", false)
        {
            Definition = nopDefinition
        };
        var endInstruction = new EndInstruction(4, 0x06);
        var script = new EventScript(0, 0, [jumpInstruction, nopInstruction, endInstruction]);
        var labelMap = new Dictionary<int, string>
        {
            [0] = "Start",
            [3] = "Target"
        };

        var exception = Assert.Throws<InvalidOperationException>(() => rewriter.DeleteInstruction(rom, script, labelMap, 3));
        Assert.Contains("target of a jump", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rewriter_InsertSelectedOperation_UsesValidDefaultJump()
    {
        var registry = EventOperationRegistry.LoadDefault();
        var rewriter = new EventScriptRewriter(registry);
        var rom = new RomFile("synthetic.gba", [0x00, 0x06]);
        var nopDefinition = AssertDefinition(registry, 0x00);
        var baseInstruction = new EventInstruction(0, 0x00, nopDefinition.Name, [], "Nop()", false)
        {
            Definition = nopDefinition
        };
        var endInstruction = new EndInstruction(1, 0x06);
        var script = new EventScript(0, 0, [baseInstruction, endInstruction]);
        var labelMap = new Dictionary<int, string>
        {
            [0] = "Start"
        };

        var rewrittenBytes = rewriter.InsertInstructionBefore(rom, script, labelMap, 0, AssertDefinition(registry, 0x31));

        Assert.Equal([0x31, 0x01, 0x00, 0x06], rewrittenBytes);
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

    private static EventOperationDefinition AssertDefinition(EventOperationRegistry registry, byte opcode)
    {
        Assert.True(registry.TryGetDefinition(opcode, out var definition));
        return definition;
    }
}
