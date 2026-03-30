using System.Text.Json;
using System.Text.Json.Serialization;
using Medabots.Rom.Text;

namespace Medabots.Rom.Projects;

public static class RomHackProjectSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task<RomHackProject> LoadAsync(string projectFilePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFilePath);

        await using var stream = File.OpenRead(projectFilePath);
        var document = await JsonSerializer.DeserializeAsync<RomHackProjectDocument>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);

        if (document is null)
        {
            throw new InvalidDataException("The project file is empty or invalid.");
        }

        return document.ToProject(projectFilePath);
    }

    public static async Task SaveAsync(RomHackProject project, string projectFilePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFilePath);

        var directory = Path.GetDirectoryName(projectFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(projectFilePath);
        await JsonSerializer.SerializeAsync(stream, RomHackProjectDocument.FromProject(project), SerializerOptions, cancellationToken).ConfigureAwait(false);
    }

    private sealed class RomHackProjectDocument
    {
        public int SchemaVersion { get; set; } = 10;

        public string Name { get; set; } = "New Medabots Hack";

        public string? SourceRomPath { get; set; }

        public string? TextProfileId { get; set; }

        public List<RomPatchActionDocument> PendingActions { get; set; } = [];

        public List<MessagePatchDocument> MessagePatches { get; set; } = [];

        public List<EventLabelPatchDocument> EventLabels { get; set; } = [];

        public List<EventScriptPatchDocument> EventScriptPatches { get; set; } = [];

        public List<short> DeletedEventScriptIds { get; set; } = [];

        public List<MapEntitySpawnPatchDocument> MapEntitySpawnPatches { get; set; } = [];

        public List<MapWarpPatchDocument> MapWarpPatches { get; set; } = [];

        public List<MapCollisionPatchDocument> MapCollisionPatches { get; set; } = [];

        public List<MapEncounterPatchDocument> MapEncounterPatches { get; set; } = [];

        public List<MapEncounterStatePatchDocument> MapEncounterStatePatches { get; set; } = [];

        public List<MapMusicPatchDocument> MapMusicPatches { get; set; } = [];

        public List<MapEventObjectResourcePatchDocument> MapEventObjectResourcePatches { get; set; } = [];

        public List<int> SplitLargeDisplayPartIds { get; set; } = [];

        public RomHackProject ToProject(string? projectFilePath)
        {
            if (SchemaVersion is not 1 and not 2 and not 3 and not 4 and not 5 and not 6 and not 7 and not 8 and not 9 and not 10)
            {
                throw new InvalidDataException($"Unsupported project schema version '{SchemaVersion}'.");
            }

            var project = new RomHackProject
            {
                Name = Name,
                SourceRomPath = SourceRomPath,
                ProjectFilePath = projectFilePath,
                TextProfileId = TextProfileId
            };

            foreach (var action in PendingActions)
            {
                project.PendingActions.Add(new RomPatchAction(action.Offset, Convert.FromBase64String(action.DataBase64), action.Description));
            }

            foreach (var patch in MessagePatches)
            {
                project.MessagePatches.Add(new MessagePatch(new MessageId(patch.Bank, patch.Index), patch.Text));
            }

            foreach (var label in EventLabels)
            {
                project.EventLabels.Add(new EventLabelPatch((short)label.EventId, label.Offset, label.Label));
            }

            foreach (var patch in EventScriptPatches)
            {
                project.EventScriptPatches.Add(new EventScriptPatch((short)patch.EventId, Convert.FromBase64String(patch.ScriptBytesBase64)));
            }

            foreach (var eventId in DeletedEventScriptIds.Distinct().OrderBy(id => id))
            {
                project.DeletedEventScriptIds.Add(eventId);
            }

            foreach (var patch in MapEntitySpawnPatches)
            {
                project.MapEntitySpawnPatches.Add(new Maps.MapEntitySpawnPatch(
                    patch.MapId,
                    patch.Records.Select(record => new Maps.MapEntitySpawnRecord(
                        record.TileX,
                        record.TileY,
                        record.RecordKindAndEventId,
                        record.SpriteAndFacingPacked,
                        record.SpawnGroupIndex,
                        record.ChapterVisibilityMask)),
                    patch.DeletedOriginalIndices));
            }

            foreach (var patch in MapWarpPatches)
            {
                project.MapWarpPatches.Add(new Maps.MapWarpPatch(
                    patch.MapId,
                    patch.Records.Select(record => new Maps.MapWarpRecord(
                        record.TileX,
                        record.TileY,
                        record.DestinationMapId,
                        record.ArrivalFacingAndTransitionKind,
                        record.Unknown4,
                        record.Unknown5,
                        record.DestinationTileX,
                        record.DestinationTileY)),
                    patch.DeletedOriginalIndices));
            }

            foreach (var patch in MapCollisionPatches)
            {
                project.MapCollisionPatches.Add(new Maps.MapCollisionPatch(
                    patch.MapId,
                    Convert.FromBase64String(patch.ColorAttributeBytesBase64)));
            }

            foreach (var patch in MapEncounterPatches)
            {
                project.MapEncounterPatches.Add(new Maps.MapEncounterPatch(
                    patch.MapId,
                    patch.Battle1,
                    patch.Battle2,
                    patch.Battle3,
                    patch.Battle4));
            }

            foreach (var patch in MapEncounterStatePatches)
            {
                project.MapEncounterStatePatches.Add(new Maps.MapEncounterStatePatch(
                    patch.MapId,
                    patch.EncounterEnabledByte));
            }

            foreach (var patch in MapMusicPatches)
            {
                project.MapMusicPatches.Add(new Maps.MapMusicPatch(
                    patch.MapId,
                    patch.MusicId));
            }

            foreach (var patch in MapEventObjectResourcePatches)
            {
                project.MapEventObjectResourcePatches.Add(new Maps.MapEventObjectResourcePatch(
                    patch.MapId,
                    patch.ResourceIds.ToArray()));
            }

            foreach (var partId in SplitLargeDisplayPartIds.Distinct())
            {
                project.SplitLargeDisplayPartIds.Add(partId);
            }

            return project;
        }

        public static RomHackProjectDocument FromProject(RomHackProject project)
        {
            return new RomHackProjectDocument
            {
                Name = project.Name,
                SourceRomPath = project.SourceRomPath,
                TextProfileId = project.TextProfileId,
                PendingActions = project.PendingActions
                    .Select(action => new RomPatchActionDocument
                    {
                        Offset = action.Offset,
                        DataBase64 = Convert.ToBase64String(action.Data),
                        Description = action.Description
                    })
                    .ToList(),
                MessagePatches = project.MessagePatches
                    .Select(patch => new MessagePatchDocument
                    {
                        Bank = patch.Id.Bank,
                        Index = patch.Id.Index,
                        Text = patch.Text
                    })
                    .ToList(),
                EventLabels = project.EventLabels
                    .Select(label => new EventLabelPatchDocument
                    {
                        EventId = label.EventId,
                        Offset = label.Offset,
                        Label = label.Label
                    })
                    .ToList(),
                EventScriptPatches = project.EventScriptPatches
                    .Select(patch => new EventScriptPatchDocument
                    {
                        EventId = patch.EventId,
                        ScriptBytesBase64 = Convert.ToBase64String(patch.ScriptBytes)
                    })
                    .ToList(),
                DeletedEventScriptIds = project.DeletedEventScriptIds
                    .Distinct()
                    .OrderBy(id => id)
                    .ToList(),
                MapEntitySpawnPatches = project.MapEntitySpawnPatches
                    .Select(patch => new MapEntitySpawnPatchDocument
                    {
                        MapId = patch.MapId,
                        DeletedOriginalIndices = patch.DeletedOriginalIndices.Distinct().OrderBy(index => index).ToList(),
                        Records = patch.Records
                            .Select(record => new MapEntitySpawnRecordDocument
                            {
                                TileX = record.TileX,
                                TileY = record.TileY,
                                RecordKindAndEventId = record.RecordKindAndEventId,
                                SpriteAndFacingPacked = record.SpriteAndFacingPacked,
                                SpawnGroupIndex = record.SpawnGroupIndex,
                                ChapterVisibilityMask = record.ChapterVisibilityMask
                            })
                            .ToList()
                    })
                    .ToList(),
                MapWarpPatches = project.MapWarpPatches
                    .Select(patch => new MapWarpPatchDocument
                    {
                        MapId = patch.MapId,
                        DeletedOriginalIndices = patch.DeletedOriginalIndices.Distinct().OrderBy(index => index).ToList(),
                        Records = patch.Records
                            .Select(record => new MapWarpRecordDocument
                            {
                                TileX = record.TileX,
                                TileY = record.TileY,
                                DestinationMapId = record.DestinationMapId,
                                ArrivalFacingAndTransitionKind = record.ArrivalFacingAndTransitionKind,
                                Unknown4 = record.Unknown4,
                                Unknown5 = record.Unknown5,
                                DestinationTileX = record.DestinationTileX,
                                DestinationTileY = record.DestinationTileY
                            })
                            .ToList()
                    })
                    .ToList(),
                MapCollisionPatches = project.MapCollisionPatches
                    .Select(patch => new MapCollisionPatchDocument
                    {
                        MapId = patch.MapId,
                        ColorAttributeBytesBase64 = Convert.ToBase64String(patch.ColorAttributeBytes)
                    })
                    .ToList(),
                MapEncounterPatches = project.MapEncounterPatches
                    .Select(patch => new MapEncounterPatchDocument
                    {
                        MapId = patch.MapId,
                        Battle1 = patch.Battle1,
                        Battle2 = patch.Battle2,
                        Battle3 = patch.Battle3,
                        Battle4 = patch.Battle4
                    })
                    .ToList(),
                MapEncounterStatePatches = project.MapEncounterStatePatches
                    .Select(patch => new MapEncounterStatePatchDocument
                    {
                        MapId = patch.MapId,
                        EncounterEnabledByte = patch.EncounterEnabledByte
                    })
                    .ToList(),
                MapMusicPatches = project.MapMusicPatches
                    .Select(patch => new MapMusicPatchDocument
                    {
                        MapId = patch.MapId,
                        MusicId = patch.MusicId
                    })
                    .ToList(),
                MapEventObjectResourcePatches = project.MapEventObjectResourcePatches
                    .Select(patch => new MapEventObjectResourcePatchDocument
                    {
                        MapId = patch.MapId,
                        ResourceIds = patch.ResourceIds.ToList()
                    })
                    .ToList(),
                SplitLargeDisplayPartIds = project.SplitLargeDisplayPartIds
                    .Distinct()
                    .ToList()
            };
        }
    }

    private sealed class MessagePatchDocument
    {
        public int Bank { get; set; }

        public int Index { get; set; }

        public string Text { get; set; } = string.Empty;
    }

    private sealed class RomPatchActionDocument
    {
        public int Offset { get; set; }

        public string DataBase64 { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;
    }

    private sealed class EventLabelPatchDocument
    {
        public int EventId { get; set; }

        public int Offset { get; set; }

        public string Label { get; set; } = string.Empty;
    }

    private sealed class EventScriptPatchDocument
    {
        public int EventId { get; set; }

        public string ScriptBytesBase64 { get; set; } = string.Empty;
    }

    private sealed class MapEntitySpawnPatchDocument
    {
        public int MapId { get; set; }

        public List<int> DeletedOriginalIndices { get; set; } = [];

        public List<MapEntitySpawnRecordDocument> Records { get; set; } = [];
    }

    private sealed class MapEntitySpawnRecordDocument
    {
        public byte TileX { get; set; }

        public byte TileY { get; set; }

        public ushort RecordKindAndEventId { get; set; }

        public byte SpriteAndFacingPacked { get; set; }

        public byte SpawnGroupIndex { get; set; }

        public ushort ChapterVisibilityMask { get; set; }
    }

    private sealed class MapWarpPatchDocument
    {
        public int MapId { get; set; }

        public List<int> DeletedOriginalIndices { get; set; } = [];

        public List<MapWarpRecordDocument> Records { get; set; } = [];
    }

    private sealed class MapWarpRecordDocument
    {
        public byte TileX { get; set; }

        public byte TileY { get; set; }

        public byte DestinationMapId { get; set; }

        public byte ArrivalFacingAndTransitionKind { get; set; }

        public byte Unknown4 { get; set; }

        public byte Unknown5 { get; set; }

        public byte DestinationTileX { get; set; }

        public byte DestinationTileY { get; set; }
    }

    private sealed class MapCollisionPatchDocument
    {
        public int MapId { get; set; }

        public string ColorAttributeBytesBase64 { get; set; } = string.Empty;
    }

    private sealed class MapEncounterPatchDocument
    {
        public int MapId { get; set; }

        public byte Battle1 { get; set; }

        public byte Battle2 { get; set; }

        public byte Battle3 { get; set; }

        public byte Battle4 { get; set; }
    }

    private sealed class MapEncounterStatePatchDocument
    {
        public int MapId { get; set; }

        public byte EncounterEnabledByte { get; set; }
    }

    private sealed class MapMusicPatchDocument
    {
        public int MapId { get; set; }

        public byte MusicId { get; set; }
    }

    private sealed class MapEventObjectResourcePatchDocument
    {
        public int MapId { get; set; }

        public List<byte> ResourceIds { get; set; } = [];
    }
}
