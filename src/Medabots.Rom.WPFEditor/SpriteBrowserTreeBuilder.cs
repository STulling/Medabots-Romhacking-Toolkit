using Medabots.Rom.Images;
using Medabots.Rom.Maps;
using Medabots.Rom.Metadata;
using Medabots.Rom.Parts;
using Medabots.Rom.Projects;
using Medabots.Rom.WPFEditor.Models;

namespace Medabots.Rom.WPFEditor;

internal sealed class SpriteBrowserTreeBuilder(
    RomFile? romFile,
    IReadOnlyList<PartDefinition> loadedParts,
    MedabotsMetadata metadata,
    ImageAssetRepository imageAssetRepository,
    MapTilesetRepository? mapTilesetRepository = null)
{
    private readonly MapTilesetRepository _mapTilesetRepository = mapTilesetRepository ?? new MapTilesetRepository();

    public List<SpriteBrowserNode> BuildTreeNodes()
    {
        var nodes = new List<SpriteBrowserNode>();
        nodes.Add(BuildOverworldSpriteRoot());
        nodes.Add(BuildPortraitRoot());
        nodes.Add(BuildMapTilesetRoot());
        nodes.Add(BuildMedabotPartRoot());
        return nodes;
    }

    public bool AreLargeDisplayVariantsIdentical(PartDefinition part)
    {
        if (romFile is null || part.Kind is not PartKind.RightArm and not PartKind.LeftArm)
        {
            return false;
        }

        var variantEntries = PartSpriteDisplayLayout.GetPreviewComponentEntriesForPartKind(part.Kind);
        if (variantEntries.Count < 2)
        {
            return false;
        }

        var assetA = imageAssetRepository.ReadLargePartDisplay(romFile, part, PartSpriteDisplayLayout.GetLargeDisplayVariantSelectorForComponent(part.Kind, variantEntries[0].ComponentIndex));
        var assetB = imageAssetRepository.ReadLargePartDisplay(romFile, part, PartSpriteDisplayLayout.GetLargeDisplayVariantSelectorForComponent(part.Kind, variantEntries[1].ComponentIndex));
        return PartSpriteDisplayLayout.AreEquivalentLargeDisplayAssets(assetA, assetB);
    }

    private SpriteBrowserNode BuildOverworldSpriteRoot()
    {
        const int groupSize = 32;
        const int characterSpriteLimit = 88;
        var root = new SpriteBrowserNode
        {
            Title = "Overworld Event Object Sheets",
            FilterText = "Overworld Event Object Sheets"
        };
        root.Children.Add(BuildOverworldSpriteSubgroup("Character Sheets", Enumerable.Range(0, characterSpriteLimit), groupSize));
        var validOtherIds = GetValidOverworldSheetIds(characterSpriteLimit, MedabotsRomSchema.SpriteCount - 1).ToArray();
        if (validOtherIds.Length > 0)
        {
            root.Children.Add(BuildOverworldSpriteSubgroup("Other Event Object Sheets", validOtherIds, groupSize));
        }
        return root;
    }

    private static SpriteBrowserNode BuildOverworldSpriteSubgroup(string title, IEnumerable<int> spriteIds, int groupSize)
    {
        var root = new SpriteBrowserNode
        {
            Title = title,
            FilterText = title
        };
        var ids = spriteIds.OrderBy(id => id).ToArray();
        for (var blockStart = 0; blockStart < ids.Length; blockStart += groupSize)
        {
            var block = ids.Skip(blockStart).Take(groupSize).ToArray();
            var start = block.First();
            var end = block.Last();
            var group = new SpriteBrowserNode
            {
                Title = $"Sheets {start:D3}-{end:D3}",
                FilterText = $"{title} {start:D3} {end:D3}"
            };

            foreach (var spriteId in block)
            {
                group.Children.Add(new SpriteBrowserNode
                {
                    Title = $"Sheet {spriteId:D3}",
                    FilterText = $"{title} {spriteId:D3} Overworld Sheet",
                    AssetKind = SpriteAssetKind.OverworldEventObject,
                    PrimaryId = spriteId
                });
            }

            root.Children.Add(group);
        }

        return root;
    }

    private IEnumerable<int> GetValidOverworldSheetIds(int firstId, int lastId)
    {
        if (romFile is null)
        {
            yield break;
        }

        for (var spriteId = firstId; spriteId <= lastId; spriteId++)
        {
            if (HasValidOverworldSheetPointers(romFile, spriteId))
            {
                yield return spriteId;
            }
        }
    }

    private static bool HasValidOverworldSheetPointers(RomFile romFile, int spriteId)
    {
        var imagePointerOffset = MedabotsRomSchema.SpritePointerTableOffset + (spriteId * sizeof(uint));
        var palettePointerOffset = MedabotsRomSchema.SpritePaletteTableOffset + (spriteId * sizeof(uint));
        return GbaPointer.TryReadFileOffset(romFile.Data, imagePointerOffset, out var imageOffset) &&
               GbaPointer.TryReadFileOffset(romFile.Data, palettePointerOffset, out var paletteOffset) &&
               imageOffset > 0 &&
               paletteOffset > 0 &&
               imageOffset < romFile.Length &&
               paletteOffset + MedabotsRomSchema.PaletteSize <= romFile.Length;
    }

    private static SpriteBrowserNode BuildPortraitRoot()
    {
        const int groupSize = 16;
        var root = new SpriteBrowserNode
        {
            Title = "Portraits",
            FilterText = "Portraits"
        };
        for (var start = 0; start < MedabotsRomSchema.PortraitCharacterCount; start += groupSize)
        {
            var end = Math.Min(start + groupSize - 1, MedabotsRomSchema.PortraitCharacterCount - 1);
            var group = new SpriteBrowserNode
            {
                Title = $"Characters {start:D3}-{end:D3}",
                FilterText = $"Portrait Character {start:D3} {end:D3}"
            };

            for (var characterId = start; characterId <= end; characterId++)
            {
                var character = new SpriteBrowserNode
                {
                    Title = $"Character {characterId:D3}",
                    FilterText = $"Portrait Character {characterId:D3}"
                };
                for (var portraitIndex = 0; portraitIndex < MedabotsRomSchema.PortraitsPerCharacter; portraitIndex++)
                {
                    character.Children.Add(new SpriteBrowserNode
                    {
                        Title = $"Portrait {portraitIndex}",
                        FilterText = $"Portrait Character {characterId:D3} Portrait {portraitIndex}",
                        AssetKind = SpriteAssetKind.Portrait,
                        PrimaryId = characterId,
                        SecondaryId = portraitIndex
                    });
                }

                group.Children.Add(character);
            }

            root.Children.Add(group);
        }

        return root;
    }

    private SpriteBrowserNode BuildMapTilesetRoot()
    {
        var root = new SpriteBrowserNode
        {
            Title = "Map Tilesets",
            FilterText = "Map Tilesets"
        };

        if (romFile is null)
        {
            return root;
        }

        var groups = Enumerable.Range(0, Math.Min(MedabotsRomSchema.MapCount, metadata.Catalog.Maps.Count))
            .Select(mapId => _mapTilesetRepository.ReadMap(romFile, mapId, metadata.GetMapName(mapId)))
            .GroupBy(asset => (asset.GraphicsDataOffset, asset.PaletteDataOffset, asset.ColorAttributeDataOffset))
            .OrderBy(group => group.First().GraphicsDataOffset)
            .ToArray();

        var tilesetIndex = 0;
        foreach (var group in groups)
        {
            var representative = group.First();
            var mapIds = group.Select(asset => asset.MapId).OrderBy(id => id).ToArray();
            root.Children.Add(new SpriteBrowserNode
            {
                Title = $"Tileset {tilesetIndex:D3}  {metadata.GetMapName(representative.MapId)}",
                FilterText = $"Tileset {tilesetIndex:D3} {string.Join(' ', mapIds.Select(id => metadata.GetMapName(id)))}",
                AssetKind = SpriteAssetKind.MapTileset,
                PrimaryId = representative.MapId
            });
            tilesetIndex++;
        }

        return root;
    }

    private SpriteBrowserNode BuildMedabotPartRoot()
    {
        var root = new SpriteBrowserNode
        {
            Title = "Medabots",
            FilterText = "Medabots Parts"
        };

        var groupedByMedabot = loadedParts
            .GroupBy(part => part.MedabotId)
            .OrderBy(group => group.Key)
            .ToArray();

        foreach (var medabotGroup in groupedByMedabot)
        {
            var medabotNode = new SpriteBrowserNode
            {
                Title = $"{medabotGroup.Key:D3}  {metadata.GetBotName(medabotGroup.Key)}",
                FilterText = $"Medabot {medabotGroup.Key:D3} {metadata.GetBotName(medabotGroup.Key)}"
            };
            var botPreviewNode = new SpriteBrowserNode
            {
                Title = "Bot Preview",
                FilterText = $"Bot Preview Medabot {medabotGroup.Key:D3} {metadata.GetBotName(medabotGroup.Key)}"
            };
            botPreviewNode.Children.Add(new SpriteBrowserNode
            {
                Title = "Large Preview",
                FilterText = $"Large Preview Medabot {medabotGroup.Key:D3} {metadata.GetBotName(medabotGroup.Key)}",
                AssetKind = SpriteAssetKind.MedabotLargePreview,
                PrimaryId = medabotGroup.Key
            });
            botPreviewNode.Children.Add(new SpriteBrowserNode
            {
                Title = "Battle Preview",
                FilterText = $"Battle Preview Medabot {medabotGroup.Key:D3} {metadata.GetBotName(medabotGroup.Key)}",
                AssetKind = SpriteAssetKind.MedabotBattlePreview,
                PrimaryId = medabotGroup.Key
            });

            var battleDisplaysNode = new SpriteBrowserNode
            {
                Title = "Battle Displays",
                FilterText = $"Battle Displays Medabot {medabotGroup.Key:D3} {metadata.GetBotName(medabotGroup.Key)}"
            };
            var editableSpritesNode = new SpriteBrowserNode
            {
                Title = "Editable Sprites",
                FilterText = $"Editable Sprites Medabot {medabotGroup.Key:D3} {metadata.GetBotName(medabotGroup.Key)}"
            };
            var editableSprites = new Dictionary<int, EditableSpriteListEntry>();

            foreach (var part in medabotGroup.OrderBy(part => part.Kind).ThenBy(part => part.Id))
            {
                var partKindLabel = PartSpriteDisplayLayout.FormatPartKind(part.Kind);
                var previewEntries = PartSpriteDisplayLayout.GetPreviewComponentEntriesForPartKind(part.Kind);

                foreach (var (componentIndex, title) in previewEntries)
                {
                    battleDisplaysNode.Children.Add(new SpriteBrowserNode
                    {
                        Title = $"{partKindLabel}  {part.Id:D3}  {metadata.GetPartName(part.Id)}  {title}",
                        FilterText = $"{title} Part {part.Id:D3} {metadata.GetPartName(part.Id)} Medabot {metadata.GetBotName(part.MedabotId)} {partKindLabel}",
                        AssetKind = SpriteAssetKind.BattleCompositePartComponent,
                        PrimaryId = part.MedabotId,
                        SecondaryId = componentIndex
                    });
                }

                foreach (var (componentIndex, title) in previewEntries)
                {
                    var largeDisplayTitle = title.Replace("Battle Display", "Large Display");
                    foreach (var descriptor in GetLargeDisplayDescriptorRecords(part, componentIndex))
                    {
                        if (descriptor.ImageOffset > 0 && !editableSprites.ContainsKey(descriptor.ImageOffset))
                        {
                            editableSprites[descriptor.ImageOffset] = new EditableSpriteListEntry(part, componentIndex, largeDisplayTitle, descriptor);
                        }
                    }
                }
            }

            foreach (var sprite in editableSprites.Values.OrderBy(entry => entry.Record.ImageOffset).ThenBy(entry => entry.Record.RecordOffset))
            {
                editableSpritesNode.Children.Add(new SpriteBrowserNode
                {
                    Title = $"0x{sprite.Record.ImageOffset:X6}  {FormatPartKind(sprite.Part.Kind)}  {sprite.Part.Id:D3}  {metadata.GetPartName(sprite.Part.Id)}",
                    FilterText = $"Editable Sprite 0x{sprite.Record.ImageOffset:X6} Part {sprite.Part.Id:D3} {metadata.GetPartName(sprite.Part.Id)} Medabot {metadata.GetBotName(sprite.Part.MedabotId)} {FormatPartKind(sprite.Part.Kind)}",
                    AssetKind = SpriteAssetKind.PartCompositeEditableSprite,
                    PrimaryId = sprite.Part.MedabotId,
                    SecondaryId = sprite.Part.Id,
                    TertiaryId = sprite.ComponentIndex,
                    DataOffset = sprite.Record.ImageOffset,
                    SharedSourcePrimaryId = sprite.Record.RecordOffset
                });
            }

            medabotNode.Children.Add(botPreviewNode);
            medabotNode.Children.Add(battleDisplaysNode);
            medabotNode.Children.Add(editableSpritesNode);
            root.Children.Add(medabotNode);
        }

        return root;
    }

    private IReadOnlyList<LargePartDisplayDescriptorRecord> GetLargeDisplayDescriptorRecords(PartDefinition part, int componentIndex)
    {
        if (romFile is null)
        {
            return [];
        }

        var variantSelector = PartSpriteDisplayLayout.GetLargeDisplayVariantSelectorForComponent(part.Kind, componentIndex);
        return imageAssetRepository.ReadLargePartDisplayDescriptorRecords(romFile, part, variantSelector)
            .OrderBy(record => record.DescriptorId)
            .ToArray();
    }

    private sealed record EditableSpriteListEntry(PartDefinition Part, int ComponentIndex, string TitlePrefix, LargePartDisplayDescriptorRecord Record);

    private static string FormatPartKind(PartKind kind) => kind switch
    {
        PartKind.Head => "Head",
        PartKind.RightArm => "Right Arm",
        PartKind.LeftArm => "Left Arm",
        PartKind.Legs => "Legs",
        _ => kind.ToString()
    };
}
