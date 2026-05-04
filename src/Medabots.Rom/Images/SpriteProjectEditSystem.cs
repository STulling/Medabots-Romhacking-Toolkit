using Medabots.Rom.Parts;
using Medabots.Rom.Projects;

namespace Medabots.Rom.Images;

internal sealed class SpriteProjectEditSystem : IProjectEditSystem
{
    private readonly ImageAssetPatcher _imageAssetPatcher;
    private readonly ImageAssetRepository _imageAssetRepository = new();

    public SpriteProjectEditSystem(ImageAssetPatcher imageAssetPatcher)
    {
        _imageAssetPatcher = imageAssetPatcher;
    }

    public string DisplayName => "Sprite";

    public IEnumerable<string> DescribeChanges(RomHackProject project) =>
        project.OverworldSpriteEdits.Select(edit => $"Overworld {edit.SpriteId:D3}")
            .Concat(project.PortraitEdits.Select(edit => $"Portrait {edit.CharacterId:D3}:{edit.PortraitIndex:D2}"))
            .Concat(project.BattleCompositeSpriteEdits.Select(edit => $"Battle {edit.MedabotId:D3}/{edit.ComponentIndex}"))
            .Concat(project.LargePartDisplayEdits.Select(edit => $"Large {edit.PartId:D3}/{edit.VariantSelector}"));

    public IEnumerable<ProjectChange> BuildChanges(RomHackProject project, ProjectBuildContext context)
    {
        var changes = new List<ProjectChange>();

        changes.AddRange(project.OverworldSpriteEdits
            .OrderBy(asset => asset.SpriteId)
            .Select(asset => new ProjectChange(DisplayName, $"Overworld {asset.SpriteId}", _imageAssetPatcher.BuildSpriteSmartActions(context.SourceRom, asset, context.Allocator))));

        changes.AddRange(project.PortraitEdits
            .OrderBy(asset => asset.CharacterId)
            .ThenBy(asset => asset.PortraitIndex)
            .Select(asset => new ProjectChange(DisplayName, $"Portrait {asset.CharacterId}:{asset.PortraitIndex}", _imageAssetPatcher.BuildPortraitSmartActions(context.SourceRom, asset, context.Allocator))));

        changes.AddRange(project.BattleCompositeSpriteEdits
            .OrderBy(asset => asset.MedabotId)
            .ThenBy(asset => asset.ComponentIndex)
            .Select(asset => new ProjectChange(DisplayName, $"Battle {asset.MedabotId}/{asset.ComponentIndex}", _imageAssetPatcher.BuildBattleCompositeSpriteComponentSmartActions(context.SourceRom, asset, context.Allocator))));

        var sourceParts = new PartTableReader()
            .ReadAll(context.SourceRom)
            .ToDictionary(part => part.Id);

        var handledLargeDisplays = new HashSet<(int PartId, int VariantSelector)>();
        foreach (var groupedPart in project.LargePartDisplayEdits
                     .OrderBy(entry => entry.PartId)
                     .ThenBy(entry => entry.VariantSelector)
                     .GroupBy(entry => entry.PartId))
        {
            var representative = groupedPart.First();
            if (representative.Kind is not PartKind.RightArm and not PartKind.LeftArm)
            {
                continue;
            }

            if (!sourceParts.TryGetValue(representative.PartId, out var sourcePart))
            {
                continue;
            }

            var actions = BuildArmLargeDisplayActions(groupedPart.ToArray(), sourcePart, context);
            if (actions.Count > 0)
            {
                changes.Add(new ProjectChange(DisplayName, $"Large {representative.PartId} arm variants", actions));
            }

            foreach (var asset in groupedPart)
            {
                handledLargeDisplays.Add((asset.PartId, asset.VariantSelector));
            }
        }

        changes.AddRange(project.LargePartDisplayEdits
            .Where(asset => !handledLargeDisplays.Contains((asset.PartId, asset.VariantSelector)))
            .OrderBy(entry => entry.PartId)
            .ThenBy(entry => entry.VariantSelector)
            .Select(asset =>
            {
                if (!sourceParts.TryGetValue(asset.PartId, out var sourcePart))
                {
                    return null;
                }

                var sourceAsset = _imageAssetRepository.ReadLargePartDisplay(context.SourceRom, sourcePart, asset.VariantSelector);
                var changedAsset = FilterLargeDisplayAssetToChangedPieces(asset, sourceAsset);
                var actions = new List<RomPatchAction>();
                actions.AddRange(BuildForcedIndependentDescriptorActions(changedAsset, context.SourceRom));
                if (changedAsset.Pieces.Count > 0)
                {
                    actions.AddRange(_imageAssetPatcher.BuildLargePartDisplaySmartActions(context.SourceRom, changedAsset, context.Allocator));
                }

                if (actions.Count == 0)
                {
                    return null;
                }

                return new ProjectChange(
                    DisplayName,
                    $"Large {asset.PartId}/{asset.VariantSelector}",
                    actions);
            })
            .Where(change => change is not null)!);

        return changes;
    }

