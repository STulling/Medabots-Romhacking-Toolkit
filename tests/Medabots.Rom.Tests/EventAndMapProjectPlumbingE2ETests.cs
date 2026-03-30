using Medabots.Rom.Battles;
using Medabots.Rom.Events;
using Medabots.Rom.Encounters;
using Medabots.Rom.Maps;
using Medabots.Rom.Metadata;
using Medabots.Rom.Parts;
using Medabots.Rom.Projects;
using Xunit;

namespace Medabots.Rom.Tests;

public sealed class EventAndMapProjectPlumbingE2ETests
{
    [Fact]
    public async Task EventScriptProjectEditor_ReusesDeletedEventId_AndApplicatorWritesReplacementScript()
    {
        var rom = await RomFile.LoadAsync(TestRomLocator.FindWorkspaceRom());
        var profile = Assert.IsType<MedabotsRomTextProfile>(MedabotsRomTextProfiles.Detect(rom));
        var project = new RomHackProject
        {
            Name = "Event Reuse",
            TextProfileId = profile.Id
        };

        var editor = new EventScriptProjectEditor();
        editor.DeleteEventScript(project, 40);

        var allocatedId = editor.AddNewEventScript(project, rom, profile, [MedabotsRomSchema.EventEndOpcode]);

        Assert.Equal(40, allocatedId);
        Assert.Empty(project.DeletedEventScriptIds);
        Assert.Single(project.EventScriptPatches);

        var session = RomHackSession.FromRomFile(new RomFile("event-reuse.gba", rom.Data.ToArray()));
        var applicator = new RomHackProjectApplicator();
        var reader = new EventScriptReader();

        applicator.Apply(project, session);

        var reread = reader.ReadById(session.RomFile, profile, allocatedId);
        Assert.True(reread.StartOffset >= 0x800000);
        Assert.Single(reread.Instructions);
        Assert.Equal(MedabotsRomSchema.EventEndOpcode, reread.Instructions[0].Opcode);
    }

    [Fact]
    public async Task EventScriptProjectEditor_AllocatesMapUnreferencedEventId_WhenNoDeletedIdsExist()
    {
        var rom = await RomFile.LoadAsync(TestRomLocator.FindWorkspaceRom());
        var profile = Assert.IsType<MedabotsRomTextProfile>(MedabotsRomTextProfiles.Detect(rom));
        var project = new RomHackProject
        {
            Name = "Event Unreferenced Reuse",
            TextProfileId = profile.Id
        };

        var editor = new EventScriptProjectEditor();
        var allocatedId = editor.AddNewEventScript(project, rom, profile, [MedabotsRomSchema.EventEndOpcode]);

        Assert.Equal(32, allocatedId);
        Assert.Single(project.EventScriptPatches);
        Assert.Equal((short)32, project.EventScriptPatches[0].EventId);
    }

    [Fact]
    public async Task EventScriptProjectEditor_AddFreshEventScript_UsesExpandedIdInsteadOfReusedMapEventId()
    {
        var rom = await RomFile.LoadAsync(TestRomLocator.FindWorkspaceRom());
        var profile = Assert.IsType<MedabotsRomTextProfile>(MedabotsRomTextProfiles.Detect(rom));
        var project = new RomHackProject
        {
            Name = "Fresh Event Allocation",
            TextProfileId = profile.Id
        };

        var editor = new EventScriptProjectEditor();
        var allocatedId = editor.AddFreshEventScript(project, rom, profile, [MedabotsRomSchema.EventEndOpcode]);

        Assert.Equal((short)profile.EventCount, allocatedId);
        Assert.Single(project.EventScriptPatches);
    }

    [Fact]
    public async Task Applicator_ExpandsEventScriptDatabase_ForEventIdsBeyondOriginalCount()
    {
        var rom = await RomFile.LoadAsync(TestRomLocator.FindWorkspaceRom());
        var profile = Assert.IsType<MedabotsRomTextProfile>(MedabotsRomTextProfiles.Detect(rom));
        var expandedEventId = (short)profile.EventCount;
        var project = new RomHackProject
        {
            Name = "Event Expansion",
            TextProfileId = profile.Id
        };

        project.EventScriptPatches.Add(new EventScriptPatch(expandedEventId, [0x04, 0x02, MedabotsRomSchema.EventEndOpcode]));

        var session = RomHackSession.FromRomFile(new RomFile("event-expansion.gba", rom.Data.ToArray()));
        var applicator = new RomHackProjectApplicator();
        var reader = new EventScriptReader();

        applicator.Apply(project, session);

        var installedTable = EventScriptReader.ResolveInstalledEventTable(session.RomFile.Data, profile);
        Assert.True(installedTable.EventCount > profile.EventCount);

        var reread = reader.ReadById(session.RomFile, profile, expandedEventId);
        Assert.Equal(expandedEventId, reread.EventId);
        Assert.True(reread.StartOffset >= 0x800000);
        Assert.Equal(2, reread.Instructions.Count);
        Assert.Equal((byte)0x04, reread.Instructions[0].Opcode);
        Assert.Equal(MedabotsRomSchema.EventEndOpcode, reread.Instructions[1].Opcode);
    }

