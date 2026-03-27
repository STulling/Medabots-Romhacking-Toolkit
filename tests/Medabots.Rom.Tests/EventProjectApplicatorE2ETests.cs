using Medabots.Rom.Events;
using Medabots.Rom.Metadata;
using Medabots.Rom.Projects;
using Medabots.Rom.Text;
using Xunit;

namespace Medabots.Rom.Tests;

public sealed class EventProjectApplicatorE2ETests
{
    [Fact]
    public async Task ProjectApplicator_AppliesRealEventScriptPatchToRomSession()
    {
        var rom = await RomFile.LoadAsync(TestRomLocator.FindWorkspaceRom());
        var session = RomHackSession.FromRomFile(new RomFile("project-event-apply.gba", rom.Data.ToArray()));
        var profile = MedabotsRomTextProfiles.Detect(rom);

        Assert.NotNull(profile);

        var reader = new EventScriptReader();
        var serializer = new EventScriptSerializer();
        var applicator = new RomHackProjectApplicator();
        var registry = EventOperationRegistry.LoadDefault();
        var messages = new MedabotsMessageTableReader().ReadAll(session.RomFile, profile.TextPointerTableOffset);
        var script = reader.ReadById(session.RomFile, profile!, 40);
        var startBattle = Assert.IsType<StartBattleInstruction>(script.Instructions.First(instruction => instruction is StartBattleInstruction));
        var definition = EventTestHelpers.AssertDefinition(registry, 0x33);

        var replacement = new EventInstruction(
            startBattle.Offset,
            0x33,
            definition.Name,
            [
                new EventArgumentValue("battle", EventArgumentType.Byte, 0x2C, "44"),
                new EventArgumentValue("battle_mode_flags", EventArgumentType.Byte, startBattle.BattleModeFlags.Value, startBattle.BattleModeFlags.Value.ToString()),
                new EventArgumentValue("post_battle_mode_flags", EventArgumentType.Byte, startBattle.PostBattleModeFlags.Value, startBattle.PostBattleModeFlags.Value.ToString())
            ],
            "Start_Battle(battle: 44, ...)",
            false)
        {
            Definition = definition
        };

        var project = new RomHackProject
        {
            Name = "Event E2E",
            TextProfileId = profile.Id
        };
        project.MessagePatches.Add(new MessagePatch(new MessageId(0, 0), messages[new MessageId(0, 0)]));
        project.EventScriptPatches.Add(new EventScriptPatch(40, serializer.Serialize(session.RomFile, script, replacement)));

        applicator.Apply(project, session);

        var reread = reader.ReadById(session.RomFile, profile, 40);
        var updatedStartBattle = Assert.IsType<StartBattleInstruction>(reread.Instructions.First(instruction => instruction is StartBattleInstruction));

        Assert.Equal(0x2C, updatedStartBattle.Battle.Value);
        Assert.True(reread.StartOffset >= 0x800000);
    }

    [Fact]
    public async Task ProjectApplicator_AppliesEventScriptPatchWithoutMessagePatches()
    {
        var rom = await RomFile.LoadAsync(TestRomLocator.FindWorkspaceRom());
        var session = RomHackSession.FromRomFile(new RomFile("project-event-apply-only.gba", rom.Data.ToArray()));
        var profile = MedabotsRomTextProfiles.Detect(rom);

        Assert.NotNull(profile);

        var reader = new EventScriptReader();
        var serializer = new EventScriptSerializer();
        var applicator = new RomHackProjectApplicator();
        var registry = EventOperationRegistry.LoadDefault();
        var script = reader.ReadById(session.RomFile, profile!, 40);
        var startBattle = Assert.IsType<StartBattleInstruction>(script.Instructions.First(instruction => instruction is StartBattleInstruction));
        var definition = EventTestHelpers.AssertDefinition(registry, 0x33);

        var replacement = new EventInstruction(
            startBattle.Offset,
            0x33,
            definition.Name,
            [
                new EventArgumentValue("battle", EventArgumentType.Byte, 0x19, "25"),
                new EventArgumentValue("battle_mode_flags", EventArgumentType.Byte, startBattle.BattleModeFlags.Value, startBattle.BattleModeFlags.Value.ToString()),
                new EventArgumentValue("post_battle_mode_flags", EventArgumentType.Byte, startBattle.PostBattleModeFlags.Value, startBattle.PostBattleModeFlags.Value.ToString())
            ],
            "Start_Battle(battle: 25, ...)",
            false)
        {
            Definition = definition
        };

        var project = new RomHackProject
        {
            Name = "Event Only E2E",
            TextProfileId = profile.Id
        };
        project.EventScriptPatches.Add(new EventScriptPatch(40, serializer.Serialize(session.RomFile, script, replacement)));

        applicator.Apply(project, session);

        var reread = reader.ReadById(session.RomFile, profile, 40);
        var updatedStartBattle = Assert.IsType<StartBattleInstruction>(reread.Instructions.First(instruction => instruction is StartBattleInstruction));

        Assert.Equal(0x19, updatedStartBattle.Battle.Value);
        Assert.True(reread.StartOffset >= 0x800000);
    }
}
