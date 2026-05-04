using System.Collections.Generic;
using Medabots.Rom.Battles;
using Medabots.Rom;
using Medabots.Rom.Images;
using Medabots.Rom.Maps;
using Medabots.Rom.Metadata;
using Medabots.Rom.Parts;
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
            project.DeletedEventScriptIds.Add(91);
            project.MapEntitySpawnPatches.Add(new MapEntitySpawnPatch(
                16,
                [
                    new MapEntitySpawnRecord(4, 5, 0x4123, 0x21, 3, 0x00FF),
                    new MapEntitySpawnRecord(7, 8, 0x8567, 0xFF, 1, 0x0F0F)
                ],
                [0]));
            project.MapEncounterPatches.Add(new MapEncounterPatch(16, 10, 11, 12, 13));
            project.MapEncounterStatePatches.Add(new MapEncounterStatePatch(16, 1));
            project.MapMusicPatches.Add(new MapMusicPatch(16, 29));
            project.MapEventObjectResourcePatches.Add(new MapEventObjectResourcePatch(16, [0x00, 0x02, 0x03, 0xFF]));
            project.MapDimensionPatches.Add(new MapDimensionPatch(16, 40, 32));
            project.OverworldSpriteEdits.Add(new SpriteAsset(7, 0x10, 0x20, 0x30, 0x40, new IndexedImage(1, 1, [1, 2, 3, 4, 5, 6, 7, 8], new byte[32])));
            project.PortraitEdits.Add(new PortraitAsset(3, 1, 0x11, 0x21, 0x31, 0x41, new IndexedImage(1, 1, [8, 7, 6, 5, 4, 3, 2, 1], new byte[32])));
            project.BattleCompositeSpriteEdits.Add(new BattleCompositeSpriteComponentAsset(12, 2, 0x12, 0x32, 0x22, 0x42, 5, 7, 9, new IndexedImage(1, 1, [0, 1, 2, 3, 4, 5, 6, 7], new byte[32])));
            project.LargePartDisplayEdits.Add(new LargePartDisplayAsset(
                449,
                112,
                PartKind.RightArm,
                1,
                4,
                0x123456,
                new Dictionary<int, byte[]> { [8] = [0x00, 0x01, 0x02, 0x03] },
                new[]
                {
                    new LargePartDisplayPieceAsset(
                        4,
                        0x600000,
                        0x654321,
                        new byte[0x18],
                        0x15,
                        0x25,
                        0x35,
                        0x45,
                        [0x00, 0x01, 0x02, 0x03],
                        8,
                        1,
                        2,
                        3,
                        4,
                        32,
                        32,
                        0x11,
                        1,
                        false,
                        false,
                        new IndexedImage(1, 1, [1, 1, 1, 1, 1, 1, 1, 1], new byte[32]))
                }));
            project.BattleEdits.Add(new BattleDefinition(5, 0x1000, 0x2000, 17, 1, 3, 1, [new BattleBot(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 0), new BattleBot(12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 0), new BattleBot(23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 0)]));
            project.PartEdits.Add(new PartDefinition(9, 2, PartKind.RightArm, 0x3000, 1, 2, 3, 0, 4, 5, 6, 1, 7, 8, 9, 10, 11, 12, 13, 14));
            project.MapLayerPatches.Add(new MapLayerPatch(16, 1, 30, 20, 0, 0, 0, 0, [0x0001, 0x1002, 0x2003, 0x3004]));
            project.SplitLargeDisplayPartIds.Add(449);
            project.SplitLargeDisplayPartIds.Add(450);

            await RomHackProjectSerializer.SaveAsync(project, tempFile);
            var loaded = await RomHackProjectSerializer.LoadAsync(tempFile);

            Assert.Equal(project.Name, loaded.Name);
            Assert.Null(loaded.SourceRomPath);
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
            Assert.Equal([(short)91], loaded.DeletedEventScriptIds);
            Assert.Single(loaded.MapEntitySpawnPatches);
            Assert.Equal(16, loaded.MapEntitySpawnPatches[0].MapId);
            Assert.Equal([0], loaded.MapEntitySpawnPatches[0].DeletedOriginalIndices);
            Assert.Equal(2, loaded.MapEntitySpawnPatches[0].Records.Count);
            Assert.Equal((byte)4, loaded.MapEntitySpawnPatches[0].Records[0].TileX);
            Assert.Equal((ushort)0x4123, loaded.MapEntitySpawnPatches[0].Records[0].RecordKindAndEventId);
            Assert.Equal((byte)0xFF, loaded.MapEntitySpawnPatches[0].Records[1].SpriteAndFacingPacked);
            Assert.Single(loaded.MapEncounterPatches);
            Assert.Equal((byte)10, loaded.MapEncounterPatches[0].Battle1);
            Assert.Single(loaded.MapEncounterStatePatches);
            Assert.Equal((byte)1, loaded.MapEncounterStatePatches[0].EncounterEnabledByte);
            Assert.Single(loaded.MapMusicPatches);
            Assert.Equal((byte)29, loaded.MapMusicPatches[0].MusicId);
            Assert.Single(loaded.MapEventObjectResourcePatches);
            Assert.Equal([0x00, 0x02, 0x03, 0xFF], loaded.MapEventObjectResourcePatches[0].ResourceIds);
            Assert.Single(loaded.MapDimensionPatches);
            Assert.Equal((byte)40, loaded.MapDimensionPatches[0].WidthInTiles);
            Assert.Equal((byte)32, loaded.MapDimensionPatches[0].HeightInTiles);
            Assert.Single(loaded.OverworldSpriteEdits);
            Assert.Equal(7, loaded.OverworldSpriteEdits[0].SpriteId);
            Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8], loaded.OverworldSpriteEdits[0].Image.PixelIndices);
            Assert.Single(loaded.PortraitEdits);
            Assert.Equal(3, loaded.PortraitEdits[0].CharacterId);
            Assert.Equal(1, loaded.PortraitEdits[0].PortraitIndex);
            Assert.Single(loaded.BattleCompositeSpriteEdits);
            Assert.Equal(12, loaded.BattleCompositeSpriteEdits[0].MedabotId);
            Assert.Equal((byte)5, loaded.BattleCompositeSpriteEdits[0].PaletteFamily);
            Assert.Single(loaded.LargePartDisplayEdits);
            Assert.Equal(449, loaded.LargePartDisplayEdits[0].PartId);
            Assert.Single(loaded.LargePartDisplayEdits[0].Pieces);
            Assert.Single(loaded.BattleEdits);
            Assert.Equal(5, loaded.BattleEdits[0].Id);
            Assert.Equal((byte)17, loaded.BattleEdits[0].CharacterId);
            Assert.Equal(3, loaded.BattleEdits[0].Bots.Count);
            Assert.Single(loaded.PartEdits);
            Assert.Equal(9, loaded.PartEdits[0].Id);
            Assert.Equal(PartKind.RightArm, loaded.PartEdits[0].Kind);
            Assert.Single(loaded.MapLayerPatches);
            Assert.Equal(16, loaded.MapLayerPatches[0].MapId);
            Assert.Equal([0x0001, 0x1002, 0x2003, 0x3004], loaded.MapLayerPatches[0].TileEntries);
            Assert.Equal([449, 450], loaded.SplitLargeDisplayPartIds.OrderBy(id => id).ToArray());
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
    public async Task Serializer_DoesNotPersistSourceRomPath()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.medahack.json");

        try
        {
            var project = new RomHackProject
            {
                Name = "Portable Project",
                SourceRomPath = @"C:\roms\private-copy.gba",
                TextProfileId = "MEDABOTSRKSVA9BPE9"
            };

            await RomHackProjectSerializer.SaveAsync(project, tempFile);
            var json = await File.ReadAllTextAsync(tempFile);
            var loaded = await RomHackProjectSerializer.LoadAsync(tempFile);

            Assert.DoesNotContain("private-copy.gba", json);
            Assert.Null(loaded.SourceRomPath);
            Assert.Equal("MEDABOTSRKSVA9BPE9", loaded.TextProfileId);
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

    [Fact]
    public void MapMetadataProjectEditor_StagesAndUpdatesMetadataPatches()
    {
        var project = new RomHackProject();
        var editor = new MapMetadataProjectEditor();
        var sourceAsset = CreateTestMapTilesetAsset(
            mapId: 16,
            encounterEnabledByte: 0,
            musicId: 7,
            resourceIds: [0x00, 0x01, 0x02, 0x03]);

        editor.StageMetadata(
            project,
            sourceAsset,
            1,
            29,
            [0x00, 0x02, 0x03, 0xFF],
            [10, 11, 12, 13]);

        Assert.Single(project.MapEncounterStatePatches);
        Assert.Equal(16, project.MapEncounterStatePatches[0].MapId);
        Assert.Equal((byte)1, project.MapEncounterStatePatches[0].EncounterEnabledByte);

        Assert.Single(project.MapMusicPatches);
        Assert.Equal((byte)29, project.MapMusicPatches[0].MusicId);

        Assert.Single(project.MapEventObjectResourcePatches);
        Assert.Equal(
            [0x00, 0x02, 0x03, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF],
            project.MapEventObjectResourcePatches[0].ResourceIds);

        Assert.Single(project.MapEncounterPatches);
        Assert.Equal((byte)10, project.MapEncounterPatches[0].Battle1);
        Assert.Equal((byte)13, project.MapEncounterPatches[0].Battle4);

        editor.StageMetadata(
            project,
            sourceAsset,
            0,
            7,
            [0x00, 0x01, 0x02, 0x03],
            null);

        Assert.Empty(project.MapEncounterStatePatches);
        Assert.Empty(project.MapMusicPatches);
        Assert.Empty(project.MapEventObjectResourcePatches);
        Assert.Empty(project.MapEncounterPatches);
    }

    [Fact]
    public void MapMetadataProjectEditor_CreatesSpriteSlotPatch_WhenOnlySpriteSlotsChange()
    {
        var project = new RomHackProject();
        var editor = new MapMetadataProjectEditor();
        var sourceAsset = CreateTestMapTilesetAsset(
            mapId: 33,
            encounterEnabledByte: 0,
            musicId: 11,
            resourceIds: [0x00, 0x01, 0x02, 0x03]);

        editor.StageMetadata(
            project,
            sourceAsset,
            0,
            11,
            [0x05, 0x01, 0x02, 0x03],
            null);

        Assert.Empty(project.MapEncounterStatePatches);
        Assert.Empty(project.MapMusicPatches);
        Assert.Single(project.MapEventObjectResourcePatches);
        Assert.Equal(33, project.MapEventObjectResourcePatches[0].MapId);
        Assert.Equal((byte)0x05, project.MapEventObjectResourcePatches[0].ResourceIds[0]);
    }

    private static MapTilesetAsset CreateTestMapTilesetAsset(int mapId, byte encounterEnabledByte, byte musicId, IReadOnlyList<byte> resourceIds)
    {
        var palette = new byte[32];
        var pixels = new byte[64];
        var image = new IndexedImage(1, 1, pixels, palette);
        var layer = new MapLayerAsset(0, 0, 0, 1, 1, 0, 0, 0, 0, [0], image);
        return new MapTilesetAsset(
            mapId,
            $"Map {mapId:D3}",
            2,
            2,
            0,
            encounterEnabledByte,
            0,
            musicId,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            palette,
            [],
            null,
            resourceIds,
            pixels,
            image,
            [0],
            [layer]);
    }
}