    private IReadOnlyList<RomPatchAction> BuildArmLargeDisplayActions(
        IReadOnlyList<LargePartDisplayAsset> stagedAssets,
        PartDefinition sourcePart,
        ProjectBuildContext context)
    {
        var stagedByVariant = stagedAssets.ToDictionary(asset => asset.VariantSelector);
        var sourceAssetA = _imageAssetRepository.ReadLargePartDisplay(context.SourceRom, sourcePart, 0);
        var sourceAssetB = _imageAssetRepository.ReadLargePartDisplay(context.SourceRom, sourcePart, 1);
        var desiredAssetA = stagedByVariant.TryGetValue(0, out var stagedA) ? stagedA : sourceAssetA;
        var desiredAssetB = stagedByVariant.TryGetValue(1, out var stagedB) ? stagedB : sourceAssetB;

        var actions = new List<RomPatchAction>();
        actions.AddRange(BuildForcedIndependentDescriptorActions(desiredAssetA, context.SourceRom));
        actions.AddRange(BuildForcedIndependentDescriptorActions(desiredAssetB, context.SourceRom));
        var splitDescriptorIds = new HashSet<int>();
        var sourcePiecesA = sourceAssetA.Pieces.ToDictionary(piece => piece.DescriptorId);
        var sourcePiecesB = sourceAssetB.Pieces.ToDictionary(piece => piece.DescriptorId);
        var desiredPiecesA = desiredAssetA.Pieces.ToDictionary(piece => piece.DescriptorId);
        var desiredPiecesB = desiredAssetB.Pieces.ToDictionary(piece => piece.DescriptorId);

        foreach (var descriptorId in sourcePiecesA.Keys.Intersect(sourcePiecesB.Keys).OrderBy(id => id))
        {
            var sourcePieceA = sourcePiecesA[descriptorId];
            var sourcePieceB = sourcePiecesB[descriptorId];
            var desiredPieceA = desiredPiecesA[descriptorId];
            var desiredPieceB = desiredPiecesB[descriptorId];
            var sharesImagePointer = sourcePieceA.ImagePointerOffset > 0 && sourcePieceA.ImagePointerOffset == sourcePieceB.ImagePointerOffset;
            var sharesPalettePointer = sourcePieceA.PalettePointerOffset > 0 && sourcePieceA.PalettePointerOffset == sourcePieceB.PalettePointerOffset;
            var imagesDiffer = !desiredPieceA.Image.PixelIndices.SequenceEqual(desiredPieceB.Image.PixelIndices);
            var palettesDiffer = !desiredPieceA.PaletteBytes.SequenceEqual(desiredPieceB.PaletteBytes);
            if ((!sharesImagePointer || !imagesDiffer) && (!sharesPalettePointer || !palettesDiffer))
            {
                continue;
            }

            splitDescriptorIds.Add(descriptorId);
            if (sourcePieceA.AppearanceEntryOffset > 0)
            {
                var updatedEntry = BitConverter.ToUInt32(context.SourceRom.Data, sourcePieceA.AppearanceEntryOffset) | (1u << 15);
                actions.Add(RomPatchAction.Create(sourcePieceA.AppearanceEntryOffset, BitConverter.GetBytes(updatedEntry), $"Split large part display {sourcePart.Id} descriptor {descriptorId} variants"));
            }

            actions.AddRange(BuildLargeDisplayPieceActions(
                context.SourceRom,
                desiredAssetA,
                desiredPieceA,
                sourcePieceA.ImagePointerOffset,
                sourcePieceA.PalettePointerOffset,
                sourcePieceA.ImageOffset,
                sourcePieceA.PaletteOffset,
                context.Allocator));

            actions.AddRange(BuildLargeDisplayPieceActions(
                context.SourceRom,
                desiredAssetB,
                desiredPieceB,
                sourcePieceA.ImagePointerOffset + sizeof(ulong),
                sourcePieceA.PalettePointerOffset > 0 ? sourcePieceA.PalettePointerOffset + sizeof(ulong) : 0,
                sharesImagePointer ? 0 : sourcePieceB.ImageOffset,
                sharesPalettePointer ? 0 : sourcePieceB.PaletteOffset,
                context.Allocator));
        }

        foreach (var variant in stagedByVariant.OrderBy(entry => entry.Key))
        {
            var sourceAsset = variant.Key == 0 ? sourceAssetA : sourceAssetB;
            var changedAsset = FilterLargeDisplayAssetToChangedPieces(variant.Value, sourceAsset);
            if (changedAsset.Pieces.Count == 0)
            {
                continue;
            }

            var filteredPieces = variant.Value.Pieces
                .Where(piece => !splitDescriptorIds.Contains(piece.DescriptorId))
                .ToArray();
            if (filteredPieces.Length == 0)
            {
                continue;
            }

            actions.AddRange(_imageAssetPatcher.BuildLargePartDisplaySmartActions(
                context.SourceRom,
                changedAsset with
                {
                    Pieces = changedAsset.Pieces
                        .Where(piece => !splitDescriptorIds.Contains(piece.DescriptorId))
                        .ToArray()
                },
                context.Allocator));
        }

        return actions;
    }

