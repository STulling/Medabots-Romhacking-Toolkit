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
        SpritePaletteFamilyEditorPanel.Visibility = Visibility.Collapsed;
        SpritePaletteFamilyComboBox.SelectedItem = null;
        SpritePaletteFamilyHintLabel.Text = string.Empty;
        SpritePatchStatusLabel.Text = string.Empty;
        _selectedPaletteIndex = 1;
        _hasCapturedUndoForCurrentStroke = false;
    }

    private List<SpriteBrowserNode> BuildSpriteTreeNodes() =>
        new SpriteBrowserTreeBuilder(_session?.RomFile, _loadedParts, _metadata, _project, _imageAssetRepository, _mapTilesetRepository).BuildTreeNodes();

    private bool ShouldForceSplitLargeDisplay(int partId)
    {
        return _project.SplitLargeDisplayPartIds.Contains(partId);
    }

    private bool AreLargeDisplayVariantsIdentical(PartDefinition part) =>
        new SpriteBrowserTreeBuilder(_session?.RomFile, _loadedParts, _metadata, _project, _imageAssetRepository).AreLargeDisplayVariantsIdentical(part);

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
        $"{node.AssetKind}:{node.PrimaryId}:{node.SecondaryId}:{node.Title}";

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
            SpriteAssetKind.PartCompositePreview => BuildPartCompositePreviewState(GetRequiredPartDefinition(node.PrimaryId), node.SecondaryId),
            _ => throw new InvalidOperationException("Unsupported sprite asset kind.")
        };

        _spritePreviewCache[cacheKey] = preview;
        return preview;
    }

    private static string GetSpritePreviewCacheKey(SpriteBrowserNode node) =>
        $"{node.AssetKind}:{node.PrimaryId}:{node.SecondaryId}";

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

    private SpritePreviewState BuildPartCompositePreviewState(PartDefinition part, int variantComponentIndex)
    {
        var asset = GetPreviewLargePartDisplayAsset(part.Id, variantComponentIndex);
        var renderedPieces = BuildRenderedLargeDisplayPieces(asset, part.Kind);
        var finalBanks = GetFinalLargeDisplayPaletteBankMap(asset);
        var paletteBytes = ResolveDisplayedLargeDisplayPalette(asset, finalBanks);
        var swatches = BuildPaletteSwatches(paletteBytes);
        var bitmap = CreateLargePartDisplayBitmap(renderedPieces, finalBanks);
        var summary = $"Part {part.Id:D3}  {_metadata.GetPartName(part.Id)}{Environment.NewLine}Kind: {PartSpriteDisplayLayout.FormatPartKind(part.Kind)}{Environment.NewLine}Variant: {PartSpriteDisplayLayout.GetLargeDisplayVariantLabel(part.Kind, variantComponentIndex)}{Environment.NewLine}Medabot family: {part.MedabotId:D3}  {_metadata.GetBotName(part.MedabotId)}{Environment.NewLine}Large display: {bitmap.PixelWidth}x{bitmap.PixelHeight}px{Environment.NewLine}Root descriptor: {asset.RootDescriptorId:D2} @ 0x{asset.RootRecordOffset:X6}{Environment.NewLine}Pieces: {asset.Pieces.Count}{Environment.NewLine}First piece palette: 0x{asset.Pieces[0].PaletteOffset:X6}  |  Bank: {asset.Pieces[0].PaletteBank + 8}";
        var paletteSummary = $"Palette colors: {swatches.Count}  |  Large display uses staged OBJ palette banks from descriptor-selected pieces";
        var pieces = renderedPieces.Select((entry, index) => new SpritePreviewPiece(index, entry.X, entry.Y, entry.Image)).ToArray();
        return new SpritePreviewState(part.Id, bitmap, summary, paletteSummary, swatches, pieces);
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

    private static IReadOnlyList<(LargePartDisplayPieceAsset Piece, IndexedImage Image, int X, int Y)> BuildRenderedLargeDisplayPieces(
        LargePartDisplayAsset asset,
        PartKind kind)
    {
        var rendered = asset.Pieces
            .Select(piece => (Piece: piece, Image: GetRenderedLargeDisplayPieceImage(piece, kind, asset.Pieces.Count), X: piece.X, Y: piece.Y))
            .ToArray();

        if (asset.Pieces.Count <= 1)
        {
            return rendered.Select(entry => (entry.Piece, entry.Image, 0, 0)).ToArray();
        }

        if (kind is PartKind.RightArm or PartKind.LeftArm)
        {
            return rendered
                .OrderBy(entry => entry.Piece.X)
                .ThenBy(entry => entry.Piece.Y)
                .Select((entry, index) => (entry.Piece, entry.Image, X: 0, Y: index * entry.Image.Height))
                .ToArray();
        }

        if (kind == PartKind.Head)
        {
            const int headColumnWidth = 32;
            var y = 0;
            return rendered
                .OrderBy(entry => entry.Image.Width * entry.Image.Height)
                .ThenBy(entry => entry.Piece.DescriptorId)
                .Select(entry =>
                {
                    var placed = (entry.Piece, entry.Image, X: Math.Max(0, (headColumnWidth - entry.Image.Width) / 2), Y: y);
                    y += entry.Image.Height;
                    return placed;
                })
                .ToArray();
        }

        if (kind == PartKind.Legs)
        {
            const int maxRowWidth = 32;
            var ordered = rendered
                .OrderBy(entry => entry.Piece.Y)
                .ThenBy(entry => entry.Piece.X)
                .ToArray();
            var laidOut = new List<(LargePartDisplayPieceAsset Piece, IndexedImage Image, int X, int Y)>(ordered.Length);
            var x = 0;
            var y = 0;
            var rowHeight = 0;

            foreach (var entry in ordered)
            {
                if (x > 0 && x + entry.Image.Width > maxRowWidth)
                {
                    x = 0;
                    y += rowHeight;
                    rowHeight = 0;
                }

                laidOut.Add((entry.Piece, entry.Image, x, y));
                x += entry.Image.Width;
                rowHeight = Math.Max(rowHeight, entry.Image.Height);
            }

            return laidOut;
        }

        return rendered;
    }

    private static Dictionary<int, byte[]> GetFinalLargeDisplayPaletteBankMap(LargePartDisplayAsset asset)
    {
        var banks = asset.InitialPaletteBanks
            .Where(pair => !IsAllZeroPalette(pair.Value))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        foreach (var piece in asset.Pieces)
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
    {
        var uploadedPalette = asset.Pieces
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
        var tileWidth = kind switch
        {
            PartKind.Head when pieceCount == 1 => 4,
            PartKind.RightArm => 4,
            PartKind.LeftArm => 4,
            PartKind.Legs when pieceCount == 1 => 8,
            PartKind.Head => Math.Max(1, piece.Image.TileWidth),
            PartKind.Legs => Math.Max(1, piece.Image.TileWidth),
            _ => Math.Max(1, piece.Image.TileWidth)
        };
        var tileHeight = Math.Max(1, (int)Math.Ceiling(totalTiles / (double)tileWidth));
        var effectivePixels = new byte[tileWidth * tileHeight * 64];
        Array.Copy(piece.Image.PixelIndices, effectivePixels, Math.Min(totalTiles * 64, piece.Image.PixelIndices.Length));
        return new IndexedImage(tileWidth, tileHeight, effectivePixels, piece.Image.PaletteBytes);
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
                var color = colorIndex < swatches.Count ? swatches[colorIndex].Color : Colors.Transparent;
                var outputIndex = ((pixelY * bitmapWidth) + pixelX) * 4;
                output[outputIndex + 0] = color.B;
                output[outputIndex + 1] = color.G;
                output[outputIndex + 2] = color.R;
                output[outputIndex + 3] = colorIndex == 0 ? (byte)0 : (byte)255;
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
        var staged = ProjectEditCollection.Find(_project, ProjectEditAdapters.LargeDisplaySprite, (partId, variantSelector));
        if (staged is not null)
        {
            return staged;
        }

        if (_largePartDisplayAssetCache.TryGetValue((partId, variantSelector), out var cached))
        {
            return cached;
        }

        if (_session is null)
        {
            throw new InvalidOperationException("No ROM session is open.");
        }

        var asset = _imageAssetRepository.ReadLargePartDisplay(_session.RomFile, part, variantSelector);
        _largePartDisplayAssetCache[(partId, variantSelector)] = asset;
        return asset;
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
            SpriteAssetKind.PartCompositePreview when _editedLargePartDisplayAssets.ContainsKey((node.PrimaryId, PartSpriteDisplayLayout.GetLargeDisplayVariantSelectorForComponent(GetRequiredPartDefinition(node.PrimaryId).Kind, node.SecondaryId)))
                => HasStagedLargeDisplay(node.PrimaryId, PartSpriteDisplayLayout.GetLargeDisplayVariantSelectorForComponent(GetRequiredPartDefinition(node.PrimaryId).Kind, node.SecondaryId))
                    ? "Status: this asset has draft edits and an older staged version. Stage Changes to update the staged version."
                    : "Status: this asset has draft edits. Stage Changes to make other tabs and export use them.",
            SpriteAssetKind.OverworldEventObject when HasStagedOverworldSprite(node.PrimaryId)
                => "Status: this overworld sprite is staged. Other tabs and export use the staged version.",
            SpriteAssetKind.Portrait when HasStagedPortrait(node.PrimaryId, node.SecondaryId)
                => "Status: this portrait is staged. Other tabs and export use the staged version.",
            SpriteAssetKind.MapTileset
                => "Status: showing map tileset graphics. Editing/writeback is not wired yet; use this as the future tile picker surface.",
            SpriteAssetKind.BattleCompositePartComponent when HasStagedBattleCompositeComponent(node.PrimaryId, node.SecondaryId)
                => "Status: this Medabot component sprite is staged. Other tabs and export use the staged version.",
            SpriteAssetKind.PartCompositePreview when HasStagedLargeDisplay(node.PrimaryId, PartSpriteDisplayLayout.GetLargeDisplayVariantSelectorForComponent(GetRequiredPartDefinition(node.PrimaryId).Kind, node.SecondaryId))
                => "Status: this Large Display sprite is staged. Other tabs and export use the staged version.",
            SpriteAssetKind.BattleCompositePartComponent
                => "Status: editing a Medabot/component battle sprite family. Palette changes affect every part using that shared family palette.",
            SpriteAssetKind.PartCompositePreview
                => "Status: editing the descriptor-driven part-detail Large Display preview for this part.",
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

    private bool IsCompositePaletteFamilyEditable(SpriteBrowserNode node)
    {
        return node.AssetKind is SpriteAssetKind.BattleCompositePartComponent;
    }

    private BattleCompositeSpriteComponentAsset GetSelectedBattleCompositeComponentAsset(SpriteBrowserNode node)
    {
        return node.AssetKind switch
        {
            SpriteAssetKind.BattleCompositePartComponent => GetPreviewBattleCompositeComponentAsset(node.PrimaryId, node.SecondaryId),
            SpriteAssetKind.PartCompositePreview => GetPreviewBattleCompositeComponentAsset(GetRequiredPartDefinition(node.PrimaryId).MedabotId, node.SecondaryId),
            _ => throw new InvalidOperationException("The selected sprite node does not use a composite component asset.")
        };
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
            SpriteAssetKind.PartCompositePreview => GetEditableBattleCompositeComponentAsset(GetRequiredPartDefinition(node.PrimaryId).MedabotId, node.SecondaryId),
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
                case SpriteAssetKind.PartCompositePreview:
                    EditLargeDisplayPalette(GetEditableLargePartDisplayAsset(_selectedSpriteNode.PrimaryId, _selectedSpriteNode.SecondaryId));
                    break;
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
            {
                if (!TryResolveLargeDisplayPixel(point, out var pieceIndex, out var piecePixelX, out var piecePixelY))
                {
                    return;
                }

                var asset = GetEditableLargePartDisplayAsset(_selectedSpriteNode.PrimaryId, _selectedSpriteNode.SecondaryId);
                ApplyToolToLargeDisplayAsset(asset, pieceIndex, piecePixelX, piecePixelY);
                break;
            }
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
            SpriteAssetKind.PartCompositePreview => null,
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

        return $"{(int)_selectedSpriteNode.AssetKind}:{_selectedSpriteNode.PrimaryId}:{_selectedSpriteNode.SecondaryId}";
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
            case SpriteAssetKind.PartCompositePreview:
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
            case SpriteAssetKind.PartCompositePreview:
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
            SpriteAssetKind.PartCompositePreview => $"part_{_selectedSpriteNode.PrimaryId:D3}_{_selectedSpriteNode.SecondaryId}.png",
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
                case SpriteAssetKind.PartCompositePreview:
                {
                    var current = GetEditableLargePartDisplayAsset(_selectedSpriteNode.PrimaryId, _selectedSpriteNode.SecondaryId);
                    var preview = GetOrBuildSpritePreviewState(_selectedSpriteNode);
                    PushUndoSnapshot(GetSelectedSpriteHistoryKey(), current);
                    _editedLargePartDisplayAssets[(_selectedSpriteNode.PrimaryId, current.VariantSelector)] = ImportLargePartDisplayFromPng(path, current, preview);
                    break;
                }
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
                case SpriteAssetKind.PartCompositePreview:
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
        ProjectEditCollection.Upsert(_project, ProjectEditAdapters.LargeDisplaySprite, CloneLargePartDisplayAsset(editedAsset));
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

