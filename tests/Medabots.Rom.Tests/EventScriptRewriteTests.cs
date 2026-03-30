using Medabots.Rom.Events;
using Medabots.Rom.Metadata;
using Xunit;

namespace Medabots.Rom.Tests;

public sealed class EventScriptRewriteTests
{
    [Fact]
    public async Task Patcher_RelocatesEditedEventIntoExpansionSpace()
    {
        var romPath = TestRomLocator.FindWorkspaceRom();
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
            EventTestHelpers.AssertDefinition(EventOperationRegistry.LoadDefault(), 0x33),
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
    public async Task Patcher_ReplayedActions_PreserveEditedRealEventInFreshSession()
    {
        var romPath = TestRomLocator.FindWorkspaceRom();
        var sourceRom = await RomFile.LoadAsync(romPath);
        var workingSession = RomHackSession.FromRomFile(new RomFile("working-event.gba", sourceRom.Data.ToArray()));
        var exportedSession = RomHackSession.FromRomFile(new RomFile("exported-event.gba", sourceRom.Data.ToArray()));
        var profile = MedabotsRomTextProfiles.Detect(sourceRom);

        Assert.NotNull(profile);

        var reader = new EventScriptReader();
        var patcher = new EventInstructionPatcher();
        var originalScript = reader.ReadById(workingSession.RomFile, profile!, 40);
        var startBattle = Assert.IsType<StartBattleInstruction>(originalScript.Instructions.First(instruction => instruction is StartBattleInstruction));

        patcher.Apply(
            workingSession,
            profile,
            originalScript,
            startBattle,
            EventTestHelpers.AssertDefinition(EventOperationRegistry.LoadDefault(), 0x33),
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["battle"] = 0x23,
                ["battle_mode_flags"] = startBattle.BattleModeFlags.Value,
                ["post_battle_mode_flags"] = startBattle.PostBattleModeFlags.Value
            });

        exportedSession.ApplyPatches(workingSession.AppliedActions);

        var reread = reader.ReadById(exportedSession.RomFile, profile, 40);
        var updatedStartBattle = Assert.IsType<StartBattleInstruction>(reread.Instructions.First(instruction => instruction is StartBattleInstruction));
        Assert.Equal(0x23, updatedStartBattle.Battle.Value);
        Assert.True(reread.StartOffset >= 0x800000);
    }

    [Fact]
    public void Rewriter_InsertNopBeforeJumpTarget_RecalculatesJumpOffsets()
    {
        var registry = EventOperationRegistry.LoadDefault();
        var rewriter = new EventScriptRewriter(registry);
        var rom = new RomFile("synthetic.gba", [0x30, 0x00, 0x02, 0x00, 0x06]);
        var jumpDefinition = EventTestHelpers.AssertDefinition(registry, 0x30);
        var nopDefinition = EventTestHelpers.AssertDefinition(registry, 0x00);
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
        var jumpDefinition = EventTestHelpers.AssertDefinition(registry, 0x30);
        var nopDefinition = EventTestHelpers.AssertDefinition(registry, 0x00);
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
        var nopDefinition = EventTestHelpers.AssertDefinition(registry, 0x00);
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

        var rewrittenBytes = rewriter.InsertInstructionBefore(rom, script, labelMap, 0, EventTestHelpers.AssertDefinition(registry, 0x31));

        Assert.Equal([0x31, 0x01, 0x00, 0x06], rewrittenBytes);
    }

    [Fact]
    public void Rewriter_MoveInstructionDown_ReordersBodyWithoutLosingStart()
    {
        var registry = EventOperationRegistry.LoadDefault();
        var rewriter = new EventScriptRewriter(registry);
        var rom = new RomFile("synthetic.gba", [0x00, 0x04, 0x01, 0x04, 0x02, 0x06]);
        var nopDefinition = EventTestHelpers.AssertDefinition(registry, 0x00);
        var waitDefinition = EventTestHelpers.AssertDefinition(registry, 0x04);
        var start = new EventInstruction(0, 0x00, nopDefinition.Name, [], "Nop()", false) { Definition = nopDefinition };
        var waitOne = new EventInstruction(1, 0x04, waitDefinition.Name, [new EventArgumentValue("frames", EventArgumentType.Byte, 1, "1")], "Wait_X_Frames(1)", false) { Definition = waitDefinition };
        var waitTwo = new EventInstruction(3, 0x04, waitDefinition.Name, [new EventArgumentValue("frames", EventArgumentType.Byte, 2, "2")], "Wait_X_Frames(2)", false) { Definition = waitDefinition };
        var end = new EndInstruction(5, 0x06);
        var script = new EventScript(0, 0, [start, waitOne, waitTwo, end]);
        var labelMap = new Dictionary<int, string> { [0] = "Start" };

        var rewrittenBytes = rewriter.MoveInstructionDown(rom, script, labelMap, 1);

        Assert.Equal([0x00, 0x04, 0x02, 0x04, 0x01, 0x06], rewrittenBytes);
    }

    [Fact]
    public void Rewriter_MoveInstructionUp_FromSecondSlot_Throws()
    {
        var registry = EventOperationRegistry.LoadDefault();
        var rewriter = new EventScriptRewriter(registry);
        var rom = new RomFile("synthetic.gba", [0x00, 0x04, 0x01, 0x06]);
        var nopDefinition = EventTestHelpers.AssertDefinition(registry, 0x00);
        var waitDefinition = EventTestHelpers.AssertDefinition(registry, 0x04);
        var start = new EventInstruction(0, 0x00, nopDefinition.Name, [], "Nop()", false) { Definition = nopDefinition };
        var wait = new EventInstruction(1, 0x04, waitDefinition.Name, [new EventArgumentValue("frames", EventArgumentType.Byte, 1, "1")], "Wait_X_Frames(1)", false) { Definition = waitDefinition };
        var end = new EndInstruction(3, 0x06);
        var script = new EventScript(0, 0, [start, wait, end]);
        var labelMap = new Dictionary<int, string> { [0] = "Start" };

        var exception = Assert.Throws<InvalidOperationException>(() => rewriter.MoveInstructionUp(rom, script, labelMap, 1));

        Assert.Contains("event start", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