    private static LargePartDisplayAsset FilterLargeDisplayAssetToChangedPieces(
        LargePartDisplayAsset stagedAsset,
        LargePartDisplayAsset sourceAsset)
    {
        var sourcePieces = sourceAsset.Pieces.ToDictionary(piece => piece.DescriptorId);
        var changedPieces = stagedAsset.Pieces
            .Where(piece =>
            {
                if (!sourcePieces.TryGetValue(piece.DescriptorId, out var sourcePiece))
                {
                    return true;
                }

                if (piece.ForceIndependentSource && !sourcePiece.ForceIndependentSource)
                {
                    return true;
                }

                return !piece.Image.PixelIndices.SequenceEqual(sourcePiece.Image.PixelIndices)
                       || !piece.DescriptorRecordBytes.SequenceEqual(sourcePiece.DescriptorRecordBytes)
                       || !piece.PaletteBytes.SequenceEqual(sourcePiece.PaletteBytes);
            })
            .ToArray();

        return stagedAsset with { Pieces = changedPieces };
    }

    private IReadOnlyList<RomPatchAction> BuildLargeDisplayPieceActions(
        RomFile romFile,
        LargePartDisplayAsset sourceAsset,
        LargePartDisplayPieceAsset sourcePiece,
        int imagePointerOffset,
        int palettePointerOffset,
        int imageOffset,
        int paletteOffset,
        FreeSpaceAllocator allocator)
    {
        var pieceAsset = sourceAsset with
        {
            Pieces =
            [
                sourcePiece with
                {
                    ImagePointerOffset = imagePointerOffset,
                    PalettePointerOffset = palettePointerOffset,
                    ImageOffset = imageOffset,
                    PaletteOffset = paletteOffset
                }
            ]
        };

        return _imageAssetPatcher.BuildLargePartDisplaySmartActions(romFile, pieceAsset, allocator);
    }

    private static IEnumerable<RomPatchAction> BuildForcedIndependentDescriptorActions(
        LargePartDisplayAsset asset,
        RomFile sourceRom)
    {
        foreach (var piece in asset.Pieces.Where(piece => piece.ForceIndependentSource && piece.AppearanceEntryOffset > 0))
        {
            var entry = BitConverter.ToUInt32(sourceRom.Data, piece.AppearanceEntryOffset);
            if ((entry & (1u << 15)) != 0)
            {
                continue;
            }

            yield return RomPatchAction.Create(
                piece.AppearanceEntryOffset,
                BitConverter.GetBytes(entry | (1u << 15)),
                $"Split shared large display descriptor {asset.PartId}#{piece.DescriptorId}");
        }
    }
}
