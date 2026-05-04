using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Forms = System.Windows.Forms;
using WpfColor = System.Windows.Media.Color;
using WpfPoint = System.Windows.Point;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfButton = System.Windows.Controls.Button;
using WpfMenuItem = System.Windows.Controls.MenuItem;
using WpfCursors = System.Windows.Input.Cursors;
using WpfMessageBox = System.Windows.MessageBox;
using Win32OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using Win32SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using Medabots.Rom.Battles;
using Medabots.Rom.Editor;
using Medabots.Rom.Encounters;
using Medabots.Rom.Events;
using Medabots.Rom.Images;
using Medabots.Rom.Maps;
using Medabots.Rom.Metadata;
using Medabots.Rom.Parts;
using Medabots.Rom.Projects;
using Medabots.Rom.Shops;
using Medabots.Rom.Starter;
using Medabots.Rom.Text;
using Medabots.Rom.WPFEditor.Dialogs;
using Medabots.Rom.WPFEditor.Models;
using Microsoft.Win32;

namespace Medabots.Rom.WPFEditor;

public partial class MainWindow : Window
{
    private void ClearSpritePreview()
    {
        _selectedSpriteNode = null;
        SpritePreviewImage.Source = null;
        SpritePreviewImage.Width = double.NaN;
        SpritePreviewImage.Height = double.NaN;
        SpritePreviewSurface.Width = double.NaN;
        SpritePreviewSurface.Height = double.NaN;
        SpritePreviewImage.Margin = new Thickness(0);
        SpriteGridCanvas.Margin = new Thickness(0);
        SpriteGridCanvas.Children.Clear();
        SpriteGridCanvas.Width = 0;
        SpriteGridCanvas.Height = 0;
        SpriteSummaryLabel.Text = "Select an overworld sheet, portrait, map tileset, Medabot composite sprite, or individual part preview to inspect its decoded image and palette data.";
        SpritePaletteSummaryLabel.Text = string.Empty;
        SpritePaletteItemsControl.ItemsSource = null;
        LargeBotLivePreviewPanel.Visibility = Visibility.Collapsed;
        LargeBotLivePreviewImage.Source = null;
        LargeBotLivePreviewLabel.Text = string.Empty;
        SpritePaletteFamilyEditorPanel.Visibility = Visibility.Collapsed;
        SpritePaletteFamilyComboBox.SelectedItem = null;
        SpritePaletteFamilyHintLabel.Text = string.Empty;
        BotBattleFacingEditorPanel.Visibility = Visibility.Collapsed;
        BotBattleFacingComboBox.SelectedItem = null;
        ParsedDescriptorEditorPanel.Visibility = Visibility.Collapsed;
        SpritePatchStatusLabel.Text = string.Empty;
        _selectedPaletteIndex = 1;
        _hasCapturedUndoForCurrentStroke = false;
    }

    private List<SpriteBrowserNode> BuildSpriteTreeNodes() =>
        new SpriteBrowserTreeBuilder(_session?.RomFile, _loadedParts, _metadata, _imageAssetRepository, _mapTilesetRepository).BuildTreeNodes();

    private bool ShouldForceSplitLargeDisplay(int partId)
    {
        return _project.SplitLargeDisplayPartIds.Contains(partId);
    }

    private bool AreLargeDisplayVariantsIdentical(PartDefinition part) =>
        new SpriteBrowserTreeBuilder(_session?.RomFile, _loadedParts, _metadata, _imageAssetRepository).AreLargeDisplayVariantsIdentical(part);

    private async void OnSplitLargeDisplayMenuClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not WpfMenuItem { CommandParameter: SpriteBrowserNode node } || !TryGetSplitEligibleLargeDisplayPart(node, out var part))
        {
            return;
        }

        if (!_project.SplitLargeDisplayPartIds.Contains(part.Id))
        {
            _project.SplitLargeDisplayPartIds.Add(part.Id);
        }

        RefreshSpriteTreeForLargeDisplayLayoutChange(part.Id, node.SecondaryId);
        SpritePatchStatusLabel.Text = $"Status: split large display variants for part {part.Id:D3}. Save the project to keep this layout.";
        await Task.CompletedTask;
    }

    private async void OnMergeLargeDisplayMenuClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not WpfMenuItem { CommandParameter: SpriteBrowserNode node } || !TryGetSplitEligibleLargeDisplayPart(node, out var part))
        {
            return;
        }

        while (_project.SplitLargeDisplayPartIds.Remove(part.Id))
        {
        }

        RefreshSpriteTreeForLargeDisplayLayoutChange(part.Id, node.SecondaryId);
        SpritePatchStatusLabel.Text = $"Status: merged identical large display variants for part {part.Id:D3}. Save the project to keep this layout.";
        await Task.CompletedTask;
    }

    private async void OnSplitSharedLargeDisplayDescriptorMenuClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not WpfMenuItem { CommandParameter: SpriteBrowserNode node } || !TryGetSharedDescriptorSplitTarget(node, out var partId, out var componentIndex, out var descriptorId))
        {
            return;
        }

        var asset = GetEditableLargePartDisplayAsset(partId, componentIndex);
        var pieceIndex = Array.FindIndex(asset.Pieces.ToArray(), piece => piece.DescriptorId == descriptorId);
        if (pieceIndex < 0)
        {
            return;
        }

        var updatedPieces = asset.Pieces.ToArray();
        updatedPieces[pieceIndex] = updatedPieces[pieceIndex] with { ForceIndependentSource = true };
        var updatedAsset = asset with { Pieces = updatedPieces };
        _editedLargePartDisplayAssets[(partId, updatedAsset.VariantSelector)] = updatedAsset;
        StageLargeDisplayEdit(partId, updatedAsset.VariantSelector, updatedAsset);
        RefreshSpriteTreeForLargeDisplayLayoutChange(partId, componentIndex);
        RefreshSharedSpriteConsumers();
        SpritePatchStatusLabel.Text = $"Status: split shared descriptor {descriptorId:D2} for part {partId:D3}.";
        await Task.CompletedTask;
    }

    private bool TryGetSplitEligibleLargeDisplayPart(SpriteBrowserNode node, out PartDefinition part)
    {
        part = null!;
        if (node.AssetKind != SpriteAssetKind.PartCompositePreview)
        {
            return false;
        }

        part = GetRequiredPartDefinition(node.PrimaryId);
        return part.Kind is PartKind.RightArm or PartKind.LeftArm;
    }

    private bool TryGetSharedDescriptorSplitTarget(SpriteBrowserNode node, out int partId, out int componentIndex, out int descriptorId)
    {
        partId = -1;
        componentIndex = -1;
        descriptorId = -1;
        if (node.AssetKind != SpriteAssetKind.PartCompositeDescriptorPiece || !node.CanSplitSharedDescriptor)
        {
            return false;
        }

        partId = node.PrimaryId;
        componentIndex = node.SecondaryId;
        descriptorId = node.TertiaryId;
        return true;
    }

    private void RefreshSpriteTreeForLargeDisplayLayoutChange(int partId, int preferredSecondaryId)
    {
        var states = CaptureSpriteNodeExpansionStates(_allSpriteNodes);
        _spritePreviewCache.Clear();
        _allSpriteNodes.Clear();
        _allSpriteNodes.AddRange(BuildSpriteTreeNodes());
        RestoreSpriteNodeExpansionStates(_allSpriteNodes, states);
        RefreshSpriteFilter();
        RestoreSpriteNodeExpansionStates(_visibleSpriteNodes, states);

        var selection = FindSpriteNode(_visibleSpriteNodes, node =>
                node.AssetKind == SpriteAssetKind.PartCompositePreview &&
                node.PrimaryId == partId &&
                node.SecondaryId == preferredSecondaryId)
            ?? FindSpriteNode(_visibleSpriteNodes, node =>
                node.AssetKind == SpriteAssetKind.PartCompositePreview &&
                node.PrimaryId == partId);

        if (selection is not null)
        {
            OnSpriteSelectionChanged(SpriteTreeView, new RoutedPropertyChangedEventArgs<object>(selection, selection));
        }
    }

    private static SpriteBrowserNode? FindSpriteNode(IEnumerable<SpriteBrowserNode> nodes, Func<SpriteBrowserNode, bool> predicate)
    {
        foreach (var node in nodes)
        {
            if (predicate(node))
            {
                return node;
            }

            var child = FindSpriteNode(node.Children, predicate);
            if (child is not null)
            {
                return child;
            }
        }

        return null;
    }

    private static Dictionary<string, bool> CaptureSpriteNodeExpansionStates(IEnumerable<SpriteBrowserNode> nodes)
    {
        var states = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            states[GetSpriteNodeExpansionKey(node)] = node.IsExpanded;
            foreach (var entry in CaptureSpriteNodeExpansionStates(node.Children))
            {
                states[entry.Key] = entry.Value;
            }
        }

        return states;
    }

    private static void RestoreSpriteNodeExpansionStates(IEnumerable<SpriteBrowserNode> nodes, IReadOnlyDictionary<string, bool> states)
    {
        foreach (var node in nodes)
        {
            if (states.TryGetValue(GetSpriteNodeExpansionKey(node), out var isExpanded))
            {
                node.IsExpanded = isExpanded;
            }

            RestoreSpriteNodeExpansionStates(node.Children, states);
        }
    }

    private static string GetSpriteNodeExpansionKey(SpriteBrowserNode node) =>
        $"{node.AssetKind}:{node.PrimaryId}:{node.SecondaryId}:{node.TertiaryId}:{node.DataOffset}:{node.Title}";

    private static IEnumerable<SpriteBrowserNode> FilterSpriteNodes(IEnumerable<SpriteBrowserNode> nodes, string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return nodes;
        }

        var trimmed = filter.Trim();
        var filtered = new List<SpriteBrowserNode>();
        foreach (var node in nodes)
        {
            if (node.IsAsset)
            {
                if (node.FilterText.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
                    node.Title.Contains(trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    filtered.Add(node);
                }

                continue;
            }

            var matchingChildren = node.Children
                .SelectMany(child => FilterSpriteNodes(new[] { child }, trimmed))
                .ToList();
            if (matchingChildren.Count == 0)
            {
                continue;
            }

            var clone = new SpriteBrowserNode
            {
                Title = node.Title,
                FilterText = node.FilterText
            };
            foreach (var child in matchingChildren)
            {
                clone.Children.Add(child);
            }

            filtered.Add(clone);
        }

        return filtered;
    }

    private async void OnSpriteSelectionChanged(object? sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (_session is null)
        {
            return;
        }

        if (e.NewValue is not SpriteBrowserNode node || !node.IsAsset)
        {
            _selectedSpriteNode = null;
            if (e.NewValue is not SpriteBrowserNode)
            {
                ClearSpritePreview();
            }

            return;
        }

        try
        {
            _selectedSpriteNode = node;
            var preview = GetOrBuildSpritePreviewState(node);
            SpritePreviewImage.Source = preview.Bitmap;
            UpdateSpritePreviewLayout(preview.Bitmap.PixelWidth, preview.Bitmap.PixelHeight);
            SpriteSummaryLabel.Text = preview.Summary;
            SpritePaletteSummaryLabel.Text = preview.PaletteSummary;
            SpritePaletteItemsControl.ItemsSource = preview.Swatches;
            SpritePatchStatusLabel.Text = GetSpritePatchStatusText(node);
            UpdateSelectedPaletteSwatch();
            UpdateSpritePaletteFamilyEditor(node);
            UpdateBotBattleFacingEditor(node);
            UpdateParsedDescriptorEditor(node);
            UpdateLargeBotLivePreview(node);
            UpdateSpriteGridOverlay(preview.Bitmap.PixelWidth, preview.Bitmap.PixelHeight);
        }
        catch (Exception ex)
        {
            ClearSpritePreview();
            await DisplayAlertAsync("Sprite Load Failed", ex.Message, "OK");
        }
    }

    private SpritePreviewState GetOrBuildSpritePreviewState(SpriteBrowserNode node)
    {
        var cacheKey = GetSpritePreviewCacheKey(node);
        if (_spritePreviewCache.TryGetValue(cacheKey, out var preview))
        {
            return preview;
        }

        preview = node.AssetKind switch
        {
            SpriteAssetKind.OverworldEventObject => BuildOverworldSpritePreviewState(GetPreviewOverworldSpriteAsset(node.PrimaryId)),
            SpriteAssetKind.Portrait => BuildPortraitPreviewState(GetPreviewPortraitAsset(node.PrimaryId, node.SecondaryId)),
            SpriteAssetKind.MapTileset => BuildMapTilesetPreviewState(GetCurrentMapTilesetAsset(node.PrimaryId)),
            SpriteAssetKind.BattleCompositePartComponent => BuildBattleCompositeComponentPreviewState(GetPreviewBattleCompositeComponentAsset(node.PrimaryId, node.SecondaryId)),
            SpriteAssetKind.MedabotLargePreview => BuildMedabotLargePreviewState(node.PrimaryId),
            SpriteAssetKind.MedabotBattlePreview => BuildMedabotBattlePreviewState(node.PrimaryId),
            SpriteAssetKind.PartCompositePreview => BuildPartCompositePreviewState(GetRequiredPartDefinition(node.PrimaryId), node.SecondaryId),
            SpriteAssetKind.PartCompositeDescriptorPiece => BuildPartCompositeDescriptorPreviewState(GetRequiredPartDefinition(node.PrimaryId), node.SecondaryId, node.TertiaryId),
            SpriteAssetKind.PartCompositeParsedDescriptor => BuildPartCompositeDescriptorPreviewState(GetRequiredPartDefinition(node.PrimaryId), node.SecondaryId, node.TertiaryId),
            SpriteAssetKind.PartCompositeEditableSprite => BuildEditableLargeDisplaySpritePreviewState(node),
            _ => throw new InvalidOperationException("Unsupported sprite asset kind.")
        };

        _spritePreviewCache[cacheKey] = preview;
        return preview;
    }

    private string GetSpritePreviewCacheKey(SpriteBrowserNode node) =>
        $"{node.AssetKind}:{node.PrimaryId}:{node.SecondaryId}:{node.TertiaryId}:{node.DataOffset}:{node.SharedSourcePrimaryId}:{(node.AssetKind == SpriteAssetKind.MedabotBattlePreview ? _selectedBotBattleFacing : 0)}";

    private static SpritePreviewState BuildOverworldSpritePreviewState(SpriteAsset asset)
    {
        var swatches = BuildPaletteSwatches(asset.Image.PaletteBytes);
        var bitmap = CreateBitmapSource(asset.Image.PixelIndices, 2, swatches);
        var summary = $"Overworld sheet {asset.SpriteId:D3}{Environment.NewLine}Image: {bitmap.PixelWidth}x{bitmap.PixelHeight}px{Environment.NewLine}Layout: tile width 2 (16px){Environment.NewLine}Image pointer entry: 0x{asset.ImagePointerOffset:X6} -> 0x{asset.ImageOffset:X6}{Environment.NewLine}Palette pointer entry: 0x{asset.PalettePointerOffset:X6} -> 0x{asset.PaletteOffset:X6}";
        var paletteSummary = $"Palette colors: {swatches.Count}  |  Format: GBA BGR555";
        return new SpritePreviewState(asset.SpriteId, bitmap, summary, paletteSummary, swatches);
    }

    private static SpritePreviewState BuildPortraitPreviewState(PortraitAsset asset)
    {
        var swatches = BuildPaletteSwatches(asset.Image.PaletteBytes);
        var bitmap = CreateBitmapSource(asset.Image.PixelIndices, asset.Image.TileWidth, swatches);
        var summary = $"Portrait {asset.CharacterId:D3}:{asset.PortraitIndex}{Environment.NewLine}Image: {asset.Image.Width}x{asset.Image.Height}px{Environment.NewLine}Image pointer entry: 0x{asset.ImagePointerOffset:X6} -> 0x{asset.ImageOffset:X6}{Environment.NewLine}Palette pointer entry: 0x{asset.PalettePointerOffset:X6} -> 0x{asset.PaletteOffset:X6}";
        var paletteSummary = $"Palette colors: {swatches.Count}  |  Format: GBA BGR555";
        return new SpritePreviewState(asset.CharacterId, bitmap, summary, paletteSummary, swatches);
    }

    private static SpritePreviewState BuildMapTilesetPreviewState(MapTilesetAsset asset)
    {
        var swatches = BuildPaletteSwatches(asset.TilesetSheet.PaletteBytes);
        var bitmap = CreateBitmapSource(asset.TilesetSheet.PixelIndices, asset.TilesetSheet.TileWidth, swatches);
        var summary = $"Map tileset for map {asset.MapId:D3}  {asset.Name}{Environment.NewLine}Tileset sheet: {bitmap.PixelWidth}x{bitmap.PixelHeight}px{Environment.NewLine}Graphics pointer entry: 0x{asset.GraphicsPointerOffset:X6} -> 0x{asset.GraphicsDataOffset:X6}{Environment.NewLine}Palette pointer entry: 0x{asset.PalettePointerOffset:X6} -> 0x{asset.PaletteDataOffset:X6}{Environment.NewLine}Color attribute pointer entry: 0x{asset.ColorAttributePointerOffset:X6} -> {(asset.ColorAttributeDataOffset >= 0 ? $"0x{asset.ColorAttributeDataOffset:X6}" : "none")}";
        var paletteSummary = $"Palette colors: {swatches.Count}  |  Tiles: {asset.TilesetSheet.PixelIndices.Length / 64}";
        return new SpritePreviewState(asset.MapId, bitmap, summary, paletteSummary, swatches);
    }

    private static SpritePreviewState BuildBattleCompositeComponentPreviewState(BattleCompositeSpriteComponentAsset asset)
    {
        var swatches = BuildPaletteSwatches(asset.Image.PaletteBytes);
        var bitmap = CreateBitmapSource(asset.Image.PixelIndices, asset.Image.TileWidth, swatches);
        var componentName = PartSpriteDisplayLayout.GetBattleCompositeComponentNames()[asset.ComponentIndex];
        var summary = $"Battle composite Medabot {asset.MedabotId:D3} / {componentName}{Environment.NewLine}Image: {asset.Image.Width}x{asset.Image.Height}px{Environment.NewLine}Image pointer entry: 0x{asset.ImagePointerOffset:X6} -> 0x{asset.ImageOffset:X6}{Environment.NewLine}Palette family entry: 0x{asset.PalettePointerOffset:X6}  |  Family: {asset.PaletteFamily}  |  Palette bank: {asset.PaletteSelector}{Environment.NewLine}Palette data: 0x{asset.PaletteOffset:X6}";
        var paletteSummary = $"Palette colors: {swatches.Count}  |  Component palette bank {asset.PaletteSelector}";
        return new SpritePreviewState(asset.MedabotId, bitmap, summary, paletteSummary, swatches);
    }

    private SpritePreviewState BuildMedabotLargePreviewState(int medabotId)
    {
        var preview = CreateMedabotLargePreviewStripBitmap(medabotId);
        var summary =
            $"Medabot {medabotId:D3}  {_metadata.GetBotName(medabotId)}{Environment.NewLine}" +
            $"Large bot preview uses the combined battle-preview descriptor path traced from `FUN_08046d1c`; draft/staged part sprite edits are overlaid onto that ROM-derived placement/order.{Environment.NewLine}" +
            "Left frame: game side 0  |  Right frame: game side 1";
        var paletteSummary = "Palette colors: combined large-preview OBJ palette view from the game renderer path.";
        return new SpritePreviewState(medabotId, preview.Bitmap, summary, paletteSummary, preview.Swatches);
    }

    private (BitmapSource Bitmap, IReadOnlyList<PaletteSwatchItem> Swatches) CreateMedabotLargePreviewStripBitmap(int medabotId)
    {
        var previewRight = CreateMedabotLargePreviewFrameBitmap(medabotId, 0)
            ?? CreateLegacyLargePreviewVariantBitmap(new[]
            {
                (Part: GetRequiredPartForMedabot(medabotId, PartKind.Head), ComponentIndex: 0, AnchorX: 0x43, AnchorY: 0x10),
                (Part: GetRequiredPartForMedabot(medabotId, PartKind.RightArm), ComponentIndex: 1, AnchorX: 0x18, AnchorY: 0x28),
                (Part: GetRequiredPartForMedabot(medabotId, PartKind.LeftArm), ComponentIndex: 3, AnchorX: 0x38, AnchorY: 0x28),
                (Part: GetRequiredPartForMedabot(medabotId, PartKind.Legs), ComponentIndex: 5, AnchorX: 0x19, AnchorY: 0x33)
            }, false);
        var previewLeft = CreateMedabotLargePreviewFrameBitmap(medabotId, 1)
            ?? CreateLegacyLargePreviewVariantBitmap(new[]
            {
                (Part: GetRequiredPartForMedabot(medabotId, PartKind.Head), ComponentIndex: 0, AnchorX: 0x43, AnchorY: 0x10),
                (Part: GetRequiredPartForMedabot(medabotId, PartKind.RightArm), ComponentIndex: 2, AnchorX: 0x18, AnchorY: 0x28),
                (Part: GetRequiredPartForMedabot(medabotId, PartKind.LeftArm), ComponentIndex: 4, AnchorX: 0x38, AnchorY: 0x28),
                (Part: GetRequiredPartForMedabot(medabotId, PartKind.Legs), ComponentIndex: 5, AnchorX: 0x19, AnchorY: 0x33)
            }, true);
        var bitmap = CreateBitmapStrip(previewRight.Bitmap, previewLeft.Bitmap, 24);
        var swatches = previewRight.Swatches.Count != 0 ? previewRight.Swatches : previewLeft.Swatches;
        return (bitmap, swatches);
    }

    private SpritePreviewState BuildMedabotBattlePreviewState(int medabotId)
    {
        var headBase = GetPreviewBattleCompositeComponentAsset(medabotId, 0);
        var rightArmA = GetPreviewBattleCompositeComponentAsset(medabotId, 1);
        var rightArmB = GetPreviewBattleCompositeComponentAsset(medabotId, 2);
        var leftArmA = GetPreviewBattleCompositeComponentAsset(medabotId, 3);
        var leftArmB = GetPreviewBattleCompositeComponentAsset(medabotId, 4);
        var legs = GetPreviewBattleCompositeComponentAsset(medabotId, 5);
        var swatches = BuildPaletteSwatches(headBase.Image.PaletteBytes);
        var bitmap = CreateCompositeBattlePreviewBitmap(headBase, rightArmA, rightArmB, leftArmA, leftArmB, legs, swatches);
        if (_selectedBotBattleFacing != 0)
        {
            bitmap = MirrorBitmapHorizontally(bitmap);
        }

        var facingLabel = _selectedBotBattleFacing == 0 ? "Facing Right / Default" : "Facing Left / Mirrored";
        var summary =
            $"Medabot {medabotId:D3}  {_metadata.GetBotName(medabotId)}{Environment.NewLine}" +
            $"Battle preview composed from the 6 battle component sprites used by the assembled combat Medabot object path.{Environment.NewLine}" +
            $"Facing: {facingLabel}";
        var paletteSummary = $"Palette colors: {swatches.Count}  |  Battle composite family palette";
        return new SpritePreviewState(medabotId, bitmap, summary, paletteSummary, swatches);
    }

    private SpritePreviewState BuildPartCompositePreviewState(PartDefinition part, int variantComponentIndex)
    {
        var asset = GetPreviewLargePartDisplayAsset(part.Id, variantComponentIndex);
        var renderedPieces = BuildRenderedLargeDisplayPiecesForPreview(part, variantComponentIndex, asset);
        var finalBanks = GetFinalLargeDisplayPaletteBankMap(asset);
        var paletteBytes = ResolveDisplayedLargeDisplayPalette(asset, finalBanks);
        var swatches = BuildPaletteSwatches(paletteBytes);
        var bitmap = CreateLargePartDisplayBitmap(renderedPieces, finalBanks);
        var summary = $"Part {part.Id:D3}  {_metadata.GetPartName(part.Id)}{Environment.NewLine}Kind: {PartSpriteDisplayLayout.FormatPartKind(part.Kind)}{Environment.NewLine}Variant: {PartSpriteDisplayLayout.GetLargeDisplayVariantLabel(part.Kind, variantComponentIndex)}{Environment.NewLine}Medabot family: {part.MedabotId:D3}  {_metadata.GetBotName(part.MedabotId)}{Environment.NewLine}Large display: {bitmap.PixelWidth}x{bitmap.PixelHeight}px{Environment.NewLine}Root descriptor: {asset.RootDescriptorId:D2} @ 0x{asset.RootRecordOffset:X6}{Environment.NewLine}Pieces: {asset.Pieces.Count}{Environment.NewLine}First piece palette: 0x{asset.Pieces[0].PaletteOffset:X6}  |  Bank: {asset.Pieces[0].PaletteBank + 8}";
        var paletteSummary = $"Palette colors: {swatches.Count}  |  Large display uses staged OBJ palette banks from descriptor-selected pieces";
        var pieces = renderedPieces.Select((entry, index) => new SpritePreviewPiece(index, entry.X, entry.Y, entry.Image)).ToArray();
        return new SpritePreviewState(part.Id, bitmap, summary, paletteSummary, swatches, pieces);
    }

    private SpritePreviewState BuildPartCompositeDescriptorPreviewState(PartDefinition part, int variantComponentIndex, int descriptorId)
    {
        var asset = GetPreviewLargePartDisplayAsset(part.Id, variantComponentIndex);
        var piece = asset.Pieces.FirstOrDefault(entry => entry.DescriptorId == descriptorId);
        if (piece is null)
        {
            var record = _imageAssetRepository.ReadLargePartDisplayDescriptorRecords(_session!.RomFile, part, PartSpriteDisplayLayout.GetLargeDisplayVariantSelectorForComponent(part.Kind, variantComponentIndex))
                .FirstOrDefault(entry => entry.DescriptorId == descriptorId)
                ?? throw new InvalidOperationException($"Descriptor {descriptorId} is not present in large display part {part.Id}.");
            var appearanceInfo = BuildLargeDisplayAppearanceInfo(record);
            var swatchesWithoutSprite = BuildPaletteSwatches(new byte[ImageAssetRepository.PaletteSize]);
            var bitmapWithoutSprite = CreateBitmapSource(new byte[64], 1, swatchesWithoutSprite);
            var summaryWithoutSprite =
                $"Part {part.Id:D3}  {_metadata.GetPartName(part.Id)}{Environment.NewLine}" +
                $"Kind: {PartSpriteDisplayLayout.FormatPartKind(part.Kind)}  |  Variant: {PartSpriteDisplayLayout.GetLargeDisplayVariantLabel(part.Kind, variantComponentIndex)}{Environment.NewLine}" +
                $"Descriptor: {record.DescriptorId:D2}{Environment.NewLine}" +
                $"Appearance entry: 0x{record.AppearanceEntryOffset:X6}{Environment.NewLine}" +
                $"{appearanceInfo}" +
                $"{BuildLargeDisplayVariantResolutionInfo(record)}{Environment.NewLine}" +
                $"Descriptor record: 0x{record.RecordOffset:X6}{Environment.NewLine}" +
                $"Descriptor pointer entry: 0x{record.DescriptorPointerOffset:X6}{Environment.NewLine}" +
                $"Blob pointer table: 0x{record.BlobPointerTableOffset:X6}{Environment.NewLine}" +
                $"Image pointer entry: {(record.ImagePointerOffset > 0 ? $"0x{record.ImagePointerOffset:X6}" : "none")}{Environment.NewLine}" +
                $"Image target: {(record.ImageOffset > 0 ? $"0x{record.ImageOffset:X6}" : "none")}{Environment.NewLine}" +
                $"Origin: ({record.X}, {record.Y})  |  Raw XY: ({record.RawX}, {record.RawY}){Environment.NewLine}" +
                $"Raw size: {record.RawWidth}x{record.RawHeight}  |  Effective: {record.EffectiveWidth}x{record.EffectiveHeight}  |  Palette bank: {record.PaletteBank + 8}{Environment.NewLine}" +
                $"Bytes 0C/0D/0E: {record.RawByte0C}, {record.RawByte0D}, {record.RawByte0E}  |  Bytes 15/16/17: {record.RawByte15}, {record.RawByte16}, {record.RawByte17}{Environment.NewLine}" +
                "No decoded sprite is attached to this descriptor for this variant.";
            return new SpritePreviewState(part.Id, bitmapWithoutSprite, summaryWithoutSprite, "Palette colors: 16  |  This descriptor currently has no decoded sprite data.", swatchesWithoutSprite);
        }

        var renderedImage = GetRenderedLargeDisplayPieceImage(piece, part.Kind, asset.Pieces.Count);
        var finalBanks = GetFinalLargeDisplayPaletteBankMap(asset);
        var sharedDisplayPalette = ResolveDisplayedLargeDisplayPalette(asset, finalBanks);
        var effectivePalette = piece.PaletteBytes.Length != 0 && !IsAllZeroPalette(piece.PaletteBytes)
            ? piece.PaletteBytes
            : sharedDisplayPalette;
        var swatches = BuildPaletteSwatches(effectivePalette);
        var bitmap = CreateBitmapSource(renderedImage.PixelIndices, renderedImage.TileWidth, swatches);
        var parsedPiece = GetRequiredBaseParsedDescriptorRecord(new SpriteBrowserNode
        {
            AssetKind = SpriteAssetKind.PartCompositeParsedDescriptor,
            PrimaryId = part.Id,
            SecondaryId = variantComponentIndex,
            TertiaryId = descriptorId,
            DataOffset = piece.RecordOffset
        });
        var appearanceInfoWithSprite = BuildLargeDisplayAppearanceInfo(parsedPiece);
        var summary =
            $"Part {part.Id:D3}  {_metadata.GetPartName(part.Id)}{Environment.NewLine}" +
            $"Kind: {PartSpriteDisplayLayout.FormatPartKind(part.Kind)}  |  Variant: {PartSpriteDisplayLayout.GetLargeDisplayVariantLabel(part.Kind, variantComponentIndex)}{Environment.NewLine}" +
            $"Descriptor: {piece.DescriptorId:D2}{Environment.NewLine}" +
            $"Appearance entry: 0x{piece.AppearanceEntryOffset:X6}{Environment.NewLine}" +
            $"{appearanceInfoWithSprite}" +
            $"{BuildLargeDisplayVariantResolutionInfo(parsedPiece)}{Environment.NewLine}" +
            $"Descriptor record: 0x{piece.RecordOffset:X6}{Environment.NewLine}" +
            $"Descriptor pointer entry: 0x{parsedPiece.DescriptorPointerOffset:X6}{Environment.NewLine}" +
            $"Blob pointer table: 0x{parsedPiece.BlobPointerTableOffset:X6}{Environment.NewLine}" +
            $"Image pointer entry: 0x{piece.ImagePointerOffset:X6} -> 0x{piece.ImageOffset:X6}{Environment.NewLine}" +
            $"Palette pointer entry: {(piece.PalettePointerOffset > 0 ? $"0x{piece.PalettePointerOffset:X6} -> 0x{piece.PaletteOffset:X6}" : "none")}{Environment.NewLine}" +
              $"Origin: ({piece.X}, {piece.Y})  |  Size: {renderedImage.Width}x{renderedImage.Height}px  |  Loaded tiles: {piece.LoadedTileCount}  |  Palette bank: {piece.PaletteBank + 8}{Environment.NewLine}" +
              $"Bytes 0C/0D/0E: {parsedPiece.RawByte0C}, {parsedPiece.RawByte0D}, {parsedPiece.RawByte0E}  |  Bytes 15/16/17: {parsedPiece.RawByte15}, {parsedPiece.RawByte16}, {parsedPiece.RawByte17}";
        var paletteSummary = $"Palette colors: {swatches.Count}  |  Editing this view changes only descriptor {piece.DescriptorId:D2} within the selected large display variant.";
        return new SpritePreviewState(part.Id, bitmap, summary, paletteSummary, swatches);
    }

    private SpritePreviewState BuildEditableLargeDisplaySpritePreviewState(SpriteBrowserNode node)
    {
        var reference = GetPreviewEditableLargeDisplaySpriteReference(node);
        var piece = ResolveEditableLargeDisplayPreviewPiece(reference);
        var renderedImage = GetRenderedLargeDisplayPieceImage(piece, reference.Part.Kind, reference.PieceCount);
        var rootPalette = ResolveRootDescriptorPalette(GetPreviewLargePartDisplayAsset(reference.Part.Id, reference.ComponentIndex));
        var palette = piece.PaletteBytes.Length != 0 && !IsAllZeroPalette(piece.PaletteBytes)
            ? piece.PaletteBytes
            : rootPalette;
        var swatches = BuildPaletteSwatches(palette);
        var bitmap = CreateBitmapSource(renderedImage.PixelIndices, renderedImage.TileWidth, swatches);
        var referenceCount = GetEditableLargeDisplaySpriteReferences(node.PrimaryId, piece.ImageOffset).Count;
        var summary =
            $"Editable sprite 0x{piece.ImageOffset:X6}{Environment.NewLine}" +
            $"Representative part: {reference.Part.Id:D3}  {_metadata.GetPartName(reference.Part.Id)}{Environment.NewLine}" +
            $"Kind: {PartSpriteDisplayLayout.FormatPartKind(reference.Part.Kind)}  |  Variant: {PartSpriteDisplayLayout.GetLargeDisplayVariantLabel(reference.Part.Kind, reference.ComponentIndex)}{Environment.NewLine}" +
            $"Descriptor: {piece.DescriptorId:D2}  |  Record: 0x{piece.RecordOffset:X6}{Environment.NewLine}" +
            $"Image pointer entry: 0x{piece.ImagePointerOffset:X6} -> 0x{piece.ImageOffset:X6}{Environment.NewLine}" +
            $"Palette pointer entry: {(piece.PalettePointerOffset > 0 ? $"0x{piece.PalettePointerOffset:X6} -> 0x{piece.PaletteOffset:X6}" : "none")}{Environment.NewLine}" +
            $"References in Medabot: {referenceCount}";
        var paletteSummary = $"Palette colors: {swatches.Count}  |  Editing this node updates all staged references to sprite 0x{piece.ImageOffset:X6} for this Medabot.";
        return new SpritePreviewState(node.PrimaryId, bitmap, summary, paletteSummary, swatches);
    }


    private static BitmapSource CreateCompositeBattlePreviewBitmap(
        BattleCompositeSpriteComponentAsset headBase,
        BattleCompositeSpriteComponentAsset rightArmA,
        BattleCompositeSpriteComponentAsset rightArmB,
        BattleCompositeSpriteComponentAsset leftArmA,
        BattleCompositeSpriteComponentAsset leftArmB,
        BattleCompositeSpriteComponentAsset legs,
        IReadOnlyList<PaletteSwatchItem> swatches)
    {
        var rightArm = CombineCompositeComponentsVertically(rightArmA.Image, rightArmB.Image);
        var leftArm = CombineCompositeComponentsVertically(leftArmA.Image, leftArmB.Image);

        var baseX = Math.Max(rightArm.Width + 8, 8);
        var baseY = 0;
        var armY = 8;
        var legsY = 4 + Math.Max(headBase.Image.Height - 8, 0);
        var rightArmX = Math.Max(0, baseX - 8);
        var leftArmX = baseX + 8;
        var legsX = baseX;

        var width = Math.Max(
            Math.Max(baseX + headBase.Image.Width, rightArmX + rightArm.Width),
            Math.Max(leftArmX + leftArm.Width, legsX + legs.Image.Width));
        var height = Math.Max(
            Math.Max(baseY + headBase.Image.Height, armY + Math.Max(rightArm.Height, leftArm.Height)),
            legsY + legs.Image.Height);

        var pixels = new byte[width * height * 4];
        BlitIndexedImage(headBase.Image, width, height, baseX, baseY, swatches, pixels);
        BlitIndexedImage(rightArm, width, height, rightArmX, armY, swatches, pixels);
        BlitIndexedImage(leftArm, width, height, leftArmX, armY, swatches, pixels);
        BlitIndexedImage(legs.Image, width, height, legsX, legsY, swatches, pixels);

        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        bitmap.Freeze();
        return bitmap;
    }

    private (BitmapSource Bitmap, IReadOnlyList<PaletteSwatchItem> Swatches)? CreateMedabotLargePreviewFrameBitmap(int medabotId, int side)
    {
        var frame = _imageAssetRepository.ReadMedabotLargeDisplayFrame(_session!.RomFile, _loadedParts, medabotId, side);
        if (frame.Pieces.Count == 0 || !ShouldRenderCombinedMedabotLargePreview(frame))
        {
            return null;
        }

        frame = ApplyPreviewLargeDisplayEditsToMedabotFrame(frame);
        var renderedPieces = frame.Pieces
            .Select(piece => (Piece: piece, Image: GetRenderedLargeDisplayPieceImage(piece, PartKind.Head, frame.Pieces.Count), X: piece.X, Y: piece.Y))
            .ToArray();
        var finalBanks = GetFinalLargeDisplayPaletteBankMap(frame.InitialPaletteBanks, frame.Pieces);
        var bitmap = CreateLargePartDisplayBitmap(renderedPieces, finalBanks);
        if (frame.MirrorFinalImageHorizontally)
        {
            bitmap = MirrorBitmapHorizontally(bitmap);
        }

        return (bitmap, BuildPaletteSwatches(ResolveDisplayedLargeDisplayPalette(frame.Pieces, finalBanks)));
    }

    private MedabotLargeDisplayFrame ApplyPreviewLargeDisplayEditsToMedabotFrame(MedabotLargeDisplayFrame frame)
    {
        var overridesByImageOffset = BuildLargeDisplayImageOverridesForMedabot(frame.MedabotId);
        Dictionary<int, LargePartDisplayPieceAsset> mirroredSideArmOverridesByDescriptor = frame.Side == 0
            ? []
            : BuildMirroredSideArmOverridesForMedabot(frame.MedabotId);
        if (overridesByImageOffset.Count == 0 && mirroredSideArmOverridesByDescriptor.Count == 0)
        {
            return frame;
        }

        var updatedPieces = frame.Pieces
            .Select(piece =>
            {
                if (mirroredSideArmOverridesByDescriptor.TryGetValue(piece.DescriptorId, out var armOverride))
                {
                    return ApplyLargeDisplayImageOverride(piece, armOverride);
                }

                return overridesByImageOffset.TryGetValue(piece.ImageOffset, out var imageOverride)
                    ? ApplyLargeDisplayImageOverride(piece, imageOverride)
                    : piece;
            })
            .ToArray();
        return frame with { Pieces = updatedPieces };
    }

    private Dictionary<int, LargePartDisplayPieceAsset> BuildMirroredSideArmOverridesForMedabot(int medabotId)
    {
        var overrides = new Dictionary<int, LargePartDisplayPieceAsset>();
        foreach (var kind in new[] { PartKind.RightArm, PartKind.LeftArm })
        {
            var part = GetRequiredPartForMedabot(medabotId, kind);
            var componentEntries = PartSpriteDisplayLayout.GetPreviewComponentEntriesForPartKind(kind);
            var componentIndex = componentEntries[Math.Min(1, componentEntries.Count - 1)].ComponentIndex;
            var asset = GetPreviewLargePartDisplayAsset(part.Id, componentIndex);
            foreach (var piece in asset.Pieces.Where(piece => piece.ImageOffset > 0))
            {
                if (piece.DescriptorId == asset.RootDescriptorId)
                {
                    continue;
                }

                overrides[piece.DescriptorId] = piece;
            }
        }

        return overrides;
    }

    private Dictionary<int, LargePartDisplayPieceAsset> BuildLargeDisplayImageOverridesForMedabot(int medabotId)
    {
        var overrides = new Dictionary<int, LargePartDisplayPieceAsset>();
        foreach (var part in _loadedParts.Where(part => part.MedabotId == medabotId).OrderBy(part => part.Kind).ThenBy(part => part.Id))
        {
            foreach (var (componentIndex, _) in PartSpriteDisplayLayout.GetPreviewComponentEntriesForPartKind(part.Kind))
            {
                var asset = TryGetDraftOrStagedLargePartDisplayAsset(part.Id, componentIndex);
                if (asset is null)
                {
                    continue;
                }

                foreach (var piece in asset.Pieces.Where(piece => piece.ImageOffset > 0))
                {
                    overrides[piece.ImageOffset] = piece;
                }
            }
        }

        return overrides;
    }

    private LargePartDisplayAsset? TryGetDraftOrStagedLargePartDisplayAsset(int partId, int componentIndex)
    {
        var part = GetRequiredPartDefinition(partId);
        var variantSelector = PartSpriteDisplayLayout.GetLargeDisplayVariantSelectorForComponent(part.Kind, componentIndex);
        if (_editedLargePartDisplayAssets.TryGetValue((partId, variantSelector), out var draft))
        {
            return draft;
        }

        return ProjectEditCollection.Find(_project, ProjectEditAdapters.LargeDisplaySprite, (partId, variantSelector));
    }

    private static LargePartDisplayPieceAsset ApplyLargeDisplayImageOverride(
        LargePartDisplayPieceAsset framePiece,
        LargePartDisplayPieceAsset imageOverride)
    {
        // The complete preview keeps the ROM-combined descriptor traversal, anchors, mirror flags,
        // and z-order. Draft/staged part edits only replace the source graphics and palette data.
        return framePiece with
        {
            PaletteBytes = imageOverride.PaletteBytes.ToArray(),
            PaletteOffset = imageOverride.PaletteOffset,
            PalettePointerOffset = imageOverride.PalettePointerOffset,
            Image = new IndexedImage(
                imageOverride.Image.TileWidth,
                imageOverride.Image.TileHeight,
                imageOverride.Image.PixelIndices.ToArray(),
                imageOverride.Image.PaletteBytes.ToArray())
        };
    }

    private static bool ShouldRenderCombinedMedabotLargePreview(MedabotLargeDisplayFrame frame)
    {
        _ = frame;
        return true;
    }

    private (BitmapSource Bitmap, IReadOnlyList<PaletteSwatchItem> Swatches) CreateLegacyLargePreviewVariantBitmap(
        IReadOnlyList<(PartDefinition Part, int ComponentIndex, int AnchorX, int AnchorY)> entries,
        bool mirrorFinalImageHorizontally)
    {
        var composedParts = new List<(BitmapSource Bitmap, int X, int Y)>(entries.Count);
        IReadOnlyList<PaletteSwatchItem>? representativeSwatches = null;

        foreach (var entry in entries)
        {
            var asset = GetPreviewLargePartDisplayAsset(entry.Part.Id, entry.ComponentIndex);
            var renderedPieces = BuildRenderedLargeDisplayPiecesForPreview(entry.Part, entry.ComponentIndex, asset);
            var finalBanks = GetFinalLargeDisplayPaletteBankMap(asset);
            var partBitmap = CreateLargePartDisplayBitmap(renderedPieces, finalBanks);
            representativeSwatches ??= BuildPaletteSwatches(ResolveDisplayedLargeDisplayPalette(asset, finalBanks));
            var minX = renderedPieces.Count == 0 ? 0 : renderedPieces.Min(piece => piece.X);
            var minY = renderedPieces.Count == 0 ? 0 : renderedPieces.Min(piece => piece.Y);
            composedParts.Add((partBitmap, entry.AnchorX + minX, entry.AnchorY + minY));
        }

        var bitmap = CreateBitmapComposition(composedParts);
        if (mirrorFinalImageHorizontally)
        {
            bitmap = MirrorBitmapHorizontally(bitmap);
        }

        return (bitmap, representativeSwatches ?? []);
    }

    private static BitmapSource CreateBitmapStrip(BitmapSource left, BitmapSource right, int gap)
    {
        var width = left.PixelWidth + gap + right.PixelWidth;
        var height = Math.Max(left.PixelHeight, right.PixelHeight);
        return CreateBitmapComposition(
        [
            (left, 0, Math.Max(0, (height - left.PixelHeight) / 2)),
            (right, left.PixelWidth + gap, Math.Max(0, (height - right.PixelHeight) / 2))
        ]);
    }

    private static BitmapSource CreateBitmapComposition(IReadOnlyList<(BitmapSource Bitmap, int X, int Y)> items)
    {
        if (items.Count == 0)
        {
            return CreateBitmapSource([], 1, []);
        }

        var minX = items.Min(entry => entry.X);
        var minY = items.Min(entry => entry.Y);
        var maxX = items.Max(entry => entry.X + entry.Bitmap.PixelWidth);
        var maxY = items.Max(entry => entry.Y + entry.Bitmap.PixelHeight);
        var width = Math.Max(1, maxX - minX);
        var height = Math.Max(1, maxY - minY);
        var pixels = new byte[width * height * 4];

        foreach (var (bitmap, x, y) in items)
        {
            var sourcePixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
            bitmap.CopyPixels(sourcePixels, bitmap.PixelWidth * 4, 0);
            BlitBgraImage(sourcePixels, bitmap.PixelWidth, bitmap.PixelHeight, width, height, x - minX, y - minY, pixels);
        }

        var output = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        output.Freeze();
        return output;
    }

    private static BitmapSource MirrorBitmapHorizontally(BitmapSource source)
    {
        var sourcePixels = new byte[source.PixelWidth * source.PixelHeight * 4];
        source.CopyPixels(sourcePixels, source.PixelWidth * 4, 0);
        var mirroredPixels = new byte[sourcePixels.Length];

        for (var y = 0; y < source.PixelHeight; y++)
        {
            for (var x = 0; x < source.PixelWidth; x++)
            {
                var sourceIndex = ((y * source.PixelWidth) + x) * 4;
                var destIndex = ((y * source.PixelWidth) + (source.PixelWidth - 1 - x)) * 4;
                Buffer.BlockCopy(sourcePixels, sourceIndex, mirroredPixels, destIndex, 4);
            }
        }

        var bitmap = BitmapSource.Create(source.PixelWidth, source.PixelHeight, 96, 96, PixelFormats.Bgra32, null, mirroredPixels, source.PixelWidth * 4);
        bitmap.Freeze();
        return bitmap;
    }

    private static IReadOnlyList<PaletteSwatchItem> BuildPaletteSwatches(byte[] paletteBytes)
    {
        var swatches = new List<PaletteSwatchItem>(paletteBytes.Length / 2);
        for (var index = 0; index + 1 < paletteBytes.Length; index += 2)
        {
            var rawColor = (ushort)(paletteBytes[index] | (paletteBytes[index + 1] << 8));
            var color = DecodeGbaColor(rawColor);
            swatches.Add(new PaletteSwatchItem
            {
                Index = index / 2,
                Color = color,
                Hex = $"#{color.R:X2}{color.G:X2}{color.B:X2}"
            });
        }

        return swatches;
    }

    private static BitmapSource CreateBitmapSource(byte[] pixelIndices, int tileWidth, IReadOnlyList<PaletteSwatchItem> swatches)
    {
        var tileCount = pixelIndices.Length / 64;
        var pixelWidth = tileWidth * 8;
        var pixelHeight = Math.Max(1, tileCount / Math.Max(1, tileWidth)) * 8;
        var pixels = new byte[pixelWidth * pixelHeight * 4];

        for (var tileIndex = 0; tileIndex < tileCount; tileIndex++)
        {
            var tileX = tileIndex % tileWidth;
            var tileY = tileIndex / tileWidth;
            BlitTile(pixelIndices, tileIndex, pixelWidth, pixelHeight, tileX * 8, tileY * 8, swatches, pixels);
        }

        var bitmap = BitmapSource.Create(pixelWidth, pixelHeight, 96.0, 96.0, PixelFormats.Bgra32, null, pixels, pixelWidth * 4);
        bitmap.Freeze();
        return bitmap;
    }

    private static BitmapSource CreateLargePartDisplayBitmap(
        IReadOnlyList<(LargePartDisplayPieceAsset Piece, IndexedImage Image, int X, int Y)> renderedPieces,
        IReadOnlyDictionary<int, byte[]> finalBanks)
    {
        if (renderedPieces.Count == 0)
        {
            return CreateBitmapSource([], 1, []);
        }

        var minX = renderedPieces.Min(entry => entry.X);
        var minY = renderedPieces.Min(entry => entry.Y);
        var maxX = renderedPieces.Max(entry => entry.X + entry.Image.Width);
        var maxY = renderedPieces.Max(entry => entry.Y + entry.Image.Height);
        var width = Math.Max(1, maxX - minX);
        var height = Math.Max(1, maxY - minY);
        var pixels = new byte[width * height * 4];
        var uploadedPalette = renderedPieces
            .Select(entry => entry.Piece.PaletteBytes)
            .FirstOrDefault(palette => palette.Length != 0 && !IsAllZeroPalette(palette));
        var stagedPalette = finalBanks.Values.FirstOrDefault(palette => !IsAllZeroPalette(palette));
        var fallbackPalette = uploadedPalette ?? stagedPalette ?? new byte[ImageAssetRepository.PaletteSize];
        byte[] currentPalette = fallbackPalette;

        foreach (var entry in renderedPieces)
        {
            var piece = entry.Piece;
            var bank = piece.PaletteBank + 8;
            var palette = ResolveEffectiveLargeDisplayPiecePalette(piece, bank, finalBanks, currentPalette, fallbackPalette);
            if (!IsAllZeroPalette(palette))
            {
                currentPalette = palette;
            }

            var pieceSwatches = BuildPaletteSwatches(palette);
            BlitIndexedImage(entry.Image, width, height, entry.X - minX, entry.Y - minY, pieceSwatches, pixels);
        }

        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        bitmap.Freeze();
        return bitmap;
    }

    private IReadOnlyList<(LargePartDisplayPieceAsset Piece, IndexedImage Image, int X, int Y)> BuildRenderedLargeDisplayPiecesForPreview(
        PartDefinition part,
        int componentIndex,
        LargePartDisplayAsset asset)
    {
        return asset.Pieces
            .Select(piece =>
            {
                var renderImage = GetRenderedLargeDisplayPieceImage(piece, part.Kind, asset.Pieces.Count);
                return (Piece: piece, Image: renderImage, X: piece.X, Y: piece.Y);
            })
            .ToArray();
    }

    private static Dictionary<int, byte[]> GetFinalLargeDisplayPaletteBankMap(LargePartDisplayAsset asset)
        => GetFinalLargeDisplayPaletteBankMap(asset.InitialPaletteBanks, asset.Pieces);

    private static Dictionary<int, byte[]> GetFinalLargeDisplayPaletteBankMap(
        IReadOnlyDictionary<int, byte[]> initialPaletteBanks,
        IReadOnlyList<LargePartDisplayPieceAsset> pieces)
    {
        var banks = initialPaletteBanks
            .Where(pair => !IsAllZeroPalette(pair.Value))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        foreach (var piece in pieces)
        {
            if (piece.PaletteBytes.Length != 0)
            {
                banks[piece.PaletteBank + 8] = piece.PaletteBytes;
            }
        }

        return banks;
    }

    private static byte[] ResolveEffectiveLargeDisplayPiecePalette(
        LargePartDisplayPieceAsset piece,
        int bank,
        IReadOnlyDictionary<int, byte[]> finalBanks,
        byte[] currentPalette,
        byte[] fallbackPalette)
    {
        if (piece.PaletteBytes.Length != 0 && !IsAllZeroPalette(piece.PaletteBytes))
        {
            return piece.PaletteBytes;
        }

        if (finalBanks.TryGetValue(bank, out var bankPalette) && !IsAllZeroPalette(bankPalette))
        {
            return bankPalette;
        }

        if (!IsAllZeroPalette(currentPalette))
        {
            return currentPalette;
        }

        return fallbackPalette;
    }

    private static byte[] ResolveDisplayedLargeDisplayPalette(
        LargePartDisplayAsset asset,
        IReadOnlyDictionary<int, byte[]> finalBanks)
        => ResolveDisplayedLargeDisplayPalette(asset.Pieces, finalBanks);

    private static byte[] ResolveDisplayedLargeDisplayPalette(
        IReadOnlyList<LargePartDisplayPieceAsset> pieces,
        IReadOnlyDictionary<int, byte[]> finalBanks)
    {
        var uploadedPalette = pieces
            .Select(piece => piece.PaletteBytes)
            .FirstOrDefault(palette => palette.Length != 0 && !IsAllZeroPalette(palette));
        if (uploadedPalette is not null)
        {
            return uploadedPalette;
        }

        var stagedPalette = finalBanks.Values.FirstOrDefault(palette => !IsAllZeroPalette(palette));
        if (stagedPalette is not null)
        {
            return stagedPalette;
        }

        return new byte[ImageAssetRepository.PaletteSize];
    }

    private static bool IsAllZeroPalette(IReadOnlyList<byte> palette)
    {
        foreach (var value in palette)
        {
            if (value != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static IndexedImage GetRenderedLargeDisplayPieceImage(
        LargePartDisplayPieceAsset piece,
        PartKind kind,
        int pieceCount)
    {
        var totalTiles = Math.Max(1, piece.LoadedTileCount);
        var widthDivisor = Math.Max(1, piece.SizeDivisors & 0x0F);
        var heightDivisor = Math.Max(1, piece.SizeDivisors >> 4);
        var effectiveWidth = Math.Max(8, piece.RawWidth / widthDivisor);
        var effectiveHeight = Math.Max(8, piece.RawHeight / heightDivisor);
        var descriptorTileWidth = Math.Max(1, effectiveWidth / 8);
        var descriptorTileHeight = Math.Max(1, effectiveHeight / 8);
        var tileWidth = piece.RawWidth > 0 ? descriptorTileWidth : Math.Max(1, piece.Image.TileWidth);
        var tileHeight = piece.RawHeight > 0 ? descriptorTileHeight : Math.Max(1, piece.Image.TileHeight);
        var effectivePixels = new byte[tileWidth * tileHeight * 64];
        Array.Copy(piece.Image.PixelIndices, effectivePixels, Math.Min(totalTiles * 64, piece.Image.PixelIndices.Length));
        var rendered = new IndexedImage(tileWidth, tileHeight, effectivePixels, piece.Image.PaletteBytes);
        return piece.MirrorDisplayHorizontally ? MirrorIndexedImageHorizontally(rendered) : rendered;
    }

    private static IndexedImage MirrorIndexedImageHorizontally(IndexedImage source)
    {
        var mirrored = new byte[source.PixelIndices.Length];
        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                var sourceIndex = GetTileOrderedPixelIndex(source, x, y);
                var destIndex = GetTileOrderedPixelIndex(source, source.Width - 1 - x, y);
                mirrored[destIndex] = source.PixelIndices[sourceIndex];
            }
        }

        return new IndexedImage(source.TileWidth, source.TileHeight, mirrored, source.PaletteBytes);
    }


    private static IndexedImage CombineCompositeComponentsVertically(IndexedImage top, IndexedImage bottom)
    {
        var tileWidth = Math.Max(top.TileWidth, bottom.TileWidth);
        var tileHeight = top.TileHeight + bottom.TileHeight;
        var pixels = new byte[tileWidth * tileHeight * 64];

        for (var y = 0; y < top.Height; y++)
        {
            for (var x = 0; x < top.Width; x++)
            {
                var sourceIndex = GetTileOrderedPixelIndex(top, x, y);
                var destIndex = GetTileOrderedPixelIndex(new IndexedImage(tileWidth, tileHeight, pixels, Array.Empty<byte>()), x, y);
                pixels[destIndex] = top.PixelIndices[sourceIndex];
            }
        }

        var offsetY = top.Height;
        for (var y = 0; y < bottom.Height; y++)
        {
            for (var x = 0; x < bottom.Width; x++)
            {
                var sourceIndex = GetTileOrderedPixelIndex(bottom, x, y);
                var destIndex = GetTileOrderedPixelIndex(new IndexedImage(tileWidth, tileHeight, pixels, Array.Empty<byte>()), x, y + offsetY);
                pixels[destIndex] = bottom.PixelIndices[sourceIndex];
            }
        }

        return new IndexedImage(tileWidth, tileHeight, pixels, top.PaletteBytes);
    }

    private static void BlitIndexedImage(IndexedImage image, int bitmapWidth, int bitmapHeight, int destX, int destY, IReadOnlyList<PaletteSwatchItem> swatches, byte[] output)
    {
        for (var tileY = 0; tileY < image.TileHeight; tileY++)
        {
            for (var tileX = 0; tileX < image.TileWidth; tileX++)
            {
                var tileIndex = (tileY * image.TileWidth) + tileX;
                BlitTile(image.PixelIndices, tileIndex, bitmapWidth, bitmapHeight, destX + (tileX * 8), destY + (tileY * 8), swatches, output);
            }
        }
    }

    private static void BlitBgraImage(byte[] sourcePixels, int sourceWidth, int sourceHeight, int bitmapWidth, int bitmapHeight, int destX, int destY, byte[] output)
    {
        for (var y = 0; y < sourceHeight; y++)
        {
            var targetY = destY + y;
            if (targetY < 0 || targetY >= bitmapHeight)
            {
                continue;
            }

            for (var x = 0; x < sourceWidth; x++)
            {
                var targetX = destX + x;
                if (targetX < 0 || targetX >= bitmapWidth)
                {
                    continue;
                }

                var sourceIndex = ((y * sourceWidth) + x) * 4;
                var alpha = sourcePixels[sourceIndex + 3];
                if (alpha == 0)
                {
                    continue;
                }

                var outputIndex = ((targetY * bitmapWidth) + targetX) * 4;
                output[outputIndex] = sourcePixels[sourceIndex];
                output[outputIndex + 1] = sourcePixels[sourceIndex + 1];
                output[outputIndex + 2] = sourcePixels[sourceIndex + 2];
                output[outputIndex + 3] = alpha;
            }
        }
    }

    private static void BlitTile(byte[] pixelIndices, int tileIndex, int bitmapWidth, int bitmapHeight, int destX, int destY, IReadOnlyList<PaletteSwatchItem> swatches, byte[] output)
    {
        var tileBase = tileIndex * 64;
        for (var localY = 0; localY < 8; localY++)
        {
            for (var localX = 0; localX < 8; localX++)
            {
                var sourceIndex = tileBase + (localY * 8) + localX;
                if (sourceIndex >= pixelIndices.Length)
                {
                    return;
                }

                var pixelX = destX + localX;
                var pixelY = destY + localY;
                if (pixelX >= bitmapWidth || pixelY >= bitmapHeight)
                {
                    continue;
                }

                var colorIndex = pixelIndices[sourceIndex];
                if (colorIndex == 0)
                {
                    continue;
                }

                var color = colorIndex < swatches.Count ? swatches[colorIndex].Color : Colors.Transparent;
                var outputIndex = ((pixelY * bitmapWidth) + pixelX) * 4;
                output[outputIndex + 0] = color.B;
                output[outputIndex + 1] = color.G;
                output[outputIndex + 2] = color.R;
                output[outputIndex + 3] = 255;
            }
        }
    }

    private static WpfColor DecodeGbaColor(ushort rawColor)
    {
        static byte Expand5To8(int value) => (byte)((value << 3) | (value >> 2));

        var red = Expand5To8(rawColor & 0x1F);
        var green = Expand5To8((rawColor >> 5) & 0x1F);
        var blue = Expand5To8((rawColor >> 10) & 0x1F);
        return WpfColor.FromRgb(red, green, blue);
    }

    private static ushort EncodeGbaColor(WpfColor color)
    {
        static ushort Compress8To5(byte value) => (ushort)(value >> 3);

        var red = Compress8To5(color.R);
        var green = Compress8To5(color.G);
        var blue = Compress8To5(color.B);
        return (ushort)(red | (green << 5) | (blue << 10));
    }

    private SpriteAsset GetCurrentOverworldSpriteAsset(int spriteId)
    {
        var staged = ProjectEditCollection.Find(_project, ProjectEditAdapters.OverworldSprite, spriteId);
        if (staged is not null)
        {
            return staged;
        }

        if (_session is null)
        {
            throw new InvalidOperationException("No ROM session is open.");
        }

        return _imageAssetRepository.ReadSprite(_session.RomFile, spriteId);
    }

    private MapTilesetAsset GetCurrentMapTilesetAsset(int mapId)
    {
        if (_mapTilesetCache.TryGetValue(mapId, out var cached))
        {
            return cached;
        }

        if (_session is null)
        {
            throw new InvalidOperationException("Open a ROM before reading map tilesets.");
        }

        var asset = _mapTilesetRepository.ReadMap(_session.RomFile, mapId, _metadata.GetMapName(mapId));
        _mapTilesetCache[mapId] = asset;
        return asset;
    }

    private PortraitAsset GetCurrentPortraitAsset(int characterId, int portraitIndex)
    {
        var staged = ProjectEditCollection.Find(_project, ProjectEditAdapters.Portrait, (characterId, portraitIndex));
        if (staged is not null)
        {
            return staged;
        }

        if (_session is null)
        {
            throw new InvalidOperationException("No ROM session is open.");
        }

        return _imageAssetRepository.ReadPortrait(_session.RomFile, characterId, portraitIndex);
    }

    private BattleCompositeSpriteComponentAsset GetCurrentBattleCompositeComponentAsset(int medabotId, int componentIndex)
    {
        var staged = ProjectEditCollection.Find(_project, ProjectEditAdapters.BattleCompositeSprite, (medabotId, componentIndex));
        if (staged is not null)
        {
            return staged;
        }

        if (_battleCompositeComponentCache.TryGetValue((medabotId, componentIndex), out var cached))
        {
            return cached;
        }

        if (_session is null)
        {
            throw new InvalidOperationException("No ROM session is open.");
        }

        var asset = _imageAssetRepository.ReadBattleCompositeSpriteComponent(_session.RomFile, medabotId, componentIndex);
        _battleCompositeComponentCache[(medabotId, componentIndex)] = asset;
        return asset;
    }

    private BattleCompositeSpriteComponentAsset GetEditableBattleCompositeComponentAsset(int medabotId, int componentIndex)
    {
        if (_editedBattleCompositeComponentAssets.TryGetValue((medabotId, componentIndex), out var edited))
        {
            return edited;
        }

        var current = GetCurrentBattleCompositeComponentAsset(medabotId, componentIndex);
        var clone = current with
        {
            Image = new IndexedImage(current.Image.TileWidth, current.Image.TileHeight, current.Image.PixelIndices.ToArray(), current.Image.PaletteBytes.ToArray())
        };
        _editedBattleCompositeComponentAssets[(medabotId, componentIndex)] = clone;
        return clone;
    }

    private LargePartDisplayAsset GetCurrentLargePartDisplayAsset(int partId, int componentIndex)
    {
        var part = GetRequiredPartDefinition(partId);
        var variantSelector = PartSpriteDisplayLayout.GetLargeDisplayVariantSelectorForComponent(part.Kind, componentIndex);
        if (_largePartDisplayAssetCache.TryGetValue((partId, variantSelector), out var cached))
        {
            var stagedCached = ProjectEditCollection.Find(_project, ProjectEditAdapters.LargeDisplaySprite, (partId, variantSelector));
            return stagedCached is null ? cached : MergeLargeDisplayAssets(cached, stagedCached);
        }

        if (_session is null)
        {
            throw new InvalidOperationException("No ROM session is open.");
        }

        var asset = _imageAssetRepository.ReadLargePartDisplay(_session.RomFile, part, variantSelector);
        _largePartDisplayAssetCache[(partId, variantSelector)] = asset;
        var staged = ProjectEditCollection.Find(_project, ProjectEditAdapters.LargeDisplaySprite, (partId, variantSelector));
        return staged is null ? asset : MergeLargeDisplayAssets(asset, staged);
    }

    private LargePartDisplayAsset GetEditableLargePartDisplayAsset(int partId, int componentIndex)
    {
        var part = GetRequiredPartDefinition(partId);
        var variantSelector = PartSpriteDisplayLayout.GetLargeDisplayVariantSelectorForComponent(part.Kind, componentIndex);
        if (_editedLargePartDisplayAssets.TryGetValue((partId, variantSelector), out var edited))
        {
            return edited;
        }

        var current = GetCurrentLargePartDisplayAsset(partId, componentIndex);
        var clone = current with
        {
            Pieces = current.Pieces
                .Select(piece => piece with
                {
                    PaletteBytes = piece.PaletteBytes.ToArray(),
                    Image = new IndexedImage(piece.Image.TileWidth, piece.Image.TileHeight, piece.Image.PixelIndices.ToArray(), piece.Image.PaletteBytes.ToArray())
                })
                .ToArray(),
            InitialPaletteBanks = current.InitialPaletteBanks.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray())
        };
        _editedLargePartDisplayAssets[(partId, variantSelector)] = clone;
        return clone;
    }

    private PartDefinition GetRequiredPartDefinition(int partId)
    {
        if (partId >= 0 && partId < _loadedParts.Count)
        {
            return _loadedParts[partId];
        }

        var part = _loadedParts.FirstOrDefault(candidate => candidate.Id == partId);
        if (part is not null)
        {
            return part;
        }

        throw new InvalidOperationException($"Could not resolve part definition {partId}.");
    }

    private string GetSpritePatchStatusText(SpriteBrowserNode node)
    {
        return node.AssetKind switch
        {
            SpriteAssetKind.OverworldEventObject when _editedOverworldSpriteAssets.ContainsKey(node.PrimaryId)
                => HasStagedOverworldSprite(node.PrimaryId)
                    ? "Status: this asset has draft edits and an older staged version. Stage Changes to update the staged version."
                    : "Status: this asset has draft edits. Stage Changes to make other tabs and export use them.",
            SpriteAssetKind.Portrait when _editedPortraitAssets.ContainsKey((node.PrimaryId, node.SecondaryId))
                => HasStagedPortrait(node.PrimaryId, node.SecondaryId)
                    ? "Status: this asset has draft edits and an older staged version. Stage Changes to update the staged version."
                    : "Status: this asset has draft edits. Stage Changes to make other tabs and export use them.",
            SpriteAssetKind.BattleCompositePartComponent when _editedBattleCompositeComponentAssets.ContainsKey((node.PrimaryId, node.SecondaryId))
                => HasStagedBattleCompositeComponent(node.PrimaryId, node.SecondaryId)
                    ? "Status: this asset has draft edits and an older staged version. Stage Changes to update the staged version."
                    : "Status: this asset has draft edits. Stage Changes to make other tabs and export use them.",
            SpriteAssetKind.PartCompositeDescriptorPiece when _editedLargePartDisplayAssets.ContainsKey((node.PrimaryId, PartSpriteDisplayLayout.GetLargeDisplayVariantSelectorForComponent(GetRequiredPartDefinition(node.PrimaryId).Kind, node.SecondaryId)))
                => HasStagedLargeDisplay(node.PrimaryId, PartSpriteDisplayLayout.GetLargeDisplayVariantSelectorForComponent(GetRequiredPartDefinition(node.PrimaryId).Kind, node.SecondaryId))
                    ? "Status: this asset has draft edits and an older staged version. Stage Changes to update the staged version."
                    : "Status: this asset has draft edits. Stage Changes to make other tabs and export use them.",
            SpriteAssetKind.PartCompositeEditableSprite
                => "Status: editing a unique large-display sprite blob. Drawing changes update every descriptor in this Medabot that points at the same sprite data.",
            SpriteAssetKind.OverworldEventObject when HasStagedOverworldSprite(node.PrimaryId)
                => "Status: this overworld sprite is staged. Other tabs and export use the staged version.",
            SpriteAssetKind.Portrait when HasStagedPortrait(node.PrimaryId, node.SecondaryId)
                => "Status: this portrait is staged. Other tabs and export use the staged version.",
            SpriteAssetKind.MapTileset
                => "Status: showing map tileset graphics. Editing/writeback is not wired yet; use this as the future tile picker surface.",
            SpriteAssetKind.BattleCompositePartComponent when HasStagedBattleCompositeComponent(node.PrimaryId, node.SecondaryId)
                => "Status: this Medabot component sprite is staged. Other tabs and export use the staged version.",
            SpriteAssetKind.PartCompositeDescriptorPiece when HasStagedLargeDisplay(node.PrimaryId, PartSpriteDisplayLayout.GetLargeDisplayVariantSelectorForComponent(GetRequiredPartDefinition(node.PrimaryId).Kind, node.SecondaryId))
                => "Status: this Large Display sprite is staged. Other tabs and export use the staged version.",
            SpriteAssetKind.BattleCompositePartComponent
                => "Status: editing a Medabot/component battle sprite family. Palette changes affect every part using that shared family palette.",
            SpriteAssetKind.MedabotLargePreview
                => "Status: readonly full Medabot large preview using ROM-derived combined placement with draft/staged part sprite overlays.",
            SpriteAssetKind.MedabotBattlePreview
                => "Status: readonly full Medabot battle preview composed from the 6 battle component sprites.",
            SpriteAssetKind.PartCompositePreview
                => "Status: readonly assembled complete part preview with in-game compositing.",
            SpriteAssetKind.PartCompositeDescriptorPiece
                => "Status: editing a single underlying large-display sprite piece.",
            SpriteAssetKind.PartCompositeParsedDescriptor
                => "Status: readonly parsed descriptor preview. Use Editable Sprites to edit the underlying piece.",
            _ => "Status: showing ROM data."
        };
    }

    private void UpdateSpritePaletteFamilyEditor(SpriteBrowserNode node)
    {
        if (!IsCompositePaletteFamilyEditable(node))
        {
            _isUpdatingSpritePaletteFamilyUi = true;
            SpritePaletteFamilyEditorPanel.Visibility = Visibility.Collapsed;
            SpritePaletteFamilyComboBox.SelectedItem = null;
            SpritePaletteFamilyHintLabel.Text = string.Empty;
            _isUpdatingSpritePaletteFamilyUi = false;
            return;
        }

        var asset = GetSelectedBattleCompositeComponentAsset(node);
        _isUpdatingSpritePaletteFamilyUi = true;
        SpritePaletteFamilyEditorPanel.Visibility = Visibility.Visible;
        SpritePaletteFamilyComboBox.SelectedValue = asset.PaletteFamily;
        SpritePaletteFamilyHintLabel.Text = "Part sprites use shared family palettes. Changing the family changes which shared palette row this component uses in-game.";
        _isUpdatingSpritePaletteFamilyUi = false;
    }

    private void UpdateBotBattleFacingEditor(SpriteBrowserNode node)
    {
        if (node.AssetKind != SpriteAssetKind.MedabotBattlePreview)
        {
            BotBattleFacingEditorPanel.Visibility = Visibility.Collapsed;
            return;
        }

        BotBattleFacingEditorPanel.Visibility = Visibility.Visible;
        BotBattleFacingComboBox.SelectedValue = _selectedBotBattleFacing;
    }

    private void UpdateParsedDescriptorEditor(SpriteBrowserNode node)
    {
        if (node.AssetKind != SpriteAssetKind.PartCompositeParsedDescriptor)
        {
            ParsedDescriptorEditorPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var piece = GetParsedDescriptorRecord(node);
        ParsedDescriptorEditorPanel.Visibility = Visibility.Visible;
        ParsedDescriptorXEntry.Text = piece.X.ToString();
        ParsedDescriptorYEntry.Text = piece.Y.ToString();
        ParsedDescriptorSiblingEntry.Text = piece.SiblingDescriptorId.ToString();
        ParsedDescriptorChildEntry.Text = piece.ChildDescriptorId.ToString();
        ParsedDescriptorPaletteBankEntry.Text = piece.PaletteBank.ToString();
        ParsedDescriptorWidthEntry.Text = piece.RawWidth.ToString();
        ParsedDescriptorHeightEntry.Text = piece.RawHeight.ToString();
        ParsedDescriptorDivisorsEntry.Text = piece.SizeDivisors.ToString();
        ParsedDescriptorRaw0CEntry.Text = piece.RawByte0C.ToString();
        ParsedDescriptorRaw0DEntry.Text = piece.RawByte0D.ToString();
        ParsedDescriptorRaw0EEntry.Text = piece.RawByte0E.ToString();
        ParsedDescriptorRaw15Entry.Text = piece.RawByte15.ToString();
        ParsedDescriptorRaw16Entry.Text = piece.RawByte16.ToString();
        ParsedDescriptorRaw17Entry.Text = piece.RawByte17.ToString();
        ParsedDescriptorHeaderLabel.Text =
            $"Descriptor ptr: 0x{piece.DescriptorPointerOffset:X6}  |  Record: 0x{piece.RecordOffset:X6}{Environment.NewLine}" +
            $"Blob table: 0x{piece.BlobPointerTableOffset:X6}  |  Selected table index: {piece.TableIndex}{Environment.NewLine}" +
            $"Raw XY: ({piece.RawX}, {piece.RawY})  |  Effective size: {piece.EffectiveWidth}x{piece.EffectiveHeight}  |  Divisors: {piece.WidthDivisor}x{piece.HeightDivisor}";
        ParsedDescriptorVariantInfoLabel.Text = BuildLargeDisplayVariantResolutionInfo(piece);
    }

    private void UpdateLargeBotLivePreview(SpriteBrowserNode node)
    {
        if (!TryGetLargeDisplayMedabotIdForLivePreview(node, out var medabotId))
        {
            LargeBotLivePreviewPanel.Visibility = Visibility.Collapsed;
            LargeBotLivePreviewImage.Source = null;
            LargeBotLivePreviewLabel.Text = string.Empty;
            return;
        }

        var preview = CreateMedabotLargePreviewStripBitmap(medabotId);
        LargeBotLivePreviewImage.Source = preview.Bitmap;
        LargeBotLivePreviewPanel.Visibility = Visibility.Visible;
        LargeBotLivePreviewLabel.Text = $"Medabot {medabotId:D3} {_metadata.GetBotName(medabotId)}. Uses ROM combined-frame placement with current draft/staged large-display sprite edits.";
    }

    private bool TryGetLargeDisplayMedabotIdForLivePreview(SpriteBrowserNode node, out int medabotId)
    {
        medabotId = -1;
        switch (node.AssetKind)
        {
            case SpriteAssetKind.PartCompositePreview:
            case SpriteAssetKind.PartCompositeDescriptorPiece:
            case SpriteAssetKind.PartCompositeParsedDescriptor:
                medabotId = GetRequiredPartDefinition(node.PrimaryId).MedabotId;
                return true;
            case SpriteAssetKind.PartCompositeEditableSprite:
                medabotId = node.PrimaryId;
                return true;
            default:
                return false;
        }
    }

    private bool IsCompositePaletteFamilyEditable(SpriteBrowserNode node)
    {
        return node.AssetKind is SpriteAssetKind.BattleCompositePartComponent;
    }

    private void OnBotBattleFacingSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_isWindowFullyInitialized)
        {
            return;
        }

        if (BotBattleFacingComboBox.SelectedValue is not int facing || facing == _selectedBotBattleFacing)
        {
            return;
        }

        _selectedBotBattleFacing = facing;
        if (_selectedSpriteNode?.AssetKind == SpriteAssetKind.MedabotBattlePreview)
        {
            InvalidateSelectedSpritePreview();
        }
    }

    private BattleCompositeSpriteComponentAsset GetSelectedBattleCompositeComponentAsset(SpriteBrowserNode node)
    {
        return node.AssetKind switch
        {
            SpriteAssetKind.BattleCompositePartComponent => GetPreviewBattleCompositeComponentAsset(node.PrimaryId, node.SecondaryId),
            SpriteAssetKind.PartCompositePreview or SpriteAssetKind.PartCompositeDescriptorPiece or SpriteAssetKind.PartCompositeParsedDescriptor => GetPreviewBattleCompositeComponentAsset(GetRequiredPartDefinition(node.PrimaryId).MedabotId, node.SecondaryId),
            SpriteAssetKind.PartCompositeEditableSprite => GetPreviewBattleCompositeComponentAsset(node.PrimaryId, 0),
            _ => throw new InvalidOperationException("The selected sprite node does not use a composite component asset.")
        };
    }

    private PartDefinition GetRequiredPartForMedabot(int medabotId, PartKind kind)
    {
        return _loadedParts.FirstOrDefault(part => part.MedabotId == medabotId && part.Kind == kind)
            ?? throw new InvalidOperationException($"Could not resolve {kind} for Medabot {medabotId:D3}.");
    }

    private bool HasStagedOverworldSprite(int spriteId) =>
        ProjectEditCollection.Find(_project, ProjectEditAdapters.OverworldSprite, spriteId) is not null;

    private bool HasStagedPortrait(int characterId, int portraitIndex) =>
        ProjectEditCollection.Find(_project, ProjectEditAdapters.Portrait, (characterId, portraitIndex)) is not null;

    private bool HasStagedBattleCompositeComponent(int medabotId, int componentIndex) =>
        ProjectEditCollection.Find(_project, ProjectEditAdapters.BattleCompositeSprite, (medabotId, componentIndex)) is not null;

    private bool HasStagedLargeDisplay(int partId, int variantSelector) =>
        ProjectEditCollection.Find(_project, ProjectEditAdapters.LargeDisplaySprite, (partId, variantSelector)) is not null;

    private BattleCompositeSpriteComponentAsset GetEditableSelectedBattleCompositeComponentAsset(SpriteBrowserNode node)
    {
        return node.AssetKind switch
        {
            SpriteAssetKind.BattleCompositePartComponent => GetEditableBattleCompositeComponentAsset(node.PrimaryId, node.SecondaryId),
            SpriteAssetKind.PartCompositeDescriptorPiece => GetEditableBattleCompositeComponentAsset(GetRequiredPartDefinition(node.PrimaryId).MedabotId, node.SecondaryId),
            _ => throw new InvalidOperationException("The selected sprite node does not use an editable composite component asset.")
        };
    }

    private void UpdateSelectedPaletteSwatch()
    {
        if (SpritePaletteItemsControl.ItemsSource is not IEnumerable<PaletteSwatchItem> swatches)
        {
            return;
        }

        foreach (var swatch in swatches)
        {
            swatch.IsSelected = swatch.Index == _selectedPaletteIndex;
        }

        SpritePaletteItemsControl.Items.Refresh();
    }

    private void RefreshSpritePaletteFamilyOptions()
    {
        _spritePaletteFamilyOptions.Clear();
        for (var index = 0; index < MedabotsRomSchema.CompositeBattleSpritePaletteCount; index++)
        {
            var paletteBytes = _session is null
                ? new byte[ImageAssetRepository.PaletteSize]
                : _imageAssetRepository.ReadBattleCompositePaletteBytesForFamily(_session.RomFile, (byte)index);
            _spritePaletteFamilyOptions.Add(new SpritePaletteFamilyOption
            {
                Value = (byte)index,
                DisplayName = $"Family {index}",
                PreviewSwatches = BuildPaletteSwatches(paletteBytes).Take(4).ToArray()
            });
        }

        SpritePaletteFamilyComboBox.ItemsSource = null;
        SpritePaletteFamilyComboBox.ItemsSource = _spritePaletteFamilyOptions;
    }

    private void SetSpriteEditorTool(SpriteEditorTool tool)
    {
        _selectedSpriteEditorTool = tool;
        SpriteToolPencilButton.IsChecked = tool == SpriteEditorTool.Pencil;
        SpriteToolEraserButton.IsChecked = tool == SpriteEditorTool.Eraser;
        SpriteToolPickerButton.IsChecked = tool == SpriteEditorTool.Picker;
    }

    private void SetSpriteZoom(int nextZoom, ScrollViewer? scrollViewer = null, WpfPoint? pointer = null)
    {
        var clampedZoom = Math.Clamp(nextZoom, 1, 24);
        if (clampedZoom == _spriteEditorZoom && SpritePreviewImage?.Source is BitmapSource currentBitmap)
        {
            UpdateSpritePreviewLayout(currentBitmap.PixelWidth, currentBitmap.PixelHeight);
            UpdateSpriteGridOverlay(currentBitmap.PixelWidth, currentBitmap.PixelHeight);
            return;
        }

        var oldZoom = _spriteEditorZoom;
        var anchorSourceX = 0d;
        var anchorSourceY = 0d;
        var shouldRecenterToPointer = scrollViewer is not null && pointer.HasValue && SpritePreviewImage?.Source is BitmapSource;

        if (shouldRecenterToPointer)
        {
            var anchorPoint = pointer.GetValueOrDefault();
            var image = SpritePreviewImage!;
            anchorSourceX = (scrollViewer!.HorizontalOffset + anchorPoint.X - image.Margin.Left) / Math.Max(1, oldZoom);
            anchorSourceY = (scrollViewer.VerticalOffset + anchorPoint.Y - image.Margin.Top) / Math.Max(1, oldZoom);
        }

        _spriteEditorZoom = clampedZoom;
        if (SpriteZoomValueLabel is not null)
        {
            SpriteZoomValueLabel.Text = $"Zoom {_spriteEditorZoom}x";
        }

        if (SpritePreviewImage is not null && SpritePreviewImage.Source is BitmapSource bitmap)
        {
            UpdateSpritePreviewLayout(bitmap.PixelWidth, bitmap.PixelHeight);
            UpdateSpriteGridOverlay(bitmap.PixelWidth, bitmap.PixelHeight);

            if (shouldRecenterToPointer)
            {
                var anchorPoint = pointer.GetValueOrDefault();
                var targetHorizontalOffset = (SpritePreviewImage.Margin.Left + (anchorSourceX * _spriteEditorZoom)) - anchorPoint.X;
                var targetVerticalOffset = (SpritePreviewImage.Margin.Top + (anchorSourceY * _spriteEditorZoom)) - anchorPoint.Y;
                scrollViewer!.ScrollToHorizontalOffset(Math.Max(0, targetHorizontalOffset));
                scrollViewer.ScrollToVerticalOffset(Math.Max(0, targetVerticalOffset));
            }
        }
    }

    private void OnSpritePreviewViewportSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (SpritePreviewImage?.Source is BitmapSource bitmap)
        {
            UpdateSpritePreviewLayout(bitmap.PixelWidth, bitmap.PixelHeight);
            UpdateSpriteGridOverlay(bitmap.PixelWidth, bitmap.PixelHeight);
        }
    }

    private void OnSpritePreviewMouseWheel(object? sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
        {
            return;
        }

        var delta = e.Delta > 0 ? 1 : -1;
        SetSpriteZoom(_spriteEditorZoom + delta, scrollViewer, e.GetPosition(scrollViewer));
        e.Handled = true;
    }

    private void OnSpritePreviewScrollViewerMouseDown(object? sender, MouseButtonEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer || e.ChangedButton != MouseButton.Middle)
        {
            return;
        }

        _isPanningSpritePreview = true;
        _spritePanStartPoint = e.GetPosition(scrollViewer);
        _spritePanStartHorizontalOffset = scrollViewer.HorizontalOffset;
        _spritePanStartVerticalOffset = scrollViewer.VerticalOffset;
        scrollViewer.Cursor = WpfCursors.SizeAll;
        scrollViewer.CaptureMouse();
        e.Handled = true;
    }

    private void OnSpritePreviewScrollViewerMouseMove(object? sender, WpfMouseEventArgs e)
    {
        if (!_isPanningSpritePreview || sender is not ScrollViewer scrollViewer)
        {
            return;
        }

        var point = e.GetPosition(scrollViewer);
        var deltaX = point.X - _spritePanStartPoint.X;
        var deltaY = point.Y - _spritePanStartPoint.Y;
        scrollViewer.ScrollToHorizontalOffset(Math.Max(0, _spritePanStartHorizontalOffset - deltaX));
        scrollViewer.ScrollToVerticalOffset(Math.Max(0, _spritePanStartVerticalOffset - deltaY));
        e.Handled = true;
    }

    private void OnSpritePreviewScrollViewerMouseUp(object? sender, MouseButtonEventArgs e)
    {
        if (!_isPanningSpritePreview || sender is not ScrollViewer scrollViewer || e.ChangedButton != MouseButton.Middle)
        {
            return;
        }

        _isPanningSpritePreview = false;
        scrollViewer.Cursor = WpfCursors.Arrow;
        scrollViewer.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void OnSpriteToolPencilClicked(object? sender, RoutedEventArgs e) => SetSpriteEditorTool(SpriteEditorTool.Pencil);
    private void OnSpriteToolEraserClicked(object? sender, RoutedEventArgs e) => SetSpriteEditorTool(SpriteEditorTool.Eraser);
    private void OnSpriteToolPickerClicked(object? sender, RoutedEventArgs e) => SetSpriteEditorTool(SpriteEditorTool.Picker);

    private void OnSpritePaletteSwatchClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton button || button.Tag is not int index)
        {
            return;
        }

        _selectedPaletteIndex = index;
        UpdateSelectedPaletteSwatch();
    }

    private void OnSpritePaletteSwatchRightButtonDown(object? sender, MouseButtonEventArgs e)
    {
        if (sender is not WpfButton button || button.Tag is not int index)
        {
            return;
        }

        _selectedPaletteIndex = index;
        UpdateSelectedPaletteSwatch();
    }

    private void OnSpritePaletteFamilySelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingSpritePaletteFamilyUi || _selectedSpriteNode is null || _session is null || !IsCompositePaletteFamilyEditable(_selectedSpriteNode))
        {
            return;
        }

        if (SpritePaletteFamilyComboBox.SelectedValue is not byte family)
        {
            return;
        }

        var asset = GetEditableSelectedBattleCompositeComponentAsset(_selectedSpriteNode);
        if (asset.PaletteFamily == family)
        {
            return;
        }

        PushUndoSnapshot(GetSelectedSpriteHistoryKey(), asset.Image);
        var paletteBytes = _imageAssetRepository.ReadBattleCompositePaletteBytesForFamily(_session.RomFile, family);
        var updated = asset with
        {
            PaletteFamily = family,
            PaletteOffset = MedabotsRomSchema.PartSelectionComponentPaletteSetOffset + (family * ImageAssetRepository.PaletteSize),
            PaletteSelector = (byte)(family + 4),
            Image = asset.Image with { PaletteBytes = paletteBytes }
        };
        _editedBattleCompositeComponentAssets[(updated.MedabotId, updated.ComponentIndex)] = updated;
        _hasCapturedUndoForCurrentStroke = false;
        InvalidateSelectedSpritePreview();
        SpritePatchStatusLabel.Text = $"Status: staged palette family {family} for this Medabot component. Export ROM will patch the family selector byte.";
    }

    private async void OnEditPaletteColorMenuClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is WpfMenuItem menuItem && menuItem.CommandParameter is int index)
        {
            _selectedPaletteIndex = index;
            UpdateSelectedPaletteSwatch();
        }

        await EditSelectedPaletteColorAsync();
    }

    private async Task EditSelectedPaletteColorAsync()
    {
        if (_selectedSpriteNode is null || !_selectedSpriteNode.IsAsset)
        {
            await DisplayAlertAsync("No Asset Selected", "Select a sprite, portrait, Medabot composite sprite, or individual part preview before editing palette colors.", "OK");
            return;
        }

        try
        {
            switch (_selectedSpriteNode.AssetKind)
            {
                case SpriteAssetKind.OverworldEventObject:
                    EditPaletteColor(GetEditableOverworldSpriteAsset(_selectedSpriteNode.PrimaryId).Image);
                    break;
                case SpriteAssetKind.Portrait:
                    EditPaletteColor(GetEditablePortraitAsset(_selectedSpriteNode.PrimaryId, _selectedSpriteNode.SecondaryId).Image);
                    break;
                case SpriteAssetKind.BattleCompositePartComponent:
                    await DisplayAlertAsync("Use Palette Family", "Part sprites use shared family palettes. Change the Palette Family selector instead of editing palette colors directly.", "OK");
                    return;
                case SpriteAssetKind.PartCompositeDescriptorPiece:
                    EditLargeDisplayPalette(GetEditableLargePartDisplayAsset(_selectedSpriteNode.PrimaryId, _selectedSpriteNode.SecondaryId));
                    break;
                case SpriteAssetKind.PartCompositePreview:
                case SpriteAssetKind.PartCompositeParsedDescriptor:
                    await DisplayAlertAsync("Read Only", "This view is readonly. Use Editable Sprites to edit the underlying large-display sprite.", "OK");
                    return;
                default:
                    return;
            }

            _hasCapturedUndoForCurrentStroke = false;
            InvalidateSelectedSpritePreview();
            SpritePatchStatusLabel.Text = $"Status: updated palette color {_selectedPaletteIndex:X2} for the staged asset.";
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Palette Edit Failed", ex.Message, "OK");
        }
    }

    private void EditPaletteColor(IndexedImage image)
    {
        var paletteOffset = _selectedPaletteIndex * 2;
        if (paletteOffset < 0 || paletteOffset + 1 >= image.PaletteBytes.Length)
        {
            throw new InvalidOperationException("The selected palette index is out of range.");
        }

        var originalRaw = (ushort)(image.PaletteBytes[paletteOffset] | (image.PaletteBytes[paletteOffset + 1] << 8));
        var originalColor = DecodeGbaColor(originalRaw);

        using var dialog = new Forms.ColorDialog
        {
            AllowFullOpen = true,
            FullOpen = true,
            Color = System.Drawing.Color.FromArgb(originalColor.R, originalColor.G, originalColor.B)
        };

        if (dialog.ShowDialog() != Forms.DialogResult.OK)
        {
            return;
        }

        PushUndoSnapshot(GetSelectedSpriteHistoryKey(), image);
        var encoded = EncodeGbaColor(WpfColor.FromRgb(dialog.Color.R, dialog.Color.G, dialog.Color.B));
        image.PaletteBytes[paletteOffset] = (byte)(encoded & 0xFF);
        image.PaletteBytes[paletteOffset + 1] = (byte)(encoded >> 8);
    }

    private void EditLargeDisplayPalette(LargePartDisplayAsset asset)
    {
        var editablePieces = asset.Pieces
            .Where(piece => piece.PalettePointerOffset > 0 && piece.PaletteBytes.Length >= ImageAssetRepository.PaletteSize)
            .ToArray();
        if (editablePieces.Length == 0)
        {
            throw new InvalidOperationException("This Large Display does not have a part-specific uploaded palette to edit.");
        }

        var paletteOffset = _selectedPaletteIndex * 2;
        if (paletteOffset < 0 || paletteOffset + 1 >= ImageAssetRepository.PaletteSize)
        {
            throw new InvalidOperationException("The selected palette index is out of range.");
        }

        var sourcePalette = editablePieces[0].PaletteBytes;
        var originalRaw = (ushort)(sourcePalette[paletteOffset] | (sourcePalette[paletteOffset + 1] << 8));
        var originalColor = DecodeGbaColor(originalRaw);

        using var dialog = new Forms.ColorDialog
        {
            AllowFullOpen = true,
            FullOpen = true,
            Color = System.Drawing.Color.FromArgb(originalColor.R, originalColor.G, originalColor.B)
        };

        if (dialog.ShowDialog() != Forms.DialogResult.OK)
        {
            return;
        }

        PushUndoSnapshot(GetSelectedSpriteHistoryKey(), asset);
        var encoded = EncodeGbaColor(WpfColor.FromRgb(dialog.Color.R, dialog.Color.G, dialog.Color.B));
        foreach (var piece in editablePieces)
        {
            piece.PaletteBytes[paletteOffset] = (byte)(encoded & 0xFF);
            piece.PaletteBytes[paletteOffset + 1] = (byte)(encoded >> 8);
            piece.Image.PaletteBytes[paletteOffset] = (byte)(encoded & 0xFF);
            piece.Image.PaletteBytes[paletteOffset + 1] = (byte)(encoded >> 8);
        }
    }

    private void UpdateSpriteGridOverlay(int pixelWidth, int pixelHeight)
    {
        SpriteGridCanvas.Children.Clear();
        SpriteGridCanvas.Width = pixelWidth * _spriteEditorZoom;
        SpriteGridCanvas.Height = pixelHeight * _spriteEditorZoom;

        if (_spriteEditorZoom < 8)
        {
            return;
        }

        var gridBrush = new SolidColorBrush(WpfColor.FromArgb(80, 107, 114, 128));
        for (var x = 0; x <= pixelWidth; x++)
        {
            SpriteGridCanvas.Children.Add(new System.Windows.Shapes.Line
            {
                X1 = x * _spriteEditorZoom,
                Y1 = 0,
                X2 = x * _spriteEditorZoom,
                Y2 = pixelHeight * _spriteEditorZoom,
                Stroke = gridBrush,
                StrokeThickness = x % 8 == 0 ? 1.0 : 0.5
            });
        }

        for (var y = 0; y <= pixelHeight; y++)
        {
            SpriteGridCanvas.Children.Add(new System.Windows.Shapes.Line
            {
                X1 = 0,
                Y1 = y * _spriteEditorZoom,
                X2 = pixelWidth * _spriteEditorZoom,
                Y2 = y * _spriteEditorZoom,
                Stroke = gridBrush,
                StrokeThickness = y % 8 == 0 ? 1.0 : 0.5
            });
        }
    }

    private void UpdateSpritePreviewLayout(int pixelWidth, int pixelHeight)
    {
        var scaledWidth = pixelWidth * _spriteEditorZoom;
        var scaledHeight = pixelHeight * _spriteEditorZoom;

        SpritePreviewImage.Width = scaledWidth;
        SpritePreviewImage.Height = scaledHeight;
        SpriteGridCanvas.Width = scaledWidth;
        SpriteGridCanvas.Height = scaledHeight;

        var viewportWidth = Math.Max(0d, SpritePreviewScrollViewer?.ViewportWidth ?? 0d);
        var viewportHeight = Math.Max(0d, SpritePreviewScrollViewer?.ViewportHeight ?? 0d);

        var surfaceWidth = Math.Max(scaledWidth + (SpriteViewportPadding * 2), viewportWidth + (SpriteViewportPadding * 2));
        var surfaceHeight = Math.Max(scaledHeight + (SpriteViewportPadding * 2), viewportHeight + (SpriteViewportPadding * 2));

        var offsetX = Math.Max(SpriteViewportPadding, (surfaceWidth - scaledWidth) / 2d);
        var offsetY = Math.Max(SpriteViewportPadding, (surfaceHeight - scaledHeight) / 2d);

        SpritePreviewSurface.Width = surfaceWidth;
        SpritePreviewSurface.Height = surfaceHeight;
        SpritePreviewImage.Margin = new Thickness(offsetX, offsetY, 0, 0);
        SpriteGridCanvas.Margin = new Thickness(offsetX, offsetY, 0, 0);
    }

    private void OnSpritePreviewMouseLeftButtonDown(object? sender, MouseButtonEventArgs e)
    {
        if (_selectedSpriteNode is null || SpritePreviewImage.Source is not BitmapSource)
        {
            return;
        }

        _isPaintingSprite = true;
        _hasCapturedUndoForCurrentStroke = false;
        SpritePreviewImage.CaptureMouse();
        ApplySpriteToolAtPoint(e.GetPosition(SpritePreviewImage));
    }

    private void OnSpritePreviewMouseMove(object? sender, WpfMouseEventArgs e)
    {
        if (!_isPaintingSprite || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        ApplySpriteToolAtPoint(e.GetPosition(SpritePreviewImage));
    }

    private void OnSpritePreviewMouseLeftButtonUp(object? sender, MouseButtonEventArgs e)
    {
        _isPaintingSprite = false;
        _hasCapturedUndoForCurrentStroke = false;
        SpritePreviewImage.ReleaseMouseCapture();
    }

    private void ApplySpriteToolAtPoint(WpfPoint point)
    {
        if (_selectedSpriteNode is null)
        {
            return;
        }

        var pixelX = 0;
        var pixelY = 0;
        if (_selectedSpriteNode.AssetKind is not SpriteAssetKind.PartCompositePreview &&
            !TryResolveSpritePixel(point, out pixelX, out pixelY))
        {
            return;
        }

        switch (_selectedSpriteNode.AssetKind)
        {
            case SpriteAssetKind.OverworldEventObject:
            {
                var asset = GetEditableOverworldSpriteAsset(_selectedSpriteNode.PrimaryId);
                ApplyToolToIndexedImage(asset.Image, pixelX, pixelY);
                break;
            }
            case SpriteAssetKind.Portrait:
            {
                var asset = GetEditablePortraitAsset(_selectedSpriteNode.PrimaryId, _selectedSpriteNode.SecondaryId);
                ApplyToolToIndexedImage(asset.Image, pixelX, pixelY);
                break;
            }
            case SpriteAssetKind.BattleCompositePartComponent:
            {
                var asset = GetEditableBattleCompositeComponentAsset(_selectedSpriteNode.PrimaryId, _selectedSpriteNode.SecondaryId);
                ApplyToolToIndexedImage(asset.Image, pixelX, pixelY);
                break;
            }
            case SpriteAssetKind.PartCompositePreview:
                return;
            case SpriteAssetKind.PartCompositeDescriptorPiece:
            {
                if (!TryResolveLargeDisplayDescriptorPixel(point, out var pieceIndex, out var piecePixelX, out var piecePixelY))
                {
                    return;
                }

                var asset = GetEditableLargePartDisplayAsset(_selectedSpriteNode.PrimaryId, _selectedSpriteNode.SecondaryId);
                ApplyToolToLargeDisplayAsset(asset, pieceIndex, piecePixelX, piecePixelY);
                break;
            }
            case SpriteAssetKind.PartCompositeEditableSprite:
            {
                if (!TryResolveEditableLargeDisplaySpritePixel(point, out var piecePixelX, out var piecePixelY))
                {
                    return;
                }

                var selectedReference = GetPreviewEditableLargeDisplaySpriteReference(_selectedSpriteNode);
                var references = GetEditableLargeDisplaySpriteReferences(_selectedSpriteNode.PrimaryId, selectedReference.Piece.ImageOffset);
                if (references.Count == 0)
                {
                    return;
                }

                if ((_selectedSpriteEditorTool == SpriteEditorTool.Pencil || _selectedSpriteEditorTool == SpriteEditorTool.Eraser) && !_hasCapturedUndoForCurrentStroke)
                {
                    PushUndoSnapshot(GetSelectedSpriteHistoryKey(), references[0].Piece.Image);
                    _hasCapturedUndoForCurrentStroke = true;
                }

                foreach (var reference in references)
                {
                    ApplyToolToIndexedImage(reference.Piece.Image, piecePixelX, piecePixelY);
                }
                break;
            }
            case SpriteAssetKind.PartCompositeParsedDescriptor:
                return;
            default:
                return;
        }

        InvalidateSelectedSpritePreview();
    }

    private bool TryResolveSpritePixel(WpfPoint point, out int pixelX, out int pixelY)
    {
        pixelX = (int)(point.X / _spriteEditorZoom);
        pixelY = (int)(point.Y / _spriteEditorZoom);
        if (_selectedSpriteNode is null)
        {
            return false;
        }

          var image = _selectedSpriteNode.AssetKind switch
          {
              SpriteAssetKind.OverworldEventObject => GetPreviewOverworldSpriteAsset(_selectedSpriteNode.PrimaryId).Image,
              SpriteAssetKind.Portrait => GetPreviewPortraitAsset(_selectedSpriteNode.PrimaryId, _selectedSpriteNode.SecondaryId).Image,
              SpriteAssetKind.MapTileset => GetCurrentMapTilesetAsset(_selectedSpriteNode.PrimaryId).TilesetSheet,
              SpriteAssetKind.BattleCompositePartComponent => GetPreviewBattleCompositeComponentAsset(_selectedSpriteNode.PrimaryId, _selectedSpriteNode.SecondaryId).Image,
              SpriteAssetKind.MedabotLargePreview or SpriteAssetKind.MedabotBattlePreview => null,
              SpriteAssetKind.PartCompositePreview => null,
              SpriteAssetKind.PartCompositeDescriptorPiece or SpriteAssetKind.PartCompositeParsedDescriptor => GetRenderedLargeDisplayPieceImage(
                  GetPreviewLargeDisplayPieceAsset(_selectedSpriteNode.PrimaryId, _selectedSpriteNode.SecondaryId, _selectedSpriteNode.TertiaryId),
                  GetRequiredPartDefinition(_selectedSpriteNode.PrimaryId).Kind,
                  GetPreviewLargePartDisplayAsset(_selectedSpriteNode.PrimaryId, _selectedSpriteNode.SecondaryId).Pieces.Count),
              SpriteAssetKind.PartCompositeEditableSprite => GetRenderedLargeDisplayPieceImage(
                  GetPreviewEditableLargeDisplaySpriteReference(_selectedSpriteNode).Piece,
                  GetPreviewEditableLargeDisplaySpriteReference(_selectedSpriteNode).Part.Kind,
                  GetPreviewEditableLargeDisplaySpriteReference(_selectedSpriteNode).PieceCount),
              _ => null
          };
        if (image is null)
        {
            return false;
        }

        return pixelX >= 0 && pixelY >= 0 && pixelX < image.Width && pixelY < image.Height;
    }

    private bool TryResolveLargeDisplayPixel(WpfPoint point, out int pieceIndex, out int pixelX, out int pixelY)
    {
        pieceIndex = -1;
        pixelX = 0;
        pixelY = 0;
        if (_selectedSpriteNode is null || _selectedSpriteNode.AssetKind != SpriteAssetKind.PartCompositePreview)
        {
            return false;
        }

        var preview = GetOrBuildSpritePreviewState(_selectedSpriteNode);
        if (preview.Pieces is null || preview.Pieces.Count == 0)
        {
            return false;
        }

        var asset = GetEditableLargePartDisplayAsset(_selectedSpriteNode.PrimaryId, _selectedSpriteNode.SecondaryId);

        var previewX = (int)(point.X / _spriteEditorZoom);
        var previewY = (int)(point.Y / _spriteEditorZoom);
        for (var index = preview.Pieces.Count - 1; index >= 0; index--)
        {
            var piece = preview.Pieces[index];
            if (previewX < piece.X || previewY < piece.Y || previewX >= piece.X + piece.Image.Width || previewY >= piece.Y + piece.Image.Height)
            {
                continue;
            }

            var displayPixelX = previewX - piece.X;
            var displayPixelY = previewY - piece.Y;
            if (piece.PieceIndex < 0 || piece.PieceIndex >= asset.Pieces.Count)
            {
                return false;
              }

              var sourcePiece = asset.Pieces[piece.PieceIndex];
              var displayTileIndex = (displayPixelY / 8) * piece.Image.TileWidth + (displayPixelX / 8);
              if (displayTileIndex < 0 || displayTileIndex >= sourcePiece.LoadedTileCount)
              {
                  return false;
              }

              var sourceTileX = displayTileIndex % sourcePiece.Image.TileWidth;
              var sourceTileY = displayTileIndex / sourcePiece.Image.TileWidth;
              var resolvedPixelX = (sourceTileX * 8) + (displayPixelX % 8);
              var resolvedPixelY = (sourceTileY * 8) + (displayPixelY % 8);
            if (resolvedPixelX < 0 || resolvedPixelY < 0 || resolvedPixelX >= sourcePiece.Image.Width || resolvedPixelY >= sourcePiece.Image.Height)
            {
                return false;
            }

            pieceIndex = piece.PieceIndex;
            pixelX = resolvedPixelX;
            pixelY = resolvedPixelY;
            return true;
        }

          return false;
      }

      private bool TryResolveLargeDisplayDescriptorPixel(WpfPoint point, out int pieceIndex, out int pixelX, out int pixelY)
      {
          pieceIndex = -1;
          pixelX = 0;
          pixelY = 0;
          if (_selectedSpriteNode is null || _selectedSpriteNode.AssetKind != SpriteAssetKind.PartCompositeDescriptorPiece)
          {
              return false;
          }

          var asset = GetEditableLargePartDisplayAsset(_selectedSpriteNode.PrimaryId, _selectedSpriteNode.SecondaryId);
          pieceIndex = Array.FindIndex(asset.Pieces.ToArray(), piece => piece.DescriptorId == _selectedSpriteNode.TertiaryId);
          if (pieceIndex < 0)
          {
              return false;
          }

          var sourcePiece = asset.Pieces[pieceIndex];
          var renderedImage = GetRenderedLargeDisplayPieceImage(sourcePiece, GetRequiredPartDefinition(_selectedSpriteNode.PrimaryId).Kind, asset.Pieces.Count);
          var displayPixelX = (int)(point.X / _spriteEditorZoom);
          var displayPixelY = (int)(point.Y / _spriteEditorZoom);
          if (displayPixelX < 0 || displayPixelY < 0 || displayPixelX >= renderedImage.Width || displayPixelY >= renderedImage.Height)
          {
              return false;
          }

          var displayTileIndex = (displayPixelY / 8) * renderedImage.TileWidth + (displayPixelX / 8);
          if (displayTileIndex < 0 || displayTileIndex >= sourcePiece.LoadedTileCount)
          {
              return false;
          }

          var sourceTileX = displayTileIndex % sourcePiece.Image.TileWidth;
          var sourceTileY = displayTileIndex / sourcePiece.Image.TileWidth;
          pixelX = (sourceTileX * 8) + (displayPixelX % 8);
          pixelY = (sourceTileY * 8) + (displayPixelY % 8);
          return pixelX >= 0 && pixelY >= 0 && pixelX < sourcePiece.Image.Width && pixelY < sourcePiece.Image.Height;
      }

    private bool TryResolveEditableLargeDisplaySpritePixel(WpfPoint point, out int pixelX, out int pixelY)
    {
        pixelX = 0;
        pixelY = 0;
        if (_selectedSpriteNode is null || _selectedSpriteNode.AssetKind != SpriteAssetKind.PartCompositeEditableSprite)
        {
            return false;
        }

        var reference = GetPreviewEditableLargeDisplaySpriteReference(_selectedSpriteNode);
        var renderedImage = GetRenderedLargeDisplayPieceImage(reference.Piece, reference.Part.Kind, reference.PieceCount);
        var displayPixelX = (int)(point.X / _spriteEditorZoom);
        var displayPixelY = (int)(point.Y / _spriteEditorZoom);
        if (displayPixelX < 0 || displayPixelY < 0 || displayPixelX >= renderedImage.Width || displayPixelY >= renderedImage.Height)
        {
            return false;
        }

        var displayTileIndex = (displayPixelY / 8) * renderedImage.TileWidth + (displayPixelX / 8);
        if (displayTileIndex < 0 || displayTileIndex >= reference.Piece.LoadedTileCount)
        {
            return false;
        }

        var sourceTileX = displayTileIndex % reference.Piece.Image.TileWidth;
        var sourceTileY = displayTileIndex / reference.Piece.Image.TileWidth;
        pixelX = (sourceTileX * 8) + (displayPixelX % 8);
        pixelY = (sourceTileY * 8) + (displayPixelY % 8);
        return pixelX >= 0 && pixelY >= 0 && pixelX < reference.Piece.Image.Width && pixelY < reference.Piece.Image.Height;
    }

    private SpriteAsset GetEditableOverworldSpriteAsset(int spriteId)
    {
        if (_editedOverworldSpriteAssets.TryGetValue(spriteId, out var edited))
        {
            return edited;
        }

        var current = GetCurrentOverworldSpriteAsset(spriteId);
        var clone = current with
        {
            Image = new IndexedImage(current.Image.TileWidth, current.Image.TileHeight, current.Image.PixelIndices.ToArray(), current.Image.PaletteBytes.ToArray())
        };
        _editedOverworldSpriteAssets[spriteId] = clone;
        return clone;
    }

    private PortraitAsset GetEditablePortraitAsset(int characterId, int portraitIndex)
    {
        if (_editedPortraitAssets.TryGetValue((characterId, portraitIndex), out var edited))
        {
            return edited;
        }

        var current = GetCurrentPortraitAsset(characterId, portraitIndex);
        var clone = current with
        {
            Image = new IndexedImage(current.Image.TileWidth, current.Image.TileHeight, current.Image.PixelIndices.ToArray(), current.Image.PaletteBytes.ToArray())
        };
        _editedPortraitAssets[(characterId, portraitIndex)] = clone;
        return clone;
    }

    private SpriteAsset GetPreviewOverworldSpriteAsset(int spriteId)
    {
        return _editedOverworldSpriteAssets.TryGetValue(spriteId, out var edited)
            ? edited
            : GetCurrentOverworldSpriteAsset(spriteId);
    }

    private PortraitAsset GetPreviewPortraitAsset(int characterId, int portraitIndex)
    {
        return _editedPortraitAssets.TryGetValue((characterId, portraitIndex), out var edited)
            ? edited
            : GetCurrentPortraitAsset(characterId, portraitIndex);
    }

    private BattleCompositeSpriteComponentAsset GetPreviewBattleCompositeComponentAsset(int medabotId, int componentIndex)
    {
        return _editedBattleCompositeComponentAssets.TryGetValue((medabotId, componentIndex), out var edited)
            ? edited
            : GetCurrentBattleCompositeComponentAsset(medabotId, componentIndex);
    }

    private LargePartDisplayAsset GetPreviewLargePartDisplayAsset(int partId, int componentIndex)
    {
        var part = GetRequiredPartDefinition(partId);
        var variantSelector = PartSpriteDisplayLayout.GetLargeDisplayVariantSelectorForComponent(part.Kind, componentIndex);
        return _editedLargePartDisplayAssets.TryGetValue((partId, variantSelector), out var edited)
            ? edited
            : GetCurrentLargePartDisplayAsset(partId, componentIndex);
    }

    private LargePartDisplayPieceAsset GetPreviewLargeDisplayPieceAsset(int partId, int componentIndex, int descriptorId)
    {
        var asset = GetPreviewLargePartDisplayAsset(partId, componentIndex);
        return asset.Pieces.FirstOrDefault(piece => piece.DescriptorId == descriptorId)
            ?? throw new InvalidOperationException($"Descriptor {descriptorId} is not present in large display part {partId}.");
    }

    private LargePartDisplayPieceAsset? TryGetDraftOrStagedLargeDisplayImageOverride(int partId, int componentIndex, int imageOffset)
    {
        var part = GetRequiredPartDefinition(partId);
        var variantSelector = PartSpriteDisplayLayout.GetLargeDisplayVariantSelectorForComponent(part.Kind, componentIndex);
        if (_editedLargePartDisplayAssets.TryGetValue((partId, variantSelector), out var draft))
        {
            var draftPiece = draft.Pieces.FirstOrDefault(piece => piece.ImageOffset == imageOffset);
            if (draftPiece is not null)
            {
                return draftPiece;
            }
        }

        var staged = ProjectEditCollection.Find(_project, ProjectEditAdapters.LargeDisplaySprite, (partId, variantSelector));
        return staged?.Pieces.FirstOrDefault(piece => piece.ImageOffset == imageOffset);
    }

    private LargePartDisplayPieceAsset ResolveEditableLargeDisplayPreviewPiece(EditableLargeDisplaySpriteReference reference)
    {
        var imageOverride = TryGetDraftOrStagedLargeDisplayImageOverride(reference.Part.Id, reference.ComponentIndex, reference.Piece.ImageOffset);
        if (imageOverride is null)
        {
            return reference.Piece;
        }

        return reference.Piece with
        {
            PaletteBytes = imageOverride.PaletteBytes.ToArray(),
            Image = new IndexedImage(
                imageOverride.Image.TileWidth,
                imageOverride.Image.TileHeight,
                imageOverride.Image.PixelIndices.ToArray(),
                imageOverride.Image.PaletteBytes.ToArray())
        };
    }

    private EditableLargeDisplaySpriteReference GetPreviewEditableLargeDisplaySpriteReference(SpriteBrowserNode node)
    {
        return GetSpecificLargeDisplaySpriteReference(node.PrimaryId, node.SecondaryId, node.TertiaryId, node.DataOffset, node.SharedSourcePrimaryId, editable: false)
            ?? throw new InvalidOperationException($"Could not resolve editable large display sprite 0x{node.DataOffset:X6} for Medabot {node.PrimaryId:D3}.");
    }

    private List<EditableLargeDisplaySpriteReference> GetEditableLargeDisplaySpriteReferences(int medabotId, int imageOffset)
    {
        return GetLargeDisplaySpriteReferences(medabotId, imageOffset, editable: true);
    }

    private List<EditableLargeDisplaySpriteReference> GetLargeDisplaySpriteReferences(int medabotId, int imageOffset, bool editable)
    {
        var references = new List<EditableLargeDisplaySpriteReference>();
        foreach (var part in _loadedParts.Where(part => part.MedabotId == medabotId).OrderBy(part => part.Kind).ThenBy(part => part.Id))
        {
            foreach (var (componentIndex, _) in PartSpriteDisplayLayout.GetPreviewComponentEntriesForPartKind(part.Kind))
            {
                var asset = editable
                    ? GetEditableLargePartDisplayAsset(part.Id, componentIndex)
                    : GetPreviewLargePartDisplayAsset(part.Id, componentIndex);
                for (var index = 0; index < asset.Pieces.Count; index++)
                {
                    var piece = asset.Pieces[index];
                    if (piece.ImageOffset != imageOffset)
                    {
                        continue;
                    }

                    references.Add(new EditableLargeDisplaySpriteReference(part, componentIndex, piece, asset.Pieces.Count));
                }
            }
        }

        return references;
    }

    private EditableLargeDisplaySpriteReference? GetSpecificLargeDisplaySpriteReference(int medabotId, int representativePartId, int componentIndex, int imageOffset, int recordOffset, bool editable)
    {
        var part = _loadedParts.FirstOrDefault(entry => entry.MedabotId == medabotId && entry.Id == representativePartId);
        if (part is null)
        {
            return null;
        }

        if (editable)
        {
            var asset = GetEditableLargePartDisplayAsset(part.Id, componentIndex);
            for (var index = 0; index < asset.Pieces.Count; index++)
            {
                var piece = asset.Pieces[index];
                if (piece.ImageOffset == imageOffset)
                {
                    return new EditableLargeDisplaySpriteReference(part, componentIndex, piece, asset.Pieces.Count);
                }
            }

            return null;
        }

        var variantSelector = PartSpriteDisplayLayout.GetLargeDisplayVariantSelectorForComponent(part.Kind, componentIndex);
        var records = _imageAssetRepository.ReadLargePartDisplayDescriptorRecords(_session!.RomFile, part, variantSelector);
        var record = records.FirstOrDefault(entry => entry.RecordOffset == recordOffset && entry.ImageOffset == imageOffset);
        if (record is null)
        {
            return null;
        }

        var rawPiece = _imageAssetRepository.ReadLargePartDisplayPieceFromRecord(_session!.RomFile, record);
        var pieceCount = Math.Max(1, records.Count(entry => entry.ImageOffset > 0));
        return new EditableLargeDisplaySpriteReference(part, componentIndex, rawPiece, pieceCount);
    }

    private byte[] ResolveRootDescriptorPalette(LargePartDisplayAsset asset)
    {
        var rootPiece = asset.Pieces.FirstOrDefault(piece => piece.DescriptorId == asset.RootDescriptorId && piece.PaletteBytes.Length != 0 && !IsAllZeroPalette(piece.PaletteBytes))
            ?? asset.Pieces.FirstOrDefault(piece => piece.PaletteBytes.Length != 0 && !IsAllZeroPalette(piece.PaletteBytes));
        if (rootPiece is not null)
        {
            return rootPiece.PaletteBytes;
        }

        var finalBanks = GetFinalLargeDisplayPaletteBankMap(asset);
        return ResolveDisplayedLargeDisplayPalette(asset, finalBanks);
    }

    private LargePartDisplayPieceAsset GetParsedDescriptorPiece(SpriteBrowserNode node)
    {
        var asset = GetPreviewLargePartDisplayAsset(node.PrimaryId, node.SecondaryId);
        var piece = asset.Pieces.FirstOrDefault(entry => entry.RecordOffset == node.DataOffset)
                   ?? asset.Pieces.FirstOrDefault(entry => entry.DescriptorId == node.TertiaryId);
        return piece ?? throw new InvalidOperationException($"Could not resolve parsed descriptor at 0x{node.DataOffset:X6}.");
    }

    private LargePartDisplayDescriptorRecord GetParsedDescriptorRecord(SpriteBrowserNode node)
    {
        var stagedPiece = TryGetStagedLargeDisplayPieceByRecordOffset(node.PrimaryId, node.SecondaryId, node.DataOffset);
        if (stagedPiece is not null)
        {
            var baseRecord = GetRequiredBaseParsedDescriptorRecord(node);
            var widthDivisor = Math.Max(1, stagedPiece.SizeDivisors & 0x0F);
            var heightDivisor = Math.Max(1, stagedPiece.SizeDivisors >> 4);
            return new LargePartDisplayDescriptorRecord(
                stagedPiece.DescriptorId,
                stagedPiece.AppearanceEntryOffset,
                baseRecord.AppearanceEntryRaw,
                baseRecord.DescriptorPointerOffset,
                stagedPiece.RecordOffset,
                baseRecord.BlobPointerTableOffset,
                stagedPiece.DescriptorRecordBytes.ToArray(),
                stagedPiece.ImagePointerOffset,
                stagedPiece.PalettePointerOffset,
                stagedPiece.ImageOffset,
                stagedPiece.PaletteOffset,
                BitConverter.ToInt32(stagedPiece.DescriptorRecordBytes, 0x04),
                BitConverter.ToInt32(stagedPiece.DescriptorRecordBytes, 0x08),
                stagedPiece.X,
                stagedPiece.Y,
                stagedPiece.SiblingDescriptorId,
                stagedPiece.ChildDescriptorId,
                (byte)stagedPiece.PaletteBank,
                stagedPiece.RawWidth,
                stagedPiece.RawHeight,
                stagedPiece.SizeDivisors,
                stagedPiece.DescriptorRecordBytes[0x0C],
                stagedPiece.DescriptorRecordBytes[0x0D],
                stagedPiece.DescriptorRecordBytes[0x0E],
                stagedPiece.DescriptorRecordBytes[0x15],
                stagedPiece.DescriptorRecordBytes[0x16],
                stagedPiece.DescriptorRecordBytes[0x17],
                widthDivisor,
                heightDivisor,
                Math.Max(8, stagedPiece.RawWidth / widthDivisor),
                Math.Max(8, stagedPiece.RawHeight / heightDivisor),
                baseRecord.TableIndex,
                stagedPiece.ImageOffset > 0,
                baseRecord.SelectedVariantSelector,
                baseRecord.VariantResolutions);
        }

        return GetRequiredBaseParsedDescriptorRecord(node);
    }

    private LargePartDisplayDescriptorRecord GetRequiredBaseParsedDescriptorRecord(SpriteBrowserNode node)
    {
        var part = GetRequiredPartDefinition(node.PrimaryId);
        var variantSelector = PartSpriteDisplayLayout.GetLargeDisplayVariantSelectorForComponent(part.Kind, node.SecondaryId);
        return _imageAssetRepository.ReadLargePartDisplayDescriptorRecords(_session!.RomFile, part, variantSelector)
            .FirstOrDefault(record => record.RecordOffset == node.DataOffset)
            ?? throw new InvalidOperationException($"Could not resolve parsed descriptor record at 0x{node.DataOffset:X6}.");
    }

    private LargePartDisplayPieceAsset? TryGetStagedLargeDisplayPieceByRecordOffset(int partId, int componentIndex, int recordOffset)
    {
        var part = GetRequiredPartDefinition(partId);
        var variantSelector = PartSpriteDisplayLayout.GetLargeDisplayVariantSelectorForComponent(part.Kind, componentIndex);
        var staged = ProjectEditCollection.Find(_project, ProjectEditAdapters.LargeDisplaySprite, (partId, variantSelector));
        return staged?.Pieces.FirstOrDefault(piece => piece.RecordOffset == recordOffset);
    }

    private sealed record EditableLargeDisplaySpriteReference(PartDefinition Part, int ComponentIndex, LargePartDisplayPieceAsset Piece, int PieceCount);

    private string BuildLargeDisplayAppearanceInfo(LargePartDisplayDescriptorRecord record)
    {
        if (record.AppearanceEntryOffset <= 0)
        {
            return "Appearance raw: none" + Environment.NewLine;
        }

        var variantZero = record.VariantResolutions.FirstOrDefault(entry => entry.VariantSelector == 0);
        var variantOne = record.VariantResolutions.FirstOrDefault(entry => entry.VariantSelector == 1);

        return
            $"Appearance raw: 0x{record.AppearanceEntryRaw:X8}  |  Descriptor id: {record.DescriptorId:D2}{Environment.NewLine}" +
            $"Selector base: {variantZero?.AppearanceSelectorBase ?? 0}  |  Variant bit: {variantZero?.AppearanceVariantBit ?? 0}  |  Byte1 signed: {variantZero?.AppearanceSignedByte1 ?? 0}{Environment.NewLine}" +
            $"Resolved table index A: {variantZero?.TableIndex ?? 0}  |  Resolved table index B: {variantOne?.TableIndex ?? variantZero?.TableIndex ?? 0}{Environment.NewLine}";
    }

    private static string BuildLargeDisplayVariantResolutionInfo(LargePartDisplayDescriptorRecord record)
    {
        if (record.VariantResolutions.Count == 0)
        {
            return "Variants: none";
        }

        var lines = new List<string>();
        foreach (var variant in record.VariantResolutions.OrderBy(entry => entry.VariantSelector))
        {
            lines.Add(
                $"{(variant.VariantSelector == 0 ? "A" : "B")}: table {variant.TableIndex}  |  image {(variant.ImageOffset > 0 ? $"0x{variant.ImageOffset:X6}" : "none")}  |  palette {(variant.PaletteOffset > 0 ? $"0x{variant.PaletteOffset:X6}" : "none")}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static LargePartDisplayAsset MergeLargeDisplayAssets(LargePartDisplayAsset baseAsset, LargePartDisplayAsset overlayAsset)
    {
        var pieces = baseAsset.Pieces.ToDictionary(piece => piece.RecordOffset);
        foreach (var piece in overlayAsset.Pieces)
        {
            pieces[piece.RecordOffset] = piece;
        }

        return baseAsset with
        {
            Pieces = pieces.Values.OrderBy(piece => piece.DescriptorId).ThenBy(piece => piece.RecordOffset).ToArray(),
            RootDescriptorId = overlayAsset.RootDescriptorId != 0 ? overlayAsset.RootDescriptorId : baseAsset.RootDescriptorId,
            RootRecordOffset = overlayAsset.RootRecordOffset != 0 ? overlayAsset.RootRecordOffset : baseAsset.RootRecordOffset,
            InitialPaletteBanks = overlayAsset.InitialPaletteBanks.Count > 0 ? overlayAsset.InitialPaletteBanks : baseAsset.InitialPaletteBanks
        };
    }

    private static SpriteAsset CloneSpriteAsset(SpriteAsset asset) =>
        asset with
        {
            Image = new IndexedImage(asset.Image.TileWidth, asset.Image.TileHeight, asset.Image.PixelIndices.ToArray(), asset.Image.PaletteBytes.ToArray())
        };

    private static PortraitAsset ClonePortraitAsset(PortraitAsset asset) =>
        asset with
        {
            Image = new IndexedImage(asset.Image.TileWidth, asset.Image.TileHeight, asset.Image.PixelIndices.ToArray(), asset.Image.PaletteBytes.ToArray())
        };

    private static BattleCompositeSpriteComponentAsset CloneBattleCompositeSpriteComponentAsset(BattleCompositeSpriteComponentAsset asset) =>
        asset with
        {
            Image = new IndexedImage(asset.Image.TileWidth, asset.Image.TileHeight, asset.Image.PixelIndices.ToArray(), asset.Image.PaletteBytes.ToArray())
        };

    private static LargePartDisplayAsset CloneLargePartDisplayAsset(LargePartDisplayAsset asset) =>
        asset with
        {
            Pieces = asset.Pieces
                .Select(piece => piece with
                {
                    DescriptorRecordBytes = piece.DescriptorRecordBytes.ToArray(),
                    PaletteBytes = piece.PaletteBytes.ToArray(),
                    Image = new IndexedImage(piece.Image.TileWidth, piece.Image.TileHeight, piece.Image.PixelIndices.ToArray(), piece.Image.PaletteBytes.ToArray())
                })
                .ToArray(),
            InitialPaletteBanks = asset.InitialPaletteBanks.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray())
        };

    private void ApplyToolToIndexedImage(IndexedImage image, int pixelX, int pixelY)
    {
        var pixelIndex = GetTileOrderedPixelIndex(image, pixelX, pixelY);
        if (pixelIndex < 0 || pixelIndex >= image.PixelIndices.Length)
        {
            return;
        }

        switch (_selectedSpriteEditorTool)
        {
            case SpriteEditorTool.Pencil:
                if (image.PixelIndices[pixelIndex] != (byte)_selectedPaletteIndex)
                {
                    CaptureUndoSnapshotForCurrentStroke(image);
                    image.PixelIndices[pixelIndex] = (byte)_selectedPaletteIndex;
                }
                break;
            case SpriteEditorTool.Eraser:
                if (image.PixelIndices[pixelIndex] != 0)
                {
                    CaptureUndoSnapshotForCurrentStroke(image);
                    image.PixelIndices[pixelIndex] = 0;
                }
                break;
            case SpriteEditorTool.Picker:
                _selectedPaletteIndex = image.PixelIndices[pixelIndex];
                UpdateSelectedPaletteSwatch();
                break;
        }
    }

    private void ApplyToolToLargeDisplayAsset(LargePartDisplayAsset asset, int pieceIndex, int pixelX, int pixelY)
    {
        var image = asset.Pieces[pieceIndex].Image;
        var pixelIndex = GetTileOrderedPixelIndex(image, pixelX, pixelY);
        if (pixelIndex < 0 || pixelIndex >= image.PixelIndices.Length)
        {
            return;
        }

        switch (_selectedSpriteEditorTool)
        {
            case SpriteEditorTool.Pencil:
                if (image.PixelIndices[pixelIndex] != (byte)_selectedPaletteIndex)
                {
                    CaptureUndoSnapshotForCurrentStroke(asset);
                    image.PixelIndices[pixelIndex] = (byte)_selectedPaletteIndex;
                }
                break;
            case SpriteEditorTool.Eraser:
                if (image.PixelIndices[pixelIndex] != 0)
                {
                    CaptureUndoSnapshotForCurrentStroke(asset);
                    image.PixelIndices[pixelIndex] = 0;
                }
                break;
            case SpriteEditorTool.Picker:
                _selectedPaletteIndex = image.PixelIndices[pixelIndex];
                UpdateSelectedPaletteSwatch();
                break;
        }
    }

    private void CaptureUndoSnapshotForCurrentStroke(IndexedImage image)
    {
        if (_hasCapturedUndoForCurrentStroke)
        {
            return;
        }

        PushUndoSnapshot(GetSelectedSpriteHistoryKey(), image);
        _hasCapturedUndoForCurrentStroke = true;
    }

    private void CaptureUndoSnapshotForCurrentStroke(LargePartDisplayAsset asset)
    {
        if (_hasCapturedUndoForCurrentStroke)
        {
            return;
        }

        PushUndoSnapshot(GetSelectedSpriteHistoryKey(), asset);
        _hasCapturedUndoForCurrentStroke = true;
    }

    private static int GetTileOrderedPixelIndex(IndexedImage image, int pixelX, int pixelY)
    {
        if (pixelX < 0 || pixelY < 0 || pixelX >= image.Width || pixelY >= image.Height)
        {
            return -1;
        }

        var tileX = pixelX / 8;
        var tileY = pixelY / 8;
        var localX = pixelX % 8;
        var localY = pixelY % 8;
        var tileIndex = (tileY * image.TileWidth) + tileX;
        return (tileIndex * 64) + (localY * 8) + localX;
    }

    private static byte[] ConvertRasterToTileOrdered(byte[] rasterPixels, int width, int height, int tileWidth, int tileHeight)
    {
        var tileOrderedPixels = new byte[rasterPixels.Length];
        var image = new IndexedImage(tileWidth, tileHeight, tileOrderedPixels, Array.Empty<byte>());

        for (var pixelY = 0; pixelY < height; pixelY++)
        {
            for (var pixelX = 0; pixelX < width; pixelX++)
            {
                var rasterIndex = (pixelY * width) + pixelX;
                var tileIndex = GetTileOrderedPixelIndex(image, pixelX, pixelY);
                tileOrderedPixels[tileIndex] = rasterPixels[rasterIndex];
            }
        }

        return tileOrderedPixels;
    }

    private string GetSelectedSpriteHistoryKey()
    {
        if (_selectedSpriteNode is null)
        {
            throw new InvalidOperationException("No sprite or portrait is selected.");
        }

        return $"{(int)_selectedSpriteNode.AssetKind}:{_selectedSpriteNode.PrimaryId}:{_selectedSpriteNode.SecondaryId}:{_selectedSpriteNode.TertiaryId}:{_selectedSpriteNode.DataOffset}";
    }

    private void PushUndoSnapshot(string historyKey, IndexedImage image)
    {
        if (!_spriteEditHistories.TryGetValue(historyKey, out var history))
        {
            history = new SpriteEditHistory();
            _spriteEditHistories[historyKey] = history;
        }

        history.Push(image.PixelIndices, image.PaletteBytes);
    }

    private void PushUndoSnapshot(string historyKey, LargePartDisplayAsset asset)
    {
        if (!_spriteEditHistories.TryGetValue(historyKey, out var history))
        {
            history = new SpriteEditHistory();
            _spriteEditHistories[historyKey] = history;
        }

        history.Push(asset.Pieces.Select(piece => (piece.Image.PixelIndices, piece.Image.PaletteBytes)));
    }

    private async void OnUndoSpriteEditClicked(object? sender, RoutedEventArgs e)
    {
        if (_selectedSpriteNode is null)
        {
            await DisplayAlertAsync("No Asset Selected", "Select a sprite or portrait to undo.", "OK");
            return;
        }

        var historyKey = GetSelectedSpriteHistoryKey();
        if (!_spriteEditHistories.TryGetValue(historyKey, out var history) || !history.CanUndo)
        {
            SpritePatchStatusLabel.Text = "Status: nothing to undo for this asset.";
            return;
        }

        var snapshot = history.Pop();
        switch (_selectedSpriteNode.AssetKind)
        {
            case SpriteAssetKind.OverworldEventObject:
            {
                var image = GetEditableOverworldSpriteAsset(_selectedSpriteNode.PrimaryId).Image;
                Array.Copy(snapshot.Images[0].Pixels, image.PixelIndices, snapshot.Images[0].Pixels.Length);
                Array.Copy(snapshot.Images[0].Palette, image.PaletteBytes, snapshot.Images[0].Palette.Length);
                break;
            }
            case SpriteAssetKind.Portrait:
            {
                var image = GetEditablePortraitAsset(_selectedSpriteNode.PrimaryId, _selectedSpriteNode.SecondaryId).Image;
                Array.Copy(snapshot.Images[0].Pixels, image.PixelIndices, snapshot.Images[0].Pixels.Length);
                Array.Copy(snapshot.Images[0].Palette, image.PaletteBytes, snapshot.Images[0].Palette.Length);
                break;
            }
            case SpriteAssetKind.BattleCompositePartComponent:
            {
                var image = GetEditableBattleCompositeComponentAsset(_selectedSpriteNode.PrimaryId, _selectedSpriteNode.SecondaryId).Image;
                Array.Copy(snapshot.Images[0].Pixels, image.PixelIndices, snapshot.Images[0].Pixels.Length);
                Array.Copy(snapshot.Images[0].Palette, image.PaletteBytes, snapshot.Images[0].Palette.Length);
                break;
            }
            case SpriteAssetKind.MedabotLargePreview:
            case SpriteAssetKind.MedabotBattlePreview:
                return;
            case SpriteAssetKind.PartCompositeEditableSprite:
            {
                var selectedReference = GetPreviewEditableLargeDisplaySpriteReference(_selectedSpriteNode);
                foreach (var reference in GetEditableLargeDisplaySpriteReferences(_selectedSpriteNode.PrimaryId, selectedReference.Piece.ImageOffset))
                {
                    Array.Copy(snapshot.Images[0].Pixels, reference.Piece.Image.PixelIndices, snapshot.Images[0].Pixels.Length);
                    Array.Copy(snapshot.Images[0].Palette, reference.Piece.Image.PaletteBytes, snapshot.Images[0].Palette.Length);
                }
                break;
            }
            case SpriteAssetKind.PartCompositeDescriptorPiece:
            {
                var asset = GetEditableLargePartDisplayAsset(_selectedSpriteNode.PrimaryId, _selectedSpriteNode.SecondaryId);
                for (var index = 0; index < Math.Min(asset.Pieces.Count, snapshot.Images.Count); index++)
                {
                    Array.Copy(snapshot.Images[index].Pixels, asset.Pieces[index].Image.PixelIndices, snapshot.Images[index].Pixels.Length);
                    Array.Copy(snapshot.Images[index].Palette, asset.Pieces[index].Image.PaletteBytes, snapshot.Images[index].Palette.Length);
                }
                break;
            }
        }

        _hasCapturedUndoForCurrentStroke = false;
        InvalidateSelectedSpritePreview();
        SpritePatchStatusLabel.Text = "Status: reverted the last draft edit for this asset.";
    }

    private async void OnRevertSelectedSpriteChangesClicked(object? sender, RoutedEventArgs e)
    {
        if (_selectedSpriteNode is null)
        {
            await DisplayAlertAsync("No Asset Selected", "Select a sprite or portrait to reset.", "OK");
            return;
        }

        switch (_selectedSpriteNode.AssetKind)
        {
            case SpriteAssetKind.OverworldEventObject:
                _editedOverworldSpriteAssets.Remove(_selectedSpriteNode.PrimaryId);
                RemoveStagedOverworldSprite(_selectedSpriteNode.PrimaryId);
                break;
            case SpriteAssetKind.Portrait:
                _editedPortraitAssets.Remove((_selectedSpriteNode.PrimaryId, _selectedSpriteNode.SecondaryId));
                RemoveStagedPortrait(_selectedSpriteNode.PrimaryId, _selectedSpriteNode.SecondaryId);
                break;
            case SpriteAssetKind.BattleCompositePartComponent:
                _editedBattleCompositeComponentAssets.Remove((_selectedSpriteNode.PrimaryId, _selectedSpriteNode.SecondaryId));
                RemoveStagedBattleCompositeComponent(_selectedSpriteNode.PrimaryId, _selectedSpriteNode.SecondaryId);
                break;
            case SpriteAssetKind.MedabotLargePreview:
            case SpriteAssetKind.MedabotBattlePreview:
                SpritePatchStatusLabel.Text = "Status: this preview is readonly.";
                return;
            case SpriteAssetKind.PartCompositeEditableSprite:
            {
                foreach (var part in _loadedParts.Where(part => part.MedabotId == _selectedSpriteNode.PrimaryId))
                {
                    foreach (var (componentIndex, _) in PartSpriteDisplayLayout.GetPreviewComponentEntriesForPartKind(part.Kind))
                    {
                        var variantSelector = PartSpriteDisplayLayout.GetLargeDisplayVariantSelectorForComponent(part.Kind, componentIndex);
                        _editedLargePartDisplayAssets.Remove((part.Id, variantSelector));
                        RemoveStagedLargeDisplay(part.Id, variantSelector);
                    }
                }
                break;
            }
            case SpriteAssetKind.PartCompositePreview:
            case SpriteAssetKind.PartCompositeDescriptorPiece:
            case SpriteAssetKind.PartCompositeParsedDescriptor:
            {
                var part = GetRequiredPartDefinition(_selectedSpriteNode.PrimaryId);
                var variantSelector = PartSpriteDisplayLayout.GetLargeDisplayVariantSelectorForComponent(part.Kind, _selectedSpriteNode.SecondaryId);
                _editedLargePartDisplayAssets.Remove((_selectedSpriteNode.PrimaryId, variantSelector));
                RemoveStagedLargeDisplay(_selectedSpriteNode.PrimaryId, variantSelector);
                break;
            }
        }

        _spriteEditHistories.Remove(GetSelectedSpriteHistoryKey());
        _hasCapturedUndoForCurrentStroke = false;
        InvalidateSelectedSpritePreview();
        RefreshSharedSpriteConsumers();
        SpritePatchStatusLabel.Text = "Status: reset this asset back to the ROM version.";
    }

    private void OnRevertAllSpriteChangesClicked(object? sender, RoutedEventArgs e)
    {
        _editedOverworldSpriteAssets.Clear();
        _editedPortraitAssets.Clear();
        _editedBattleCompositeComponentAssets.Clear();
        _battleCompositeComponentCache.Clear();
        _editedLargePartDisplayAssets.Clear();
        _largePartDisplayAssetCache.Clear();
        _project.OverworldSpriteEdits.Clear();
        _project.PortraitEdits.Clear();
        _project.BattleCompositeSpriteEdits.Clear();
        _project.LargePartDisplayEdits.Clear();
        _spriteEditHistories.Clear();
        _spritePreviewCache.Clear();
        _hasCapturedUndoForCurrentStroke = false;

        if (_selectedSpriteNode is not null)
        {
            InvalidateSelectedSpritePreview();
        }
        else
        {
            ClearSpritePreview();
        }

        RefreshSharedSpriteConsumers();
        SpritePatchStatusLabel.Text = "Status: cleared all draft and staged sprite changes back to ROM defaults.";
    }

    private async void OnExportSpritePngClicked(object? sender, RoutedEventArgs e)
    {
        if (_selectedSpriteNode is null || !(_selectedSpriteNode?.IsAsset ?? false) || SpritePreviewImage.Source is not BitmapSource bitmap)
        {
            await DisplayAlertAsync("No Asset Selected", "Select a sprite or portrait to export.", "OK");
            return;
        }

        var title = _selectedSpriteNode.AssetKind switch
        {
            SpriteAssetKind.OverworldEventObject => $"sprite_{_selectedSpriteNode.PrimaryId:D3}.png",
            SpriteAssetKind.Portrait => $"portrait_{_selectedSpriteNode.PrimaryId:D3}_{_selectedSpriteNode.SecondaryId}.png",
            SpriteAssetKind.MapTileset => $"map_tileset_{_selectedSpriteNode.PrimaryId:D3}.png",
            SpriteAssetKind.BattleCompositePartComponent => $"battle_composite_medabot_{_selectedSpriteNode.PrimaryId:D3}_{_selectedSpriteNode.SecondaryId}.png",
            SpriteAssetKind.MedabotLargePreview => $"medabot_{_selectedSpriteNode.PrimaryId:D3}_large_preview.png",
            SpriteAssetKind.MedabotBattlePreview => $"medabot_{_selectedSpriteNode.PrimaryId:D3}_battle_preview.png",
            SpriteAssetKind.PartCompositePreview => $"part_{_selectedSpriteNode.PrimaryId:D3}_{_selectedSpriteNode.SecondaryId}.png",
            SpriteAssetKind.PartCompositeDescriptorPiece => $"part_{_selectedSpriteNode.PrimaryId:D3}_{_selectedSpriteNode.SecondaryId}_desc{_selectedSpriteNode.TertiaryId:D2}.png",
            SpriteAssetKind.PartCompositeParsedDescriptor => $"part_{_selectedSpriteNode.PrimaryId:D3}_{_selectedSpriteNode.SecondaryId}_parsed_desc{_selectedSpriteNode.TertiaryId:D2}.png",
            SpriteAssetKind.PartCompositeEditableSprite => $"medabot_{_selectedSpriteNode.PrimaryId:D3}_sprite_{_selectedSpriteNode.SharedSourcePrimaryId:X6}_record_{_selectedSpriteNode.DataOffset:X6}.png",
            _ => "asset.png"
        };
        var path = PickSaveFilePath("Export sprite PNG", "PNG image (*.png)|*.png", title);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        await using var stream = File.Create(path);
        encoder.Save(stream);
        SpritePatchStatusLabel.Text = $"Status: exported PNG to {path}";
    }

    private async void OnImportSpritePngClicked(object? sender, RoutedEventArgs e)
    {
        if (_selectedSpriteNode is null || !(_selectedSpriteNode?.IsAsset ?? false))
        {
            await DisplayAlertAsync("No Asset Selected", "Select a sprite or portrait to import.", "OK");
            return;
        }

        if (_selectedSpriteNode.AssetKind == SpriteAssetKind.MapTileset)
        {
            await DisplayAlertAsync("Read Only", "Map tileset browsing is available from the Sprites tab, but writing tileset graphics back is not wired yet.", "OK");
            return;
        }

        if (_selectedSpriteNode.AssetKind is SpriteAssetKind.MedabotLargePreview or SpriteAssetKind.MedabotBattlePreview)
        {
            await DisplayAlertAsync("Read Only", "Bot preview nodes are readonly. Edit the underlying part or battle sprites instead.", "OK");
            return;
        }

        var path = PickOpenFilePath("Import PNG", "PNG image (*.png)|*.png|All files (*.*)|*.*");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            switch (_selectedSpriteNode.AssetKind)
            {
                case SpriteAssetKind.OverworldEventObject:
                {
                    var current = GetEditableOverworldSpriteAsset(_selectedSpriteNode.PrimaryId);
                    PushUndoSnapshot(GetSelectedSpriteHistoryKey(), current.Image);
                    var updated = current with { Image = ImportIndexedImageFromPng(path, current.Image) };
                    _editedOverworldSpriteAssets[_selectedSpriteNode.PrimaryId] = updated;
                    break;
                }
                case SpriteAssetKind.Portrait:
                {
                    var current = GetEditablePortraitAsset(_selectedSpriteNode.PrimaryId, _selectedSpriteNode.SecondaryId);
                    PushUndoSnapshot(GetSelectedSpriteHistoryKey(), current.Image);
                    var updated = current with { Image = ImportIndexedImageFromPng(path, current.Image) };
                    _editedPortraitAssets[(_selectedSpriteNode.PrimaryId, _selectedSpriteNode.SecondaryId)] = updated;
                    break;
                }
                case SpriteAssetKind.BattleCompositePartComponent:
                {
                    var current = GetEditableBattleCompositeComponentAsset(_selectedSpriteNode.PrimaryId, _selectedSpriteNode.SecondaryId);
                    PushUndoSnapshot(GetSelectedSpriteHistoryKey(), current.Image);
                    var updated = current with { Image = ImportIndexedImageFromPng(path, current.Image) };
                    _editedBattleCompositeComponentAssets[(_selectedSpriteNode.PrimaryId, _selectedSpriteNode.SecondaryId)] = updated;
                    break;
                }
                case SpriteAssetKind.PartCompositeEditableSprite:
                    await DisplayAlertAsync("Import Not Supported", "Use drawing tools for unique editable sprites. PNG import is not wired for the deduplicated editable-sprite list yet.", "OK");
                    return;
                case SpriteAssetKind.PartCompositeDescriptorPiece:
                {
                    var current = GetEditableLargePartDisplayAsset(_selectedSpriteNode.PrimaryId, _selectedSpriteNode.SecondaryId);
                    var preview = GetOrBuildSpritePreviewState(_selectedSpriteNode);
                    PushUndoSnapshot(GetSelectedSpriteHistoryKey(), current);
                    _editedLargePartDisplayAssets[(_selectedSpriteNode.PrimaryId, current.VariantSelector)] = ImportLargePartDisplayFromPng(path, current, preview);
                    break;
                }
                case SpriteAssetKind.PartCompositePreview:
                case SpriteAssetKind.PartCompositeParsedDescriptor:
                case SpriteAssetKind.MedabotLargePreview:
                case SpriteAssetKind.MedabotBattlePreview:
                    await DisplayAlertAsync("Read Only", "This view is readonly. Use Editable Sprites to import changes.", "OK");
                    return;
            }

            _hasCapturedUndoForCurrentStroke = false;
            InvalidateSelectedSpritePreview();
            SpritePatchStatusLabel.Text = "Status: imported PNG into the draft version of this asset. Stage Changes to use it elsewhere.";
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Import Failed", ex.Message, "OK");
        }
    }

    private async void OnApplySpriteChangesClicked(object? sender, RoutedEventArgs e)
    {
        if (_session is null || _selectedSpriteNode is null || !_selectedSpriteNode.IsAsset)
        {
            await DisplayAlertAsync("No Asset Selected", "Select a sprite or portrait to stage.", "OK");
            return;
        }

        if (_selectedSpriteNode.AssetKind == SpriteAssetKind.MapTileset)
        {
            await DisplayAlertAsync("Read Only", "Map tileset writeback is not implemented yet.", "OK");
            return;
        }

        if (_selectedSpriteNode.AssetKind is SpriteAssetKind.MedabotLargePreview or SpriteAssetKind.MedabotBattlePreview)
        {
            await DisplayAlertAsync("Read Only", "Bot preview nodes are readonly. Stage changes from the underlying sprite assets instead.", "OK");
            return;
        }

        try
        {
            var stagedAnything = false;
            switch (_selectedSpriteNode.AssetKind)
            {
                case SpriteAssetKind.OverworldEventObject when _editedOverworldSpriteAssets.TryGetValue(_selectedSpriteNode.PrimaryId, out var editedOverworld):
                    StageOverworldSpriteEdit(_selectedSpriteNode.PrimaryId, editedOverworld);
                    stagedAnything = true;
                    break;
                case SpriteAssetKind.Portrait when _editedPortraitAssets.TryGetValue((_selectedSpriteNode.PrimaryId, _selectedSpriteNode.SecondaryId), out var editedPortrait):
                    StagePortraitEdit(_selectedSpriteNode.PrimaryId, _selectedSpriteNode.SecondaryId, editedPortrait);
                    stagedAnything = true;
                    break;
                case SpriteAssetKind.BattleCompositePartComponent when _editedBattleCompositeComponentAssets.TryGetValue((_selectedSpriteNode.PrimaryId, _selectedSpriteNode.SecondaryId), out var editedComponent):
                    StageBattleCompositeComponentEdit(_selectedSpriteNode.PrimaryId, _selectedSpriteNode.SecondaryId, editedComponent);
                    stagedAnything = true;
                    break;
                case SpriteAssetKind.PartCompositeEditableSprite:
                {
                    var selectedReference = GetPreviewEditableLargeDisplaySpriteReference(_selectedSpriteNode);
                    foreach (var part in _loadedParts.Where(part => part.MedabotId == _selectedSpriteNode.PrimaryId).OrderBy(part => part.Kind).ThenBy(part => part.Id))
                    {
                        foreach (var (componentIndex, _) in PartSpriteDisplayLayout.GetPreviewComponentEntriesForPartKind(part.Kind))
                        {
                            var asset = GetEditableLargePartDisplayAsset(part.Id, componentIndex);
                            if (!asset.Pieces.Any(piece => piece.ImageOffset == selectedReference.Piece.ImageOffset))
                            {
                                continue;
                            }

                            StageLargeDisplayEdit(part.Id, asset.VariantSelector, asset);
                            stagedAnything = true;
                        }
                    }

                    break;
                }
                case SpriteAssetKind.PartCompositeDescriptorPiece:
                {
                    var part = GetRequiredPartDefinition(_selectedSpriteNode.PrimaryId);
                    var variantSelector = PartSpriteDisplayLayout.GetLargeDisplayVariantSelectorForComponent(part.Kind, _selectedSpriteNode.SecondaryId);
                    if (_editedLargePartDisplayAssets.TryGetValue((_selectedSpriteNode.PrimaryId, variantSelector), out var editedLargeDisplay))
                    {
                        StageLargeDisplayEdit(_selectedSpriteNode.PrimaryId, variantSelector, editedLargeDisplay);
                        stagedAnything = true;
                    }

                    break;
                }
                case SpriteAssetKind.PartCompositePreview:
                case SpriteAssetKind.PartCompositeParsedDescriptor:
                case SpriteAssetKind.MedabotLargePreview:
                case SpriteAssetKind.MedabotBattlePreview:
                    SpritePatchStatusLabel.Text = "Status: this view is readonly. Stage changes from Editable Sprites instead.";
                    return;
            }

            if (!stagedAnything)
            {
                SpritePatchStatusLabel.Text = "Status: there are no draft changes to stage for this asset.";
                return;
            }

            UpdateStatus();
            _hasCapturedUndoForCurrentStroke = false;
            InvalidateSelectedSpritePreview();
            RefreshSharedSpriteConsumers();
            SpritePatchStatusLabel.Text = "Status: sprite changes are staged for export and now visible elsewhere in the editor.";
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Stage Failed", ex.Message, "OK");
        }
    }

    private async void OnApplyParsedDescriptorChangesClicked(object? sender, RoutedEventArgs e)
    {
        if (_selectedSpriteNode is null || _selectedSpriteNode.AssetKind != SpriteAssetKind.PartCompositeParsedDescriptor)
        {
            return;
        }

        try
        {
            var updatedX = int.Parse(ParsedDescriptorXEntry.Text);
            var updatedY = int.Parse(ParsedDescriptorYEntry.Text);
            var siblingId = byte.Parse(ParsedDescriptorSiblingEntry.Text);
            var childId = byte.Parse(ParsedDescriptorChildEntry.Text);
            var paletteBank = byte.Parse(ParsedDescriptorPaletteBankEntry.Text);
            var rawWidth = byte.Parse(ParsedDescriptorWidthEntry.Text);
            var rawHeight = byte.Parse(ParsedDescriptorHeightEntry.Text);
            var divisors = byte.Parse(ParsedDescriptorDivisorsEntry.Text);
            var raw0C = byte.Parse(ParsedDescriptorRaw0CEntry.Text);
            var raw0D = byte.Parse(ParsedDescriptorRaw0DEntry.Text);
            var raw0E = byte.Parse(ParsedDescriptorRaw0EEntry.Text);
            var raw15 = byte.Parse(ParsedDescriptorRaw15Entry.Text);
            var raw16 = byte.Parse(ParsedDescriptorRaw16Entry.Text);
            var raw17 = byte.Parse(ParsedDescriptorRaw17Entry.Text);

            ApplyParsedDescriptorChanges(_selectedSpriteNode.PrimaryId, _selectedSpriteNode.DataOffset, updatedX, updatedY, siblingId, childId, paletteBank, rawWidth, rawHeight, divisors, raw0C, raw0D, raw0E, raw15, raw16, raw17);
            _hasCapturedUndoForCurrentStroke = false;
            InvalidateSelectedSpritePreview();
            RefreshSharedSpriteConsumers();
            SpritePatchStatusLabel.Text = $"Status: updated parsed descriptor 0x{_selectedSpriteNode.DataOffset:X6}.";
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Descriptor Update Failed", ex.Message, "OK");
        }
    }

    private void ApplyParsedDescriptorChanges(int representativePartId, int recordOffset, int x, int y, byte siblingId, byte childId, byte paletteBank, byte rawWidth, byte rawHeight, byte sizeDivisors, byte raw0C, byte raw0D, byte raw0E, byte raw15, byte raw16, byte raw17)
    {
        var part = GetRequiredPartDefinition(representativePartId);
        var componentIndex = _selectedSpriteNode?.SecondaryId ?? 0;
        var asset = GetEditableLargePartDisplayAsset(part.Id, componentIndex);
        var pieceIndex = Array.FindIndex(asset.Pieces.ToArray(), piece => piece.RecordOffset == recordOffset);
        LargePartDisplayAsset updatedAsset;
        if (pieceIndex >= 0)
        {
            var updatedPieces = asset.Pieces.ToArray();
            updatedPieces[pieceIndex] = UpdateDescriptorPieceMetadata(updatedPieces[pieceIndex], x, y, siblingId, childId, paletteBank, rawWidth, rawHeight, sizeDivisors, raw0C, raw0D, raw0E, raw15, raw16, raw17);
            updatedAsset = asset with { Pieces = updatedPieces };
        }
        else
        {
            var record = GetParsedDescriptorRecord(_selectedSpriteNode!);
            var syntheticPiece = CreateSyntheticDescriptorOnlyPiece(record, x, y, siblingId, childId, paletteBank, rawWidth, rawHeight, sizeDivisors, raw0C, raw0D, raw0E, raw15, raw16, raw17);
            updatedAsset = asset with { Pieces = asset.Pieces.Concat([syntheticPiece]).OrderBy(piece => piece.DescriptorId).ThenBy(piece => piece.RecordOffset).ToArray() };
        }

        _editedLargePartDisplayAssets[(part.Id, asset.VariantSelector)] = updatedAsset;
        StageLargeDisplayEdit(part.Id, asset.VariantSelector, updatedAsset);
    }

    private static LargePartDisplayPieceAsset UpdateDescriptorPieceMetadata(LargePartDisplayPieceAsset piece, int x, int y, byte siblingId, byte childId, byte paletteBank, byte rawWidth, byte rawHeight, byte sizeDivisors, byte raw0C, byte raw0D, byte raw0E, byte raw15, byte raw16, byte raw17)
    {
        var descriptorBytes = piece.DescriptorRecordBytes.ToArray();
        BitConverter.GetBytes(-x).CopyTo(descriptorBytes, 0x04);
        BitConverter.GetBytes(-y).CopyTo(descriptorBytes, 0x08);
        descriptorBytes[0x0C] = raw0C;
        descriptorBytes[0x0D] = raw0D;
        descriptorBytes[0x0E] = raw0E;
        descriptorBytes[0x0F] = siblingId;
        descriptorBytes[0x10] = childId;
        descriptorBytes[0x11] = paletteBank;
        descriptorBytes[0x12] = rawWidth;
        descriptorBytes[0x13] = rawHeight;
        descriptorBytes[0x14] = sizeDivisors;
        descriptorBytes[0x15] = raw15;
        descriptorBytes[0x16] = raw16;
        descriptorBytes[0x17] = raw17;

        var widthDivisor = Math.Max(1, sizeDivisors & 0x0F);
        var heightDivisor = Math.Max(1, sizeDivisors >> 4);
        var effectiveWidth = Math.Max(8, rawWidth / widthDivisor);
        var effectiveHeight = Math.Max(8, rawHeight / heightDivisor);
        var tileWidth = Math.Max(1, effectiveWidth / 8);
        var tileHeight = Math.Max(1, effectiveHeight / 8);
        var pixelCapacity = Math.Max(piece.Image.PixelIndices.Length, tileWidth * tileHeight * 64);
        var pixelIndices = new byte[pixelCapacity];
        Array.Copy(piece.Image.PixelIndices, pixelIndices, Math.Min(piece.Image.PixelIndices.Length, pixelIndices.Length));

        return piece with
        {
            DescriptorRecordBytes = descriptorBytes,
            X = x,
            Y = y,
            SiblingDescriptorId = siblingId,
            ChildDescriptorId = childId,
            PaletteBank = paletteBank,
            RawWidth = rawWidth,
            RawHeight = rawHeight,
            SizeDivisors = sizeDivisors,
            Image = new IndexedImage(tileWidth, tileHeight, pixelIndices, piece.Image.PaletteBytes.ToArray())
        };
    }

    private static LargePartDisplayPieceAsset CreateSyntheticDescriptorOnlyPiece(LargePartDisplayDescriptorRecord record, int x, int y, byte siblingId, byte childId, byte paletteBank, byte rawWidth, byte rawHeight, byte sizeDivisors, byte raw0C, byte raw0D, byte raw0E, byte raw15, byte raw16, byte raw17)
    {
        var descriptorBytes = record.DescriptorRecordBytes.ToArray();
        BitConverter.GetBytes(-x).CopyTo(descriptorBytes, 0x04);
        BitConverter.GetBytes(-y).CopyTo(descriptorBytes, 0x08);
        descriptorBytes[0x0C] = raw0C;
        descriptorBytes[0x0D] = raw0D;
        descriptorBytes[0x0E] = raw0E;
        descriptorBytes[0x0F] = siblingId;
        descriptorBytes[0x10] = childId;
        descriptorBytes[0x11] = paletteBank;
        descriptorBytes[0x12] = rawWidth;
        descriptorBytes[0x13] = rawHeight;
        descriptorBytes[0x14] = sizeDivisors;
        descriptorBytes[0x15] = raw15;
        descriptorBytes[0x16] = raw16;
        descriptorBytes[0x17] = raw17;
        var widthDivisor = Math.Max(1, sizeDivisors & 0x0F);
        var heightDivisor = Math.Max(1, sizeDivisors >> 4);
        var tileWidth = Math.Max(1, Math.Max(8, rawWidth / widthDivisor) / 8);
        var tileHeight = Math.Max(1, Math.Max(8, rawHeight / heightDivisor) / 8);
        return new LargePartDisplayPieceAsset(
            record.DescriptorId,
            record.AppearanceEntryOffset,
            record.RecordOffset,
            descriptorBytes,
            0,
            0,
            record.ImageOffset,
            record.PaletteOffset,
            new byte[ImageAssetRepository.PaletteSize],
            paletteBank,
            x,
            y,
            siblingId,
            childId,
            rawWidth,
            rawHeight,
            sizeDivisors,
            1,
            false,
            false,
            new IndexedImage(tileWidth, tileHeight, new byte[tileWidth * tileHeight * 64], new byte[ImageAssetRepository.PaletteSize]));
    }

    private void StageOverworldSpriteEdit(int spriteId, SpriteAsset editedAsset)
    {
        ProjectEditCollection.Upsert(_project, ProjectEditAdapters.OverworldSprite, CloneSpriteAsset(editedAsset));
    }

    private void StagePortraitEdit(int characterId, int portraitIndex, PortraitAsset editedAsset)
    {
        ProjectEditCollection.Upsert(_project, ProjectEditAdapters.Portrait, ClonePortraitAsset(editedAsset));
    }

    private void StageBattleCompositeComponentEdit(int medabotId, int componentIndex, BattleCompositeSpriteComponentAsset editedAsset)
    {
        ProjectEditCollection.Upsert(_project, ProjectEditAdapters.BattleCompositeSprite, CloneBattleCompositeSpriteComponentAsset(editedAsset));
    }

    private void StageLargeDisplayEdit(int partId, int variantSelector, LargePartDisplayAsset editedAsset)
    {
        var cloned = CloneLargePartDisplayAsset(editedAsset);
        var existing = ProjectEditCollection.Find(_project, ProjectEditAdapters.LargeDisplaySprite, (partId, variantSelector));
        ProjectEditCollection.Upsert(_project, ProjectEditAdapters.LargeDisplaySprite, existing is null ? cloned : MergeLargeDisplayAssets(existing, cloned));
    }

    private void RemoveStagedOverworldSprite(int spriteId)
    {
        ProjectEditCollection.Remove(_project, ProjectEditAdapters.OverworldSprite, spriteId);
    }

    private void RemoveStagedPortrait(int characterId, int portraitIndex)
    {
        ProjectEditCollection.Remove(_project, ProjectEditAdapters.Portrait, (characterId, portraitIndex));
    }

    private void RemoveStagedBattleCompositeComponent(int medabotId, int componentIndex)
    {
        ProjectEditCollection.Remove(_project, ProjectEditAdapters.BattleCompositeSprite, (medabotId, componentIndex));
    }

    private void RemoveStagedLargeDisplay(int partId, int variantSelector)
    {
        ProjectEditCollection.Remove(_project, ProjectEditAdapters.LargeDisplaySprite, (partId, variantSelector));
    }

    private void InvalidateSelectedSpritePreview()
    {
        if (_selectedSpriteNode is null)
        {
            return;
        }

        _spritePreviewCache.Clear();
        OnSpriteSelectionChanged(SpriteTreeView, new RoutedPropertyChangedEventArgs<object>(_selectedSpriteNode, _selectedSpriteNode));
    }

    private void RefreshSharedSpriteConsumers()
    {
        _spritePreviewCache.Clear();
        _battleCompositeComponentCache.Clear();
        _largePartDisplayAssetCache.Clear();
        RefreshBattleLoadoutOptions();
        RefreshBattleBotSummariesFromSelections();
        RefreshBattleDerivedLabels();

        if (_loadedMapTileset is not null)
        {
            _mapSpriteSlotEditorMapId = null;
            UpdateMapMetadataEditor();
            UpdateMapOverlayStatus();
            RefreshMapCompositePreview();
            UpdateMapEditorSidebar();
        }
    }

    private void ApplyLargeDisplayEdits(RomHackSession session, PartDefinition part, int componentIndex, LargePartDisplayAsset editedAsset)
    {
        _imageAssetPatcher.ApplyLargePartDisplaySmart(session, editedAsset);

        if (part.Kind is not PartKind.RightArm and not PartKind.LeftArm)
        {
            return;
        }

        if (ShouldForceSplitLargeDisplay(part.Id) || !AreLargeDisplayVariantsIdentical(part))
        {
            return;
        }

        var currentVariantSelector = PartSpriteDisplayLayout.GetLargeDisplayVariantSelectorForComponent(part.Kind, componentIndex);
        var mirroredVariantSelector = currentVariantSelector ^ 1;
        var mirroredAsset = _imageAssetRepository.ReadLargePartDisplay(session.RomFile, part, mirroredVariantSelector);
        if (mirroredAsset.Pieces.Count != editedAsset.Pieces.Count)
        {
            return;
        }

        var mirroredEditedAsset = mirroredAsset with
        {
            Pieces = mirroredAsset.Pieces
                .Select((piece, index) => piece with
                {
                    PaletteBytes = editedAsset.Pieces[index].PaletteBytes.ToArray(),
                    Image = new IndexedImage(
                        piece.Image.TileWidth,
                        piece.Image.TileHeight,
                        editedAsset.Pieces[index].Image.PixelIndices.ToArray(),
                        editedAsset.Pieces[index].Image.PaletteBytes.ToArray())
                })
                .ToArray()
        };

        _imageAssetPatcher.ApplyLargePartDisplaySmart(session, mirroredEditedAsset);
        _largePartDisplayAssetCache.Remove((part.Id, mirroredVariantSelector));
        _editedLargePartDisplayAssets.Remove((part.Id, mirroredVariantSelector));
    }

    private static IndexedImage ImportIndexedImageFromPng(string path, IndexedImage referenceImage)
    {
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var source = decoder.Frames.FirstOrDefault() ?? throw new InvalidOperationException("The PNG did not contain a readable frame.");

        var targetWidth = referenceImage.Width;
        var targetHeight = referenceImage.Height;
        if (source.PixelWidth != targetWidth || source.PixelHeight != targetHeight)
        {
            throw new InvalidOperationException($"Imported image must be exactly {targetWidth}x{targetHeight} pixels.");
        }

        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var pixels = new byte[targetWidth * targetHeight * 4];
        converted.CopyPixels(pixels, targetWidth * 4, 0);

        var swatches = BuildPaletteSwatches(referenceImage.PaletteBytes);
        var rasterPixels = new byte[targetWidth * targetHeight];
        for (var i = 0; i < rasterPixels.Length; i++)
        {
            var pixelOffset = i * 4;
            var blue = pixels[pixelOffset + 0];
            var green = pixels[pixelOffset + 1];
            var red = pixels[pixelOffset + 2];
            var alpha = pixels[pixelOffset + 3];
            if (alpha < 0x80)
            {
                rasterPixels[i] = 0;
                continue;
            }

            rasterPixels[i] = FindNearestPaletteIndex(swatches, WpfColor.FromRgb(red, green, blue));
        }

        var indexedPixels = ConvertRasterToTileOrdered(rasterPixels, targetWidth, targetHeight, referenceImage.TileWidth, referenceImage.TileHeight);
        return new IndexedImage(referenceImage.TileWidth, referenceImage.TileHeight, indexedPixels, referenceImage.PaletteBytes.ToArray());
    }

    private LargePartDisplayAsset ImportLargePartDisplayFromPng(string path, LargePartDisplayAsset referenceAsset, SpritePreviewState preview)
    {
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var source = decoder.Frames.FirstOrDefault() ?? throw new InvalidOperationException("The PNG did not contain a readable frame.");
        if (source.PixelWidth != preview.Bitmap.PixelWidth || source.PixelHeight != preview.Bitmap.PixelHeight)
        {
            throw new InvalidOperationException($"Imported image must be exactly {preview.Bitmap.PixelWidth}x{preview.Bitmap.PixelHeight} pixels.");
        }

        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var pixels = new byte[source.PixelWidth * source.PixelHeight * 4];
        converted.CopyPixels(pixels, source.PixelWidth * 4, 0);

        var updatedPieces = referenceAsset.Pieces
                .Select(piece => piece with
                {
                    DescriptorRecordBytes = piece.DescriptorRecordBytes.ToArray(),
                    PaletteBytes = piece.PaletteBytes.ToArray(),
                    Image = new IndexedImage(piece.Image.TileWidth, piece.Image.TileHeight, piece.Image.PixelIndices.ToArray(), piece.Image.PaletteBytes.ToArray())
                })
            .ToArray();

        if (preview.Pieces is null)
        {
            throw new InvalidOperationException("The selected Large Display preview does not expose editable piece layout information.");
        }

        foreach (var previewPiece in preview.Pieces)
        {
            var targetPiece = updatedPieces[previewPiece.PieceIndex];
            var rasterPixels = new byte[previewPiece.Image.Width * previewPiece.Image.Height];
            var swatches = BuildPaletteSwatches(previewPiece.Image.PaletteBytes);
            for (var y = 0; y < previewPiece.Image.Height; y++)
            {
                for (var x = 0; x < previewPiece.Image.Width; x++)
                {
                    var sourceX = previewPiece.X + x;
                    var sourceY = previewPiece.Y + y;
                    var pixelOffset = ((sourceY * source.PixelWidth) + sourceX) * 4;
                    var blue = pixels[pixelOffset + 0];
                    var green = pixels[pixelOffset + 1];
                    var red = pixels[pixelOffset + 2];
                    var alpha = pixels[pixelOffset + 3];
                    rasterPixels[(y * previewPiece.Image.Width) + x] = alpha < 0x80
                        ? (byte)0
                        : FindNearestPaletteIndex(swatches, WpfColor.FromRgb(red, green, blue));
                }
            }

            var indexedPixels = ConvertRasterToTileOrdered(rasterPixels, previewPiece.Image.Width, previewPiece.Image.Height, previewPiece.Image.TileWidth, previewPiece.Image.TileHeight);
            Array.Clear(targetPiece.Image.PixelIndices, 0, targetPiece.Image.PixelIndices.Length);
            Array.Copy(indexedPixels, targetPiece.Image.PixelIndices, Math.Min(targetPiece.LoadedTileCount * 64, Math.Min(indexedPixels.Length, targetPiece.Image.PixelIndices.Length)));
        }

        return referenceAsset with { Pieces = updatedPieces };
    }

    private static byte FindNearestPaletteIndex(IReadOnlyList<PaletteSwatchItem> swatches, WpfColor color)
    {
        var bestIndex = 0;
        var bestDistance = int.MaxValue;
        for (var i = 0; i < swatches.Count; i++)
        {
            var swatch = swatches[i].Color;
            var dr = swatch.R - color.R;
            var dg = swatch.G - color.G;
            var db = swatch.B - color.B;
            var distance = (dr * dr) + (dg * dg) + (db * db);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return (byte)bestIndex;
    }

}

