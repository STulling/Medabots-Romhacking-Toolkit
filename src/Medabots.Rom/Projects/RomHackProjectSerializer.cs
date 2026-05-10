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
        public int SchemaVersion { get; set; } = 13;

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

        public List<MapDimensionPatchDocument> MapDimensionPatches { get; set; } = [];

        public List<SpriteAssetDocument> OverworldSpriteEdits { get; set; } = [];

        public List<PortraitAssetDocument> PortraitEdits { get; set; } = [];

        public List<BattleCompositeSpriteComponentAssetDocument> BattleCompositeSpriteEdits { get; set; } = [];

        public List<LargePartDisplayAssetDocument> LargePartDisplayEdits { get; set; } = [];

        public List<BattleDefinitionDocument> BattleEdits { get; set; } = [];

        public List<PartDefinitionDocument> PartEdits { get; set; } = [];

        public List<ShopDefinitionDocument> ShopEdits { get; set; } = [];

        public List<StarterDefinitionDocument> StarterEdits { get; set; } = [];

        public List<MapLayerPatchDocument> MapLayerPatches { get; set; } = [];

        public List<int> SplitLargeDisplayPartIds { get; set; } = [];

        public RomHackProject ToProject(string? projectFilePath)
        {
            if (SchemaVersion is not 1 and not 2 and not 3 and not 4 and not 5 and not 6 and not 7 and not 8 and not 9 and not 10 and not 11 and not 12 and not 13)
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

            foreach (var patch in MapDimensionPatches)
            {
                project.MapDimensionPatches.Add(new Maps.MapDimensionPatch(
                    patch.MapId,
                    patch.WidthInTiles,
                    patch.HeightInTiles));
            }

            foreach (var asset in OverworldSpriteEdits)
            {
                project.OverworldSpriteEdits.Add(new Images.SpriteAsset(
                    asset.SpriteId,
                    asset.ImagePointerOffset,
                    asset.PalettePointerOffset,
                    asset.ImageOffset,
                    asset.PaletteOffset,
                    asset.Image.ToIndexedImage()));
            }

            foreach (var asset in PortraitEdits)
            {
                project.PortraitEdits.Add(new Images.PortraitAsset(
                    asset.CharacterId,
                    asset.PortraitIndex,
                    asset.ImagePointerOffset,
                    asset.PalettePointerOffset,
                    asset.ImageOffset,
                    asset.PaletteOffset,
                    asset.Image.ToIndexedImage()));
            }

            foreach (var asset in BattleCompositeSpriteEdits)
            {
                project.BattleCompositeSpriteEdits.Add(new Images.BattleCompositeSpriteComponentAsset(
                    asset.MedabotId,
                    asset.ComponentIndex,
                    asset.ImagePointerOffset,
                    asset.ImageOffset,
                    asset.PalettePointerOffset,
                    asset.PaletteOffset,
                    asset.PaletteFamily,
                    asset.AppearanceId,
                    asset.PaletteSelector,
                    asset.Image.ToIndexedImage()));
            }

            foreach (var asset in LargePartDisplayEdits)
            {
                project.LargePartDisplayEdits.Add(new Images.LargePartDisplayAsset(
                    asset.PartId,
                    asset.PartOrdinal,
                    asset.Kind,
                    asset.VariantSelector,
                    asset.RootDescriptorId,
                    asset.RootRecordOffset,
                    asset.InitialPaletteBanks.ToDictionary(entry => entry.Key, entry => Convert.FromBase64String(entry.ValueBase64)),
                    asset.Pieces.Select(piece => new Images.LargePartDisplayPieceAsset(
                        piece.DescriptorId,
                        piece.AppearanceEntryOffset,
                        piece.RecordOffset,
                        Convert.FromBase64String(piece.DescriptorRecordBytesBase64),
                        piece.ImagePointerOffset,
                        piece.PalettePointerOffset,
                        piece.ImageOffset,
                        piece.PaletteOffset,
                        Convert.FromBase64String(piece.PaletteBytesBase64),
                        piece.PaletteBank,
                        piece.X,
                        piece.Y,
                        piece.SiblingDescriptorId,
                        piece.ChildDescriptorId,
                        piece.RawWidth,
                        piece.RawHeight,
                        piece.SizeDivisors,
                        piece.LoadedTileCount,
                        piece.MirrorDisplayHorizontally,
                        piece.ForceIndependentSource,
                        piece.Image.ToIndexedImage())).ToArray()));
            }

            foreach (var battle in BattleEdits)
            {
                project.BattleEdits.Add(new Battles.BattleDefinition(
                    battle.Id,
                    battle.PointerOffset,
                    battle.DataOffset,
                    battle.CharacterId,
                    battle.InitializationMode,
                    battle.NumberOfBots,
                    battle.TemplateFlags,
                    battle.Bots.Select(bot => new Battles.BattleBot(
                        bot.HeadPartId,
                        bot.RightArmPartId,
                        bot.LeftArmPartId,
                        bot.LegsPartId,
                        bot.MedalId,
                        bot.MedalLevel,
                        bot.PackedSpecialitySeedByte0,
                        bot.PackedSpecialitySeedByte1,
                        bot.PackedSpecialitySeedByte2,
                        bot.PackedSpecialitySeedByte3,
                        bot.SpecialityCycleResetValue,
                        bot.ReservedZeroByte)).ToArray()));
            }

            foreach (var part in PartEdits)
            {
                project.PartEdits.Add(new Parts.PartDefinition(
                    part.Id,
                    part.MedabotId,
                    part.Kind,
                    part.DataOffset,
                    part.MedalCompatibility,
                    part.TechniqueOrLegType,
                    part.Speciality,
                    part.Gender,
                    part.Armor,
                    part.RateOfSuccessOrPropulsion,
                    part.PowerOrEvasion,
                    part.ChainReactionOrDefense,
                    part.AmountOfUsesOrProximity,
                    part.Unknown2,
                    part.Unknown3,
                    part.Unknown4,
                    part.Unknown5,
                    part.Unknown6,
                    part.Unknown7,
                    part.Unknown8));
            }

            foreach (var shop in ShopEdits)
            {
                project.ShopEdits.Add(new Shops.ShopDefinition(
                    shop.Id,
                    shop.DataOffset,
                    Convert.FromBase64String(shop.ContentsBase64)));
            }

            foreach (var starter in StarterEdits)
            {
                project.StarterEdits.Add(new Starter.StarterDefinition(
                    starter.PartsOffset,
                    starter.MedalOffset,
                    starter.PartId,
                    starter.MedalId,
                    starter.IsFemale));
            }

            foreach (var patch in MapLayerPatches)
            {
                project.MapLayerPatches.Add(new Maps.MapLayerPatch(
                    patch.MapId,
                    patch.LayerIndex,
                    patch.HeaderWidthInTiles,
                    patch.HeaderHeightInTiles,
                    patch.HeaderOriginX,
                    patch.HeaderOriginY,
                    patch.HeaderOriginX2,
                    patch.HeaderOriginY2,
                    patch.TileEntries));
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
                SourceRomPath = null,
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
                MapDimensionPatches = project.MapDimensionPatches
                    .Select(patch => new MapDimensionPatchDocument
                    {
                        MapId = patch.MapId,
                        WidthInTiles = patch.WidthInTiles,
                        HeightInTiles = patch.HeightInTiles
                    })
                    .ToList(),
                OverworldSpriteEdits = project.OverworldSpriteEdits
                    .Select(asset => new SpriteAssetDocument
                    {
                        SpriteId = asset.SpriteId,
                        ImagePointerOffset = asset.ImagePointerOffset,
                        PalettePointerOffset = asset.PalettePointerOffset,
                        ImageOffset = asset.ImageOffset,
                        PaletteOffset = asset.PaletteOffset,
                        Image = IndexedImageDocument.FromIndexedImage(asset.Image)
                    })
                    .ToList(),
                PortraitEdits = project.PortraitEdits
                    .Select(asset => new PortraitAssetDocument
                    {
                        CharacterId = asset.CharacterId,
                        PortraitIndex = asset.PortraitIndex,
                        ImagePointerOffset = asset.ImagePointerOffset,
                        PalettePointerOffset = asset.PalettePointerOffset,
                        ImageOffset = asset.ImageOffset,
                        PaletteOffset = asset.PaletteOffset,
                        Image = IndexedImageDocument.FromIndexedImage(asset.Image)
                    })
                    .ToList(),
                BattleCompositeSpriteEdits = project.BattleCompositeSpriteEdits
                    .Select(asset => new BattleCompositeSpriteComponentAssetDocument
                    {
                        MedabotId = asset.MedabotId,
                        ComponentIndex = asset.ComponentIndex,
                        ImagePointerOffset = asset.ImagePointerOffset,
                        ImageOffset = asset.ImageOffset,
                        PalettePointerOffset = asset.PalettePointerOffset,
                        PaletteOffset = asset.PaletteOffset,
                        PaletteFamily = asset.PaletteFamily,
                        AppearanceId = asset.AppearanceId,
                        PaletteSelector = asset.PaletteSelector,
                        Image = IndexedImageDocument.FromIndexedImage(asset.Image)
                    })
                    .ToList(),
                LargePartDisplayEdits = project.LargePartDisplayEdits
                    .Select(asset => new LargePartDisplayAssetDocument
                    {
                        PartId = asset.PartId,
                        PartOrdinal = asset.PartOrdinal,
                        Kind = asset.Kind,
                        VariantSelector = asset.VariantSelector,
                        RootDescriptorId = asset.RootDescriptorId,
                        RootRecordOffset = asset.RootRecordOffset,
                        InitialPaletteBanks = asset.InitialPaletteBanks
                            .Select(entry => new PaletteBankEntryDocument
                            {
                                Key = entry.Key,
                                ValueBase64 = Convert.ToBase64String(entry.Value)
                            })
                            .ToList(),
                        Pieces = asset.Pieces
                            .Select(piece => new LargePartDisplayPieceAssetDocument
                            {
                                DescriptorId = piece.DescriptorId,
                                AppearanceEntryOffset = piece.AppearanceEntryOffset,
                                RecordOffset = piece.RecordOffset,
                                DescriptorRecordBytesBase64 = Convert.ToBase64String(piece.DescriptorRecordBytes),
                                ImagePointerOffset = piece.ImagePointerOffset,
                                PalettePointerOffset = piece.PalettePointerOffset,
                                ImageOffset = piece.ImageOffset,
                                PaletteOffset = piece.PaletteOffset,
                                PaletteBytesBase64 = Convert.ToBase64String(piece.PaletteBytes),
                                PaletteBank = piece.PaletteBank,
                                X = piece.X,
                                Y = piece.Y,
                                SiblingDescriptorId = piece.SiblingDescriptorId,
                                ChildDescriptorId = piece.ChildDescriptorId,
                                RawWidth = piece.RawWidth,
                                RawHeight = piece.RawHeight,
                                SizeDivisors = piece.SizeDivisors,
                                LoadedTileCount = piece.LoadedTileCount,
                                MirrorDisplayHorizontally = piece.MirrorDisplayHorizontally,
                                ForceIndependentSource = piece.ForceIndependentSource,
                                Image = IndexedImageDocument.FromIndexedImage(piece.Image)
                            })
                            .ToList()
                    })
                    .ToList(),
                BattleEdits = project.BattleEdits
                    .Select(battle => new BattleDefinitionDocument
                    {
                        Id = battle.Id,
                        PointerOffset = battle.PointerOffset,
                        DataOffset = battle.DataOffset,
                        CharacterId = battle.CharacterId,
                        InitializationMode = battle.InitializationMode,
                        NumberOfBots = battle.NumberOfBots,
                        TemplateFlags = battle.TemplateFlags,
                        Bots = battle.Bots
                            .Select(bot => new BattleBotDocument
                            {
                                HeadPartId = bot.HeadPartId,
                                RightArmPartId = bot.RightArmPartId,
                                LeftArmPartId = bot.LeftArmPartId,
                                LegsPartId = bot.LegsPartId,
                                MedalId = bot.MedalId,
                                MedalLevel = bot.MedalLevel,
                                PackedSpecialitySeedByte0 = bot.PackedSpecialitySeedByte0,
                                PackedSpecialitySeedByte1 = bot.PackedSpecialitySeedByte1,
                                PackedSpecialitySeedByte2 = bot.PackedSpecialitySeedByte2,
                                PackedSpecialitySeedByte3 = bot.PackedSpecialitySeedByte3,
                                SpecialityCycleResetValue = bot.SpecialityCycleResetValue,
                                ReservedZeroByte = bot.ReservedZeroByte
                            })
                            .ToList()
                    })
                    .ToList(),
                PartEdits = project.PartEdits
                    .Select(part => new PartDefinitionDocument
                    {
                        Id = part.Id,
                        MedabotId = part.MedabotId,
                        Kind = part.Kind,
                        DataOffset = part.DataOffset,
                        MedalCompatibility = part.MedalCompatibility,
                        TechniqueOrLegType = part.TechniqueOrLegType,
                        Speciality = part.Speciality,
                        Gender = part.Gender,
                        Armor = part.Armor,
                        RateOfSuccessOrPropulsion = part.RateOfSuccessOrPropulsion,
                        PowerOrEvasion = part.PowerOrEvasion,
                        ChainReactionOrDefense = part.ChainReactionOrDefense,
                        AmountOfUsesOrProximity = part.AmountOfUsesOrProximity,
                        Unknown2 = part.Unknown2,
                        Unknown3 = part.Unknown3,
                        Unknown4 = part.Unknown4,
                        Unknown5 = part.Unknown5,
                        Unknown6 = part.Unknown6,
                        Unknown7 = part.Unknown7,
                        Unknown8 = part.Unknown8
                    })
                    .ToList(),
                ShopEdits = project.ShopEdits
                    .Select(shop => new ShopDefinitionDocument
                    {
                        Id = shop.Id,
                        DataOffset = shop.DataOffset,
                        ContentsBase64 = Convert.ToBase64String(shop.Contents)
                    })
                    .ToList(),
                StarterEdits = project.StarterEdits
                    .Select(starter => new StarterDefinitionDocument
                    {
                        PartsOffset = starter.PartsOffset,
                        MedalOffset = starter.MedalOffset,
                        PartId = starter.PartId,
                        MedalId = starter.MedalId,
                        IsFemale = starter.IsFemale
                    })
                    .ToList(),
                MapLayerPatches = project.MapLayerPatches
                    .Select(patch => new MapLayerPatchDocument
                    {
                        MapId = patch.MapId,
                        LayerIndex = patch.LayerIndex,
                        HeaderWidthInTiles = patch.HeaderWidthInTiles,
                        HeaderHeightInTiles = patch.HeaderHeightInTiles,
                        HeaderOriginX = patch.HeaderOriginX,
                        HeaderOriginY = patch.HeaderOriginY,
                        HeaderOriginX2 = patch.HeaderOriginX2,
                        HeaderOriginY2 = patch.HeaderOriginY2,
                        TileEntries = patch.TileEntries.ToArray()
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

    private sealed class SpriteAssetDocument
    {
        public int SpriteId { get; set; }
        public int ImagePointerOffset { get; set; }
        public int PalettePointerOffset { get; set; }
        public int ImageOffset { get; set; }
        public int PaletteOffset { get; set; }
        public IndexedImageDocument Image { get; set; } = new();
    }

    private sealed class PortraitAssetDocument
    {
        public int CharacterId { get; set; }
        public int PortraitIndex { get; set; }
        public int ImagePointerOffset { get; set; }
        public int PalettePointerOffset { get; set; }
        public int ImageOffset { get; set; }
        public int PaletteOffset { get; set; }
        public IndexedImageDocument Image { get; set; } = new();
    }

    private sealed class BattleCompositeSpriteComponentAssetDocument
    {
        public int MedabotId { get; set; }
        public int ComponentIndex { get; set; }
        public int ImagePointerOffset { get; set; }
        public int ImageOffset { get; set; }
        public int PalettePointerOffset { get; set; }
        public int PaletteOffset { get; set; }
        public byte PaletteFamily { get; set; }
        public byte AppearanceId { get; set; }
        public byte PaletteSelector { get; set; }
        public IndexedImageDocument Image { get; set; } = new();
    }

    private sealed class LargePartDisplayAssetDocument
    {
        public int PartId { get; set; }
        public int PartOrdinal { get; set; }
        public Parts.PartKind Kind { get; set; }
        public int VariantSelector { get; set; }
        public int RootDescriptorId { get; set; }
        public int RootRecordOffset { get; set; }
        public List<PaletteBankEntryDocument> InitialPaletteBanks { get; set; } = [];
        public List<LargePartDisplayPieceAssetDocument> Pieces { get; set; } = [];
    }

    private sealed class PaletteBankEntryDocument
    {
        public int Key { get; set; }
        public string ValueBase64 { get; set; } = string.Empty;
    }

    private sealed class LargePartDisplayPieceAssetDocument
    {
        public int DescriptorId { get; set; }
        public int AppearanceEntryOffset { get; set; }
        public int RecordOffset { get; set; }
        public string DescriptorRecordBytesBase64 { get; set; } = string.Empty;
        public int ImagePointerOffset { get; set; }
        public int PalettePointerOffset { get; set; }
        public int ImageOffset { get; set; }
        public int PaletteOffset { get; set; }
        public string PaletteBytesBase64 { get; set; } = string.Empty;
        public int PaletteBank { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public byte SiblingDescriptorId { get; set; }
        public byte ChildDescriptorId { get; set; }
        public byte RawWidth { get; set; }
        public byte RawHeight { get; set; }
        public byte SizeDivisors { get; set; }
        public int LoadedTileCount { get; set; }
        public bool MirrorDisplayHorizontally { get; set; }
        public bool ForceIndependentSource { get; set; }
        public IndexedImageDocument Image { get; set; } = new();
    }

    private sealed class IndexedImageDocument
    {
        public int TileWidth { get; set; }
        public int TileHeight { get; set; }
        public string PixelIndicesBase64 { get; set; } = string.Empty;
        public string PaletteBytesBase64 { get; set; } = string.Empty;

        public Images.IndexedImage ToIndexedImage() =>
            new(TileWidth, TileHeight, Convert.FromBase64String(PixelIndicesBase64), Convert.FromBase64String(PaletteBytesBase64));

        public static IndexedImageDocument FromIndexedImage(Images.IndexedImage image) =>
            new()
            {
                TileWidth = image.TileWidth,
                TileHeight = image.TileHeight,
                PixelIndicesBase64 = Convert.ToBase64String(image.PixelIndices),
                PaletteBytesBase64 = Convert.ToBase64String(image.PaletteBytes)
            };
    }

    private sealed class BattleDefinitionDocument
    {
        public int Id { get; set; }
        public int PointerOffset { get; set; }
        public int DataOffset { get; set; }
        public byte CharacterId { get; set; }
        public byte InitializationMode { get; set; }
        public byte NumberOfBots { get; set; }
        public byte TemplateFlags { get; set; }
        public List<BattleBotDocument> Bots { get; set; } = [];
    }

    private sealed class BattleBotDocument
    {
        public byte HeadPartId { get; set; }
        public byte RightArmPartId { get; set; }
        public byte LeftArmPartId { get; set; }
        public byte LegsPartId { get; set; }
        public byte MedalId { get; set; }
        public byte MedalLevel { get; set; }
        public byte PackedSpecialitySeedByte0 { get; set; }
        public byte PackedSpecialitySeedByte1 { get; set; }
        public byte PackedSpecialitySeedByte2 { get; set; }
        public byte PackedSpecialitySeedByte3 { get; set; }
        public byte SpecialityCycleResetValue { get; set; }
        public byte ReservedZeroByte { get; set; }
    }

    private sealed class PartDefinitionDocument
    {
        public int Id { get; set; }
        public int MedabotId { get; set; }
        public Parts.PartKind Kind { get; set; }
        public int DataOffset { get; set; }
        public byte MedalCompatibility { get; set; }
        public byte TechniqueOrLegType { get; set; }
        public byte Speciality { get; set; }
        public byte Gender { get; set; }
        public byte Armor { get; set; }
        public byte RateOfSuccessOrPropulsion { get; set; }
        public byte PowerOrEvasion { get; set; }
        public byte ChainReactionOrDefense { get; set; }
        public byte AmountOfUsesOrProximity { get; set; }
        public byte Unknown2 { get; set; }
        public byte Unknown3 { get; set; }
        public byte Unknown4 { get; set; }
        public byte Unknown5 { get; set; }
        public byte Unknown6 { get; set; }
        public byte Unknown7 { get; set; }
        public byte Unknown8 { get; set; }
    }

    private sealed class ShopDefinitionDocument
    {
        public int Id { get; set; }
        public int DataOffset { get; set; }
        public string ContentsBase64 { get; set; } = string.Empty;
    }

    private sealed class StarterDefinitionDocument
    {
        public int PartsOffset { get; set; }
        public int MedalOffset { get; set; }
        public byte PartId { get; set; }
        public byte MedalId { get; set; }
        public bool IsFemale { get; set; }
    }

    private sealed class MapLayerPatchDocument
    {
        public int MapId { get; set; }
        public int LayerIndex { get; set; }
        public ushort HeaderWidthInTiles { get; set; }
        public ushort HeaderHeightInTiles { get; set; }
        public ushort HeaderOriginX { get; set; }
        public ushort HeaderOriginY { get; set; }
        public ushort HeaderOriginX2 { get; set; }
        public ushort HeaderOriginY2 { get; set; }
        public ushort[] TileEntries { get; set; } = [];
    }

    private sealed class MapDimensionPatchDocument
    {
        public int MapId { get; set; }
        public byte WidthInTiles { get; set; }
        public byte HeightInTiles { get; set; }
    }
}