    [Fact]
    public async Task MapEntitySpawnProjectEditor_ReusesDeletedOriginalIndex_AndApplicatorWritesReplacementList()
    {
        var rom = await RomFile.LoadAsync(TestRomLocator.FindWorkspaceRom());
        var overlayRepository = new MapOverlayRepository();
        var overlay = overlayRepository.ReadMap(rom, 16);
        Assert.NotEmpty(overlay.EntitySpawns);

        var project = new RomHackProject
        {
            Name = "Map Spawn Reuse"
        };

        var editor = new MapEntitySpawnProjectEditor();
        var deleted = editor.DeleteExistingEntitySpawnRecord(project, overlay, 0);
        Assert.Equal(overlay.EntitySpawns.Count - 1, deleted.Records.Count);
        Assert.Equal([0], deleted.DeletedOriginalIndices);

        var original = overlay.EntitySpawns[0];
        var replacement = new MapEntitySpawnRecord(
            original.TileX,
            original.TileY,
            (ushort)((original.RecordKind << 12) | ((original.EventOrObjectId + 1) & 0x0FFF)),
            original.SpriteAndFacingPacked,
            original.SpawnGroupIndex,
            original.ChapterVisibilityMask);

        var updated = editor.AddEntitySpawnRecord(project, overlay, replacement);
        Assert.Empty(updated.DeletedOriginalIndices);
        Assert.Equal(overlay.EntitySpawns.Count, updated.Records.Count);
        Assert.Equal(replacement, updated.Records[0]);

        var session = RomHackSession.FromRomFile(new RomFile("map-spawn-reuse.gba", rom.Data.ToArray()));
        var applicator = new RomHackProjectApplicator();

        applicator.Apply(project, session);

        var reread = overlayRepository.ReadMap(session.RomFile, 16);
        Assert.Equal(overlay.EntitySpawns.Count, reread.EntitySpawns.Count);
        Assert.Equal(replacement, reread.EntitySpawns[0]);
        Assert.True(reread.EntitySpawnDataOffset >= 0x800000);
    }

    [Fact]
    public async Task MapCollisionProjectEditor_StagesAndApplicatorRewritesCollisionTable()
    {
        var rom = await RomFile.LoadAsync(TestRomLocator.FindWorkspaceRom());
        var repository = new MapTilesetRepository();
        var map = repository.ReadMap(rom, 16);
        var originalBytes = map.CollisionBytes;
        Assert.NotEmpty(originalBytes);

        var editedBytes = originalBytes.ToArray();
        editedBytes[0] = (byte)(editedBytes[0] == 0x7F ? 0x01 : editedBytes[0] + 1);

        var project = new RomHackProject
        {
            Name = "Map Collision Patch"
        };

        var editor = new MapCollisionProjectEditor();
        editor.UpsertCollisionPatch(project, map.MapId, editedBytes);

        var session = RomHackSession.FromRomFile(new RomFile("map-collision.gba", rom.Data.ToArray()));
        var applicator = new RomHackProjectApplicator();

        applicator.Apply(project, session);

        var reread = repository.ReadMap(session.RomFile, map.MapId);
        Assert.Equal(editedBytes, reread.CollisionBytes);
        Assert.True(reread.CollisionDataOffset >= 0x800000);
    }

