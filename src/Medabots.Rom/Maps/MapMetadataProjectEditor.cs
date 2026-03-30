namespace Medabots.Rom.Maps;

public sealed class MapMetadataProjectEditor
{
    private readonly MapEncounterProjectEditor _encounterProjectEditor = new();
    private readonly MapEncounterStateProjectEditor _encounterStateProjectEditor = new();
    private readonly MapMusicProjectEditor _musicProjectEditor = new();
    private readonly MapEventObjectResourceProjectEditor _eventObjectResourceProjectEditor = new();

    public void StageMetadata(
        RomHackProject project,
        MapTilesetAsset sourceAsset,
        byte encounterEnabledByte,
        byte musicId,
        IReadOnlyList<byte> spriteResourceIds,
        IReadOnlyList<byte>? encounterBattleIds)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(sourceAsset);
        ArgumentNullException.ThrowIfNull(spriteResourceIds);

        var mapId = sourceAsset.MapId;

        if (encounterEnabledByte == sourceAsset.EncounterEnabledByte)
        {
            RemovePatch(project.MapEncounterStatePatches, patch => patch.MapId == mapId);
        }
        else
        {
            _encounterStateProjectEditor.UpsertEncounterStatePatch(project, mapId, encounterEnabledByte);
        }

        if (musicId == sourceAsset.MusicId)
        {
            RemovePatch(project.MapMusicPatches, patch => patch.MapId == mapId);
        }
        else
        {
            _musicProjectEditor.UpsertMusicPatch(project, mapId, musicId);
        }

        var normalizedSourceResources = sourceAsset.EventObjectResourceIds.Take(16)
            .Concat(Enumerable.Repeat((byte)0xFF, Math.Max(0, 16 - sourceAsset.EventObjectResourceIds.Count)))
            .Take(16)
            .ToArray();
        var normalizedEditedResources = spriteResourceIds.Take(16)
            .Concat(Enumerable.Repeat((byte)0xFF, Math.Max(0, 16 - spriteResourceIds.Count)))
            .Take(16)
            .ToArray();

        if (normalizedSourceResources.SequenceEqual(normalizedEditedResources))
        {
            RemovePatch(project.MapEventObjectResourcePatches, patch => patch.MapId == mapId);
        }
        else
        {
            _eventObjectResourceProjectEditor.UpsertResourcePatch(project, mapId, normalizedEditedResources);
        }

        if (encounterEnabledByte != 0 && encounterBattleIds is { Count: 4 })
        {
            _encounterProjectEditor.UpsertEncounterPatch(project, mapId, encounterBattleIds[0], encounterBattleIds[1], encounterBattleIds[2], encounterBattleIds[3]);
            return;
        }

        RemovePatch(project.MapEncounterPatches, patch => patch.MapId == mapId);
    }

    private static void RemovePatch<TPatch>(IList<TPatch> patches, Func<TPatch, bool> predicate)
    {
        var existing = patches.FirstOrDefault(predicate);
        if (existing is not null)
        {
            patches.Remove(existing);
        }
    }
}
