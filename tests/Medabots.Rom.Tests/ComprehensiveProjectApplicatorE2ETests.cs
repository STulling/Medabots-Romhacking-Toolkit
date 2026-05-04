using Medabots.Rom.Battles;
using Medabots.Rom.Encounters;
using Medabots.Rom.Events;
using Medabots.Rom.Images;
using Medabots.Rom.Maps;
using Medabots.Rom.Metadata;
using Medabots.Rom.Parts;
using Medabots.Rom.Projects;
using Medabots.Rom.Text;
using Xunit;

namespace Medabots.Rom.Tests;

public sealed class ComprehensiveProjectApplicatorE2ETests
{
    [Fact]
    public async Task ProjectApplicator_RoundTripsEditsAcrossMessagesEventsBattlesPartsSpritesAndMaps()
    {
        var rom = await RomFile.LoadAsync(TestRomLocator.FindWorkspaceRom());
        var profile = Assert.IsType<MedabotsRomTextProfile>(MedabotsRomTextProfiles.Detect(rom));

        var messageReader = new MedabotsMessageTableReader();
        var eventReader = new EventScriptReader();
        var eventSerializer = new EventScriptSerializer();
        var eventRegistry = EventOperationRegistry.LoadDefault();
        var battleReader = new BattleTableReader();
        var partReader = new PartTableReader();
        var imageRepository = new ImageAssetRepository();
        var mapRepository = new MapTilesetRepository();
        var overlayRepository = new MapOverlayRepository();
        var encounterReader = new EncounterTableReader();

        var messageId = new MessageId(0, 0);
        var replacementMessage = "A<END:0>";

        var sourceScript = eventReader.ReadById(rom, profile, 40);
        var sourceStartBattle = Assert.IsType<StartBattleInstruction>(sourceScript.Instructions.First(instruction => instruction is StartBattleInstruction));
        var startBattleDefinition = EventTestHelpers.AssertDefinition(eventRegistry, 0x33);
        var replacementInstruction = new EventInstruction(
            sourceStartBattle.Offset,
            0x33,
            startBattleDefinition.Name,
            [
                new EventArgumentValue("battle", EventArgumentType.Byte, 0x2C, "44"),
                new EventArgumentValue("battle_mode_flags", EventArgumentType.Byte, sourceStartBattle.BattleModeFlags.Value, sourceStartBattle.BattleModeFlags.Value.ToString()),
                new EventArgumentValue("post_battle_mode_flags", EventArgumentType.Byte, sourceStartBattle.PostBattleModeFlags.Value, sourceStartBattle.PostBattleModeFlags.Value.ToString())
            ],
            "Start_Battle(battle: 44, ...)",
            false)
        {
            Definition = startBattleDefinition
        };

        var sourceBattle = battleReader.ReadAll(rom, profile)[0];
        var editedBattle = sourceBattle with
        {
            CharacterId = (byte)(sourceBattle.CharacterId == byte.MaxValue ? 0 : sourceBattle.CharacterId + 1)
        };

        var sourcePart = partReader.ReadAll(rom)[0];
        var editedPart = sourcePart with
        {
            Armor = (byte)(sourcePart.Armor == byte.MaxValue ? 0 : sourcePart.Armor + 1)
        };

        var overworldSprite = imageRepository.ReadSprite(rom, 0);
        overworldSprite.Image.PixelIndices[0] = (byte)((overworldSprite.Image.PixelIndices[0] + 1) & 0x0F);
        overworldSprite.Image.PaletteBytes[2] ^= 0x1F;

        var portrait = imageRepository.ReadPortrait(rom, 0, 0);
        portrait.Image.PixelIndices[0] = (byte)((portrait.Image.PixelIndices[0] + 3) & 0x0F);
        portrait.Image.PaletteBytes[2] ^= 0x1F;

        var component = imageRepository.ReadBattleCompositeSpriteComponent(rom, 0, 0);
        component.Image.PixelIndices[0] = (byte)((component.Image.PixelIndices[0] + 5) & 0x0F);
        var nextFamily = (byte)((component.PaletteFamily + 1) % MedabotsRomSchema.CompositeBattleSpritePaletteCount);
        var nextPalette = imageRepository.ReadBattleCompositePaletteBytesForFamily(rom, nextFamily);
        component = component with
        {
            PaletteFamily = nextFamily,
            PaletteOffset = MedabotsRomSchema.PartSelectionComponentPaletteSetOffset + (nextFamily * ImageAssetRepository.PaletteSize),
            PaletteSelector = (byte)(nextFamily + 4),
            Image = component.Image with { PaletteBytes = nextPalette.ToArray() }
        };

        var largeDisplayPart = partReader.ReadAll(rom)[375];
        var largeDisplay = imageRepository.ReadLargePartDisplay(rom, largeDisplayPart);
        largeDisplay.Pieces[0].Image.PixelIndices[0] = (byte)((largeDisplay.Pieces[0].Image.PixelIndices[0] + 7) & 0x0F);
        if (largeDisplay.Pieces[0].PaletteBytes.Length >= 4)
        {
            largeDisplay.Pieces[0].PaletteBytes[2] ^= 0x1F;
            largeDisplay.Pieces[0].Image.PaletteBytes[2] ^= 0x1F;
        }

        const int mapId = 16;
        var sourceMap = mapRepository.ReadMap(rom, mapId);
        var sourceOverlay = overlayRepository.ReadMap(rom, mapId);
        var sourceEncounters = encounterReader.ReadAll(rom)[mapId];

        var editedLayerEntries = sourceMap.Layers[0].TileEntries.ToArray();
        editedLayerEntries[0] ^= 0x0001;
        editedLayerEntries[1] ^= 0x1000;

        var editedCollision = sourceMap.CollisionBytes.ToArray();
        editedCollision[0] = (byte)(editedCollision[0] == 0x7F ? 0x01 : editedCollision[0] + 1);

        var editedSpawn = sourceOverlay.EntitySpawns[0];
        editedSpawn = new MapEntitySpawnRecord(
            editedSpawn.TileX,
            editedSpawn.TileY,
            (ushort)((editedSpawn.RecordKind << 12) | ((editedSpawn.EventOrObjectId + 1) & 0x0FFF)),
            editedSpawn.SpriteAndFacingPacked,
            editedSpawn.SpawnGroupIndex,
            editedSpawn.ChapterVisibilityMask);

        var editedSpawns = sourceOverlay.EntitySpawns.ToArray();
        editedSpawns[0] = editedSpawn;

        var editedWarp = sourceOverlay.Warps[0];
        editedWarp = editedWarp with
        {
            DestinationMapId = (byte)(editedWarp.DestinationMapId == byte.MaxValue ? 0 : editedWarp.DestinationMapId + 1)
        };
        var editedWarps = sourceOverlay.Warps.ToArray();
        editedWarps[0] = editedWarp;

        var project = new RomHackProject
        {
            Name = "Comprehensive E2E",
            TextProfileId = profile.Id
        };
        project.MessagePatches.Add(new MessagePatch(messageId, replacementMessage));
        project.EventScriptPatches.Add(new EventScriptPatch(40, eventSerializer.Serialize(rom, sourceScript, replacementInstruction)));
        project.BattleEdits.Add(editedBattle);
        project.PartEdits.Add(editedPart);
        project.OverworldSpriteEdits.Add(overworldSprite);
        project.PortraitEdits.Add(portrait);
        project.BattleCompositeSpriteEdits.Add(component);
        project.LargePartDisplayEdits.Add(largeDisplay);
        project.MapLayerPatches.Add(new MapLayerPatch(
            sourceMap.MapId,
            sourceMap.Layers[0].LayerIndex,
            sourceMap.Layers[0].HeaderWidthInTiles,
            sourceMap.Layers[0].HeaderHeightInTiles,
            sourceMap.Layers[0].HeaderOriginX,
            sourceMap.Layers[0].HeaderOriginY,
            sourceMap.Layers[0].HeaderOriginX2,
            sourceMap.Layers[0].HeaderOriginY2,
            editedLayerEntries));
        project.MapCollisionPatches.Add(new MapCollisionPatch(mapId, editedCollision));
        project.MapEntitySpawnPatches.Add(new MapEntitySpawnPatch(mapId, editedSpawns, []));
        project.MapWarpPatches.Add(new MapWarpPatch(mapId, editedWarps));
        project.MapEncounterStatePatches.Add(new MapEncounterStatePatch(mapId, 0));
        project.MapEncounterPatches.Add(new MapEncounterPatch(mapId, 1, 2, 3, 4));
        project.MapMusicPatches.Add(new MapMusicPatch(mapId, 0x2A));
        project.MapEventObjectResourcePatches.Add(new MapEventObjectResourcePatch(mapId, [0x00, 0x02, 0x04, 0xFF]));

        var loadedProject = await RoundTripProjectAsync(project);
        var session = RomHackSession.FromRomFile(new RomFile("comprehensive-e2e.gba", rom.Data.ToArray()));
        var applicator = new RomHackProjectApplicator();
        applicator.Apply(loadedProject, session);

        var rereadMessages = messageReader.ReadAll(session.RomFile, profile.TextPointerTableOffset);
        var rereadScript = eventReader.ReadById(session.RomFile, profile, 40);
        var rereadBattle = battleReader.ReadAll(session.RomFile, profile)[0];
        var rereadPart = partReader.ReadSingle(session.RomFile, sourcePart.Id, sourcePart.DataOffset);
        var rereadSprite = imageRepository.ReadSprite(session.RomFile, 0);
        var rereadPortrait = imageRepository.ReadPortrait(session.RomFile, 0, 0);
        var rereadComponent = imageRepository.ReadBattleCompositeSpriteComponent(session.RomFile, 0, 0);
        var rereadLargeDisplay = imageRepository.ReadLargePartDisplay(session.RomFile, largeDisplayPart);
        var rereadMap = mapRepository.ReadMap(session.RomFile, mapId);
        var rereadOverlay = overlayRepository.ReadMap(session.RomFile, mapId);
        var rereadEncounter = encounterReader.ReadAll(session.RomFile)[mapId];

        Assert.Equal(replacementMessage, rereadMessages[messageId]);

        var rereadStartBattle = Assert.IsType<StartBattleInstruction>(rereadScript.Instructions.First(instruction => instruction is StartBattleInstruction));
        Assert.Equal(0x2C, rereadStartBattle.Battle.Value);

        Assert.Equal(editedBattle.CharacterId, rereadBattle.CharacterId);
        Assert.Equal(editedPart.Armor, rereadPart.Armor);

        Assert.Equal(overworldSprite.Image.PixelIndices, rereadSprite.Image.PixelIndices);
        Assert.Equal(overworldSprite.Image.PaletteBytes, rereadSprite.Image.PaletteBytes);
        Assert.Equal(portrait.Image.PixelIndices, rereadPortrait.Image.PixelIndices);
        Assert.Equal(portrait.Image.PaletteBytes, rereadPortrait.Image.PaletteBytes);
        Assert.Equal(component.Image.PixelIndices, rereadComponent.Image.PixelIndices);
        Assert.Equal(component.Image.PaletteBytes, rereadComponent.Image.PaletteBytes);
        Assert.Equal(component.PaletteFamily, rereadComponent.PaletteFamily);
        Assert.Equal(largeDisplay.Pieces[0].Image.PixelIndices, rereadLargeDisplay.Pieces[0].Image.PixelIndices);
        Assert.Equal(largeDisplay.Pieces[0].PaletteBytes, rereadLargeDisplay.Pieces[0].PaletteBytes);

        Assert.Equal(editedLayerEntries, rereadMap.Layers[0].TileEntries);
        Assert.Equal(editedCollision, rereadMap.CollisionBytes);
        Assert.Equal((byte)0x2A, rereadMap.MusicId);
        Assert.False(rereadMap.HasEncounters);
        Assert.Equal([0x00, 0x02, 0x04], rereadMap.EventObjectResourceIds);
        Assert.Equal(editedSpawn, rereadOverlay.EntitySpawns[0]);
        Assert.Equal(editedWarp, rereadOverlay.Warps[0]);
        Assert.Equal((byte)1, rereadEncounter.Battle1);
        Assert.Equal((byte)2, rereadEncounter.Battle2);
        Assert.Equal((byte)3, rereadEncounter.Battle3);
        Assert.Equal((byte)4, rereadEncounter.Battle4);
    }

    private static async Task<RomHackProject> RoundTripProjectAsync(RomHackProject project)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.medahack.json");
        try
        {
            await RomHackProjectSerializer.SaveAsync(project, tempFile);
            return await RomHackProjectSerializer.LoadAsync(tempFile);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