    [Fact]
    public async Task Applicator_WritesMapEncounterMusicAndSpriteSlotMetadata()
    {
        var rom = await RomFile.LoadAsync(TestRomLocator.FindWorkspaceRom());
        var repository = new MapTilesetRepository();
        var mapId = 16;
        var originalMap = repository.ReadMap(rom, mapId);
        var originalEncounterTable = new EncounterTableReader().ReadAll(rom);
        var originalEncounter = originalEncounterTable[mapId];

        var project = new RomHackProject
        {
            Name = "Map Metadata Patch"
        };

        new MapEncounterStateProjectEditor().UpsertEncounterStatePatch(project, mapId, 0);
        new MapEncounterProjectEditor().UpsertEncounterPatch(project, mapId, 1, 2, 3, 4);
        new MapMusicProjectEditor().UpsertMusicPatch(project, mapId, 0x2A);
        new MapEventObjectResourceProjectEditor().UpsertResourcePatch(project, mapId, [0x00, 0x02, 0x04, 0xFF]);

        var session = RomHackSession.FromRomFile(new RomFile("map-metadata.gba", rom.Data.ToArray()));
        var applicator = new RomHackProjectApplicator();
        applicator.Apply(project, session);

        var rereadMap = repository.ReadMap(session.RomFile, mapId);
        var rereadEncounter = new EncounterTableReader().ReadAll(session.RomFile)[mapId];

        Assert.Equal((byte)0x2A, rereadMap.MusicId);
        Assert.False(rereadMap.HasEncounters);
        Assert.Equal([0x00, 0x02, 0x04], rereadMap.EventObjectResourceIds);
        Assert.True(rereadMap.EventObjectResourceDataOffset >= 0x800000);
        Assert.Equal((byte)1, rereadEncounter.Battle1);
        Assert.Equal((byte)2, rereadEncounter.Battle2);
        Assert.Equal((byte)3, rereadEncounter.Battle3);
        Assert.Equal((byte)4, rereadEncounter.Battle4);
        Assert.NotEqual(originalEncounter.Battle1, rereadEncounter.Battle1);
        Assert.NotEqual(originalMap.MusicId, rereadMap.MusicId);
    }

    [Fact]
    public async Task Applicator_WritesProjectBackedBattlePartAndMapLayerEdits()
    {
        var rom = await RomFile.LoadAsync(TestRomLocator.FindWorkspaceRom());
        var profile = Assert.IsType<MedabotsRomTextProfile>(MedabotsRomTextProfiles.Detect(rom));
        var battleReader = new BattleTableReader();
        var partReader = new PartTableReader();
        var mapRepository = new MapTilesetRepository();
        var sourceBattle = battleReader.ReadAll(rom, profile)[0];
        var sourcePart = partReader.ReadAll(rom)[0];
        var sourceMap = mapRepository.ReadMap(rom, 16);

        var editedBattle = sourceBattle with
        {
            CharacterId = (byte)(sourceBattle.CharacterId == byte.MaxValue ? 0 : sourceBattle.CharacterId + 1)
        };
        var editedPart = sourcePart with
        {
            Armor = (byte)(sourcePart.Armor == byte.MaxValue ? 0 : sourcePart.Armor + 1)
        };
        var editedEntries = sourceMap.Layers[0].TileEntries.ToArray();
        editedEntries[0] ^= 0x0001;
        editedEntries[1] ^= 0x1000;

        var project = new RomHackProject
        {
            Name = "Unified Structured Edits",
            TextProfileId = profile.Id
        };
        project.BattleEdits.Add(editedBattle);
        project.PartEdits.Add(editedPart);
        project.MapLayerPatches.Add(new MapLayerPatch(
            sourceMap.MapId,
            sourceMap.Layers[0].LayerIndex,
            sourceMap.Layers[0].HeaderWidthInTiles,
            sourceMap.Layers[0].HeaderHeightInTiles,
            sourceMap.Layers[0].HeaderOriginX,
            sourceMap.Layers[0].HeaderOriginY,
            sourceMap.Layers[0].HeaderOriginX2,
            sourceMap.Layers[0].HeaderOriginY2,
            editedEntries));

        var session = RomHackSession.FromRomFile(new RomFile("unified-structured-edits.gba", rom.Data.ToArray()));
        new RomHackProjectApplicator().Apply(project, session);

        var rereadBattle = battleReader.ReadAll(session.RomFile, profile)[0];
        var rereadPart = partReader.ReadSingle(session.RomFile, sourcePart.Id, sourcePart.DataOffset);
        var rereadMap = mapRepository.ReadMap(session.RomFile, 16);

        Assert.Equal(editedBattle.CharacterId, rereadBattle.CharacterId);
        Assert.Equal(editedPart.Armor, rereadPart.Armor);
        Assert.Equal(editedEntries, rereadMap.Layers[0].TileEntries);
        Assert.True(rereadMap.Layers[0].DataOffset >= 0x800000);
    }
}
