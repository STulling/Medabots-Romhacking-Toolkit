using Medabots.Rom;
using Medabots.Rom.Metadata;
using Medabots.Rom.Projects;
using Xunit;

namespace Medabots.Rom.Tests;

public sealed class RomHackProjectTests
{
    [Fact]
    public async Task Serializer_RoundTripsMessagePatchProjects()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.medahack.json");

        try
        {
            var project = new RomHackProject
            {
                Name = "Salty Quest Text",
                SourceRomPath = @"C:\roms\medabots.gba",
                TextProfileId = "MEDABOTSRKSVA9BPE9"
            };

            project.PendingActions.Add(new RomPatchAction(0x1234, [0xAA, 0xBB, 0xCC], "Patch sprite pointer"));
            project.MessagePatches.Add(new Text.MessagePatch(new Text.MessageId(0, 2), "<PORTRAIT:0, 27, 0>Hello<END:0>"));
            project.EventLabels.Add(new EventLabelPatch(361, 0x25, "EquipBattleRifle"));
            project.EventLabels.Add(new EventLabelPatch(361, 0x47, "MissingBattleRifle"));
            project.EventScriptPatches.Add(new EventScriptPatch(40, [0x33, 0x12, 0x01, 0x00, 0x06]));

            await RomHackProjectSerializer.SaveAsync(project, tempFile);
            var loaded = await RomHackProjectSerializer.LoadAsync(tempFile);

            Assert.Equal(project.Name, loaded.Name);
            Assert.Equal(project.SourceRomPath, loaded.SourceRomPath);
            Assert.Equal(project.TextProfileId, loaded.TextProfileId);
            Assert.Single(loaded.PendingActions);
            Assert.Equal(0x1234, loaded.PendingActions[0].Offset);
            Assert.Equal([0xAA, 0xBB, 0xCC], loaded.PendingActions[0].Data);
            Assert.Equal("Patch sprite pointer", loaded.PendingActions[0].Description);
            Assert.Single(loaded.MessagePatches);
            Assert.Equal(0, loaded.MessagePatches[0].Id.Bank);
            Assert.Equal(2, loaded.MessagePatches[0].Id.Index);
            Assert.Equal("<PORTRAIT:0, 27, 0>Hello<END:0>", loaded.MessagePatches[0].Text);
            Assert.Equal(2, loaded.EventLabels.Count);
            Assert.Equal((short)361, loaded.EventLabels[0].EventId);
            Assert.Equal(0x25, loaded.EventLabels[0].Offset);
            Assert.Equal("EquipBattleRifle", loaded.EventLabels[0].Label);
            Assert.Equal("MissingBattleRifle", loaded.EventLabels[1].Label);
            Assert.Single(loaded.EventScriptPatches);
            Assert.Equal((short)40, loaded.EventScriptPatches[0].EventId);
            Assert.Equal([0x33, 0x12, 0x01, 0x00, 0x06], loaded.EventScriptPatches[0].ScriptBytes);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void Profiles_DetectKnownRomHeaderSignature()
    {
        var rom = new byte[512];
        var signatureBytes = System.Text.Encoding.ASCII.GetBytes("MEDABOTSRKSVA9BPE9");
        Array.Copy(signatureBytes, 0, rom, 0xA0, signatureBytes.Length);

        var profile = MedabotsRomTextProfiles.Detect(rom);

        Assert.NotNull(profile);
        Assert.Equal("MEDABOTSRKSVA9BPE9", profile!.Id);
        Assert.Equal(0x47DF44, profile.TextPointerTableOffset);
        Assert.Equal(0x7F5500, profile.TextDumpOffset);
    }
}
