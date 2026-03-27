using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Windows.Input;
using Medabots.Rom.Editor;
using Medabots.Rom.Images;
using Medabots.Rom.Maps;
using Medabots.Rom.Metadata;
using Medabots.Rom.WPFEditor.Models;

namespace Medabots.Rom.WPFEditor;

public partial class MainWindow
{
    private readonly MapTilesetRepository _mapTilesetRepository = new();
    private readonly List<MapTilesetOption> _mapTilesetOptions = [];
    private readonly List<MapLayerOption> _mapLayerOptions =
    [
        new() { Key = "layer1", DisplayName = "Layer 1", IsEditable = true, HasRuntimeData = true },
        new() { Key = "layer2", DisplayName = "Layer 2", IsEditable = true, HasRuntimeData = true },
        new() { Key = "layer3", DisplayName = "Layer 3", IsEditable = true, HasRuntimeData = true },
        new() { Key = "events", DisplayName = "Events", IsEditable = true, HasRuntimeData = true },
        new() { Key = "warps", DisplayName = "Warps", IsEditable = true, HasRuntimeData = true }
    ];
    private readonly List<MapChapterFilterOption> _mapChapterFilterOptions =
    [
        new() { Key = "all", DisplayName = "All Chapters", ChapterIndex = null },
        new() { Key = "chapter0", DisplayName = "Chapter 0", ChapterIndex = 0 },
        new() { Key = "chapter1", DisplayName = "Chapter 1", ChapterIndex = 1 },
        new() { Key = "chapter2", DisplayName = "Chapter 2", ChapterIndex = 2 },
        new() { Key = "chapter3", DisplayName = "Chapter 3", ChapterIndex = 3 },
        new() { Key = "chapter4", DisplayName = "Chapter 4", ChapterIndex = 4 },
        new() { Key = "chapter5", DisplayName = "Chapter 5", ChapterIndex = 5 },
        new() { Key = "chapter6", DisplayName = "Chapter 6", ChapterIndex = 6 },
        new() { Key = "chapter7", DisplayName = "Chapter 7", ChapterIndex = 7 },
        new() { Key = "chapter8", DisplayName = "Chapter 8", ChapterIndex = 8 },
        new() { Key = "chapter9", DisplayName = "Chapter 9", ChapterIndex = 9 },
        new() { Key = "chapter10", DisplayName = "Chapter 10", ChapterIndex = 10 },
        new() { Key = "chapter11", DisplayName = "Chapter 11", ChapterIndex = 11 },
        new() { Key = "chapter12", DisplayName = "Chapter 12", ChapterIndex = 12 },
        new() { Key = "chapter13", DisplayName = "Chapter 13", ChapterIndex = 13 },
        new() { Key = "chapter14", DisplayName = "Chapter 14", ChapterIndex = 14 },
        new() { Key = "chapter15", DisplayName = "Chapter 15", ChapterIndex = 15 }
    ];
    private const int MinimumMapPreviewTileWidth = 32;
    private const double MapViewportPadding = 160d;
    private int? _selectedMapTileX;
    private int? _selectedMapTileY;
    private MapOverlayRecordItem? _selectedMapOverlayRecord;
    private bool _isPanningMapPreview;
    private System.Windows.Point _mapPanStartPoint;
    private double _mapPanStartHorizontalOffset;
    private double _mapPanStartVerticalOffset;
    private int _mapPreviewZoom = 2;

    private void OnMapSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_session is null || MapCollectionView.SelectedItem is not BrowserItem item)
        {
            ClearMapPreview();
            return;
        }

        try
        {
            if (!_mapTilesetCache.TryGetValue(item.Id, out var asset))
            {
                asset = _mapTilesetRepository.ReadMap(_session.RomFile, item.Id, _metadata.GetMapName(item.Id));
                _mapTilesetCache[item.Id] = asset;
            }

            if (!_mapOverlayCache.TryGetValue(item.Id, out var overlays))
            {
                overlays = _mapOverlayRepository.ReadMap(_session.RomFile, item.Id);
                _mapOverlayCache[item.Id] = overlays;
            }

            _loadedMapTileset = asset;
            PopulateMapPreview(asset);
        }
        catch (Exception ex)
        {
            ClearMapPreview();
            MapSummaryLabel.Text = $"Failed to load map {item.Id:D2}: {ex.Message}";
        }
    }

    private void PopulateMapPreview(MapTilesetAsset asset)
    {
        EnsureMapUiOptions();
        var maxLayerWidth = asset.Layers.Count == 0 ? asset.WidthInTiles : asset.Layers.Max(layer => layer.HeaderWidthInTiles);
        var maxLayerHeight = asset.Layers.Count == 0 ? asset.HeightInTiles : asset.Layers.Max(layer => layer.HeaderHeightInTiles);
        MapSummaryLabel.Text = $"Map {asset.MapId:D3}  {asset.Name}{Environment.NewLine}Base size: {asset.WidthInTiles}x{asset.HeightInTiles} tiles ({asset.WidthInMetaTiles}x{asset.HeightInMetaTiles} meta-tiles){Environment.NewLine}Layer size: {maxLayerWidth}x{maxLayerHeight} tiles{Environment.NewLine}Graphics @ 0x{asset.GraphicsDataOffset:X}  Palette @ 0x{asset.PaletteDataOffset:X}  Color Attr @ {(asset.ColorAttributeDataOffset >= 0 ? $"0x{asset.ColorAttributeDataOffset:X}" : "none")}";
        MapTilesetSummaryLabel.Text = "Tileset graphics are browsed from the Sprites tab under Map Tilesets. The map tab stays focused on layout and layer data.";

        _selectedMapTileX = null;
        _selectedMapTileY = null;
        _selectedMapOverlayRecord = null;
        UpdateMapOverlayStatus();
        UpdateMapEditorSidebar();
        RefreshMapCompositePreview();
    }

    private void EnsureMapUiOptions()
    {
        if (MapEditLayerComboBox is not null && MapEditLayerComboBox.ItemsSource is null)
        {
            MapEditLayerComboBox.ItemsSource = _mapLayerOptions.ToArray();
            MapEditLayerComboBox.DisplayMemberPath = nameof(MapLayerOption.DisplayName);
            MapEditLayerComboBox.SelectedIndex = 0;
        }

        if (MapEventChapterFilterComboBox is not null && MapEventChapterFilterComboBox.ItemsSource is null)
        {
            MapEventChapterFilterComboBox.ItemsSource = _mapChapterFilterOptions;
            MapEventChapterFilterComboBox.DisplayMemberPath = nameof(MapChapterFilterOption.DisplayName);
            MapEventChapterFilterComboBox.SelectedIndex = 0;
        }
    }

    private void OnMapLayerVisibilityChanged(object? sender, RoutedEventArgs e)
    {
        if (!_isWindowFullyInitialized)
        {
            return;
        }

        UpdateMapOverlayStatus();
        RefreshMapCompositePreview();
    }

    private void OnMapEditLayerSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_isWindowFullyInitialized)
        {
            return;
        }

        UpdateMapOverlayStatus();
        UpdateMapEditorSidebar();
        if (MapCompositePreviewImage?.Source is BitmapSource bitmap)
        {
            UpdateMapGridOverlay(bitmap.PixelWidth, bitmap.PixelHeight);
        }
    }

    private void OnMapEventChapterFilterChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_isWindowFullyInitialized)
        {
            return;
        }

        UpdateMapOverlayStatus();
        UpdateMapEditorSidebar();
        RefreshMapCompositePreview();
    }

    private void OnMapCompositePreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_loadedMapTileset is null || sender is not System.Windows.Controls.Image image || image.Source is not BitmapSource source)
        {
            return;
        }

        var position = e.GetPosition(image);
        var editLayerKey = (MapEditLayerComboBox?.SelectedItem as MapLayerOption)?.Key ?? "layer1";
        var coordinateDivisor = editLayerKey is "events" or "warps" ? 16 : 8;
        var overlayOffset = editLayerKey is "events" or "warps" ? GetMapOverlayPixelOffset() : default;
        var sourceX = position.X / _mapPreviewZoom;
        var sourceY = position.Y / _mapPreviewZoom;
        var adjustedX = sourceX - overlayOffset.X;
        var adjustedY = sourceY - overlayOffset.Y;
        var tileX = (int)(adjustedX / coordinateDivisor);
        var tileY = (int)(adjustedY / coordinateDivisor);
        if (tileX < 0 || tileY < 0 || sourceX < 0 || sourceY < 0 || sourceX >= source.PixelWidth || sourceY >= source.PixelHeight)
        {
            return;
        }

        _selectedMapTileX = tileX;
        _selectedMapTileY = tileY;
        _selectedMapOverlayRecord = ResolveMapOverlaySelection(editLayerKey, tileX, tileY);
        UpdateMapOverlayStatus();
        RefreshMapCompositePreview();
        UpdateMapEditorSidebar();
    }

    private void RefreshMapCompositePreview()
    {
        if (!_isWindowFullyInitialized)
        {
            return;
        }

        if (MapCompositePreviewImage is null ||
            MapLayer1VisibilityCheckBox is null ||
            MapLayer2VisibilityCheckBox is null ||
            MapLayer3VisibilityCheckBox is null)
        {
            return;
        }

        if (_loadedMapTileset is null)
        {
            MapCompositePreviewImage.Source = null;
            return;
        }

        var visibleLayers = new List<MapLayerAsset>(3);
        if (MapLayer3VisibilityCheckBox.IsChecked == true && _loadedMapTileset.Layers.Count > 2)
        {
            visibleLayers.Add(_loadedMapTileset.Layers[2]);
        }

        if (MapLayer2VisibilityCheckBox.IsChecked == true && _loadedMapTileset.Layers.Count > 1)
        {
            visibleLayers.Add(_loadedMapTileset.Layers[1]);
        }

        if (MapLayer1VisibilityCheckBox.IsChecked == true && _loadedMapTileset.Layers.Count > 0)
        {
            visibleLayers.Add(_loadedMapTileset.Layers[0]);
        }

        SetMapCompositePreviewSource(CreateCompositeMapBitmap(visibleLayers));
        DrawMapOverlays();
    }

    private void DrawMapOverlays()
    {
        if (_loadedMapTileset is null || !_mapOverlayCache.TryGetValue(_loadedMapTileset.MapId, out var overlays))
        {
            return;
        }

        if (MapCompositePreviewImage.Source is not BitmapSource source)
        {
            return;
        }

        var width = source.PixelWidth;
        var height = source.PixelHeight;
        var pixels = new byte[width * height * 4];
        source.CopyPixels(pixels, width * 4, 0);

        if (MapWarpsVisibilityCheckBox?.IsChecked == true)
        {
            foreach (var warp in overlays.Warps)
            {
                DrawOverlayMarker(pixels, width, height, warp.TileX, warp.TileY, Colors.Magenta, GetMapOverlayPixelOffset());
            }
        }

        if (MapEventsVisibilityCheckBox?.IsChecked == true)
        {
            var chapterIndex = (MapEventChapterFilterComboBox?.SelectedItem as MapChapterFilterOption)?.ChapterIndex;
            foreach (var spawn in overlays.EntitySpawns)
            {
                if (chapterIndex.HasValue && !spawn.IsVisibleInChapter(chapterIndex.Value))
                {
                    continue;
                }

                var color = spawn.IsWalkOverTrigger
                    ? Colors.OrangeRed
                    : spawn.IsFacingTriggerOrMarker
                        ? Colors.Gold
                        : Colors.DeepSkyBlue;
                var overlayOffset = GetMapOverlayPixelOffset();
                DrawOverlayMarker(pixels, width, height, spawn.TileX, spawn.TileY, color, overlayOffset);
                DrawMapSpawnSpriteOverlay(pixels, width, height, spawn);
            }
        }

        if (_selectedMapOverlayRecord is not null)
        {
            DrawSelectedOverlayHighlight(pixels, width, height, _selectedMapOverlayRecord.TileX, _selectedMapOverlayRecord.TileY, GetMapOverlayPixelOffset());
        }

        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        bitmap.Freeze();
        SetMapCompositePreviewSource(bitmap);
    }

    private static BitmapSource CreateCompositeMapBitmap(IReadOnlyList<MapLayerAsset> visibleLayers)
    {
        if (visibleLayers.Count == 0)
        {
            return BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[4], 4);
        }

        var width = Math.Max(visibleLayers.Max(layer => layer.Image.Width), MinimumMapPreviewTileWidth * 8);
        var height = visibleLayers.Max(layer => layer.Image.Height);
        var pixels = new byte[width * height * 4];

        foreach (var layer in visibleLayers)
        {
            var transparentZeroIndex = layer.LayerIndex is 0 or 1;
            BlitMapLayer(layer, width, height, pixels, transparentZeroIndex);
        }

        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        bitmap.Freeze();
        return bitmap;
    }

    private static void BlitMapLayer(MapLayerAsset layer, int bitmapWidth, int bitmapHeight, byte[] output, bool transparentZeroIndex)
    {
        var image = layer.Image;
        var palette = image.PaletteBytes;
        for (var tileY = 0; tileY < image.TileHeight; tileY++)
        {
            for (var tileX = 0; tileX < image.TileWidth; tileX++)
            {
                var entry = layer.TileEntries[(tileY * image.TileWidth) + tileX];
                var tileIndex = entry & 0x03FF;
                if (transparentZeroIndex && tileIndex == 0)
                {
                    continue;
                }

                for (var localY = 0; localY < 8; localY++)
                {
                    for (var localX = 0; localX < 8; localX++)
                    {
                        var pixelX = tileX * 8 + localX;
                        var pixelY = tileY * 8 + localY;
                        var sourceIndex = GetTileOrderedPixelIndex(image, pixelX, pixelY);
                        var paletteIndex = image.PixelIndices[sourceIndex];
                        if (transparentZeroIndex && (paletteIndex & 0x0F) == 0)
                        {
                            continue;
                        }

                        var paletteOffset = paletteIndex * 2;
                        if (paletteOffset + 1 >= palette.Length)
                        {
                            continue;
                        }

                        var rawColor = (ushort)(palette[paletteOffset] | (palette[paletteOffset + 1] << 8));
                        var color = DecodeGbaColor(rawColor);
                        var targetIndex = (pixelY * bitmapWidth + pixelX) * 4;
                        output[targetIndex] = color.B;
                        output[targetIndex + 1] = color.G;
                        output[targetIndex + 2] = color.R;
                        output[targetIndex + 3] = 0xFF;
                    }
                }
            }
        }
    }

    private void ClearMapPreview()
    {
        _loadedMapTileset = null;
        _selectedMapTileX = null;
        _selectedMapTileY = null;
        _selectedMapOverlayRecord = null;
        MapSummaryLabel.Text = "Select a map to inspect its tileset and layers.";
        MapTilesetSummaryLabel.Text = string.Empty;
        MapOverlayStatusLabel.Text = "Select a map to inspect layers and future overlays.";
        MapCompositePreviewImage.Source = null;
        MapPreviewSurface.Width = 0;
        MapPreviewSurface.Height = 0;
        MapCompositePreviewImage.Width = 0;
        MapCompositePreviewImage.Height = 0;
        MapGridCanvas.Children.Clear();
        MapGridCanvas.Width = 0;
        MapGridCanvas.Height = 0;
        MapEditorTitleLabel.Text = "Layer Editor";
        MapTileEditorPanel.Visibility = Visibility.Visible;
        MapOverlayEditorPanel.Visibility = Visibility.Collapsed;
        MapTileEditorStatusLabel.Text = "Select a map and click the composite preview to inspect tiles.";
        MapOverlayPreviewBorder.Visibility = Visibility.Collapsed;
        MapOverlayPreviewImage.Source = null;
        MapOverlayPreviewLabel.Text = string.Empty;
        MapOverlayEditorStatusLabel.Text = "Select Events or Warps as the editing layer, then click a highlighted tile in the composite preview.";
    }

    private void UpdateMapOverlayStatus()
    {
        if (MapOverlayStatusLabel is null)
        {
            return;
        }

        var editLayer = MapEditLayerComboBox?.SelectedItem as MapLayerOption;
        var selectedEditLayerName = editLayer?.DisplayName ?? "Layer 1";
        var visibleLayers = new List<string>();
        if (MapLayer1VisibilityCheckBox?.IsChecked == true)
        {
            visibleLayers.Add("Layer 1");
        }

        if (MapLayer2VisibilityCheckBox?.IsChecked == true)
        {
            visibleLayers.Add("Layer 2");
        }

        if (MapLayer3VisibilityCheckBox?.IsChecked == true)
        {
            visibleLayers.Add("Layer 3");
        }

        if (MapEventsVisibilityCheckBox?.IsChecked == true)
        {
            visibleLayers.Add("Events");
        }

        if (MapWarpsVisibilityCheckBox?.IsChecked == true)
        {
            visibleLayers.Add("Warps");
        }

        var chapterFilter = (MapEventChapterFilterComboBox?.SelectedItem as MapChapterFilterOption)?.DisplayName ?? "All Chapters";
        if (_loadedMapTileset is not null && _mapOverlayCache.TryGetValue(_loadedMapTileset.MapId, out var overlays))
        {
            var visibleEventCount = overlays.EntitySpawns.Count(spawn => (MapEventChapterFilterComboBox?.SelectedItem as MapChapterFilterOption)?.ChapterIndex is not int chapter || spawn.IsVisibleInChapter(chapter));
            var selectedOverlay = _selectedMapOverlayRecord is null ? "none" : _selectedMapOverlayRecord.DisplayName;
            MapOverlayStatusLabel.Text = $"Edit layer: {selectedEditLayerName}  |  Visible: {(visibleLayers.Count == 0 ? "none" : string.Join(", ", visibleLayers))}{Environment.NewLine}Warps: {overlays.Warps.Count}  Events/Spawns: {visibleEventCount}  |  Event chapter filter: {chapterFilter}{Environment.NewLine}Selected overlay: {selectedOverlay}";
            return;
        }

        MapOverlayStatusLabel.Text = $"Edit layer: {selectedEditLayerName}  |  Visible: {(visibleLayers.Count == 0 ? "none" : string.Join(", ", visibleLayers))}{Environment.NewLine}Event chapter filter: {chapterFilter}";
    }

    private void UpdateMapEditorSidebar()
    {
        var editLayerKey = (MapEditLayerComboBox?.SelectedItem as MapLayerOption)?.Key ?? "layer1";
        var overlayMode = editLayerKey is "events" or "warps";
        MapTileEditorPanel.Visibility = overlayMode ? Visibility.Collapsed : Visibility.Visible;
        MapOverlayEditorPanel.Visibility = overlayMode ? Visibility.Visible : Visibility.Collapsed;

        if (overlayMode)
        {
            UpdateMapOverlayEditor(editLayerKey);
        }
        else
        {
            UpdateMapTileLayerEditor(editLayerKey);
        }
    }

    private void UpdateMapTileLayerEditor(string editLayerKey)
    {
        MapEditorTitleLabel.Text = $"Tilemap Editor: {editLayerKey.Replace("layer", "Layer ")}";
        if (_loadedMapTileset is null)
        {
            MapTileEditorStatusLabel.Text = "Select a map to inspect tiles.";
            return;
        }

        var layerIndex = editLayerKey switch
        {
            "layer1" => 0,
            "layer2" => 1,
            "layer3" => 2,
            _ => 0
        };

        var layer = _loadedMapTileset.Layers.ElementAtOrDefault(layerIndex);
        if (layer is null)
        {
            MapTileEditorStatusLabel.Text = "Selected layer is missing for this map.";
            return;
        }

        if (!_selectedMapTileX.HasValue || !_selectedMapTileY.HasValue)
        {
            MapTileEditorStatusLabel.Text = $"Layer size: {layer.HeaderWidthInTiles}x{layer.HeaderHeightInTiles} tiles. Click the composite preview to inspect a tile. Tile painting/writeback will hook into this panel.";
            return;
        }

        var tileX = _selectedMapTileX.Value;
        var tileY = _selectedMapTileY.Value;
        if (tileX < 0 || tileY < 0 || tileX >= layer.Image.TileWidth || tileY >= layer.Image.TileHeight)
        {
            MapTileEditorStatusLabel.Text = $"Tile ({tileX}, {tileY}) is outside this layer.";
            return;
        }

        var tileEntry = layer.TileEntries[(tileY * layer.Image.TileWidth) + tileX];
        MapTileEditorStatusLabel.Text = $"Tile ({tileX}, {tileY}){Environment.NewLine}Entry: 0x{tileEntry:X4}{Environment.NewLine}Tile Index: {tileEntry & 0x03FF}{Environment.NewLine}HFlip: {((tileEntry & 0x0400) != 0)}  VFlip: {((tileEntry & 0x0800) != 0)}{Environment.NewLine}Palette Bank: {(tileEntry >> 12) & 0xF}";
    }

    private void UpdateMapOverlayEditor(string editLayerKey)
    {
        MapEditorTitleLabel.Text = editLayerKey == "warps" ? "Warp Editor" : "Event/Spawn Editor";
        if (_loadedMapTileset is null || !_mapOverlayCache.TryGetValue(_loadedMapTileset.MapId, out var overlays))
        {
            MapOverlayEditorStatusLabel.Text = "Select a map to inspect overlay records.";
            return;
        }

        var selected = _selectedMapOverlayRecord;

        if (selected is null)
        {
            MapOverlayPreviewBorder.Visibility = Visibility.Collapsed;
            MapOverlayPreviewImage.Source = null;
            MapOverlayPreviewLabel.Text = string.Empty;
            MapOverlayEditorStatusLabel.Text = editLayerKey == "warps"
                ? "Click a highlighted warp tile in the composite preview to inspect that warp."
                : "Click a highlighted event/spawn tile in the composite preview to inspect that record.";
            return;
        }

        if (selected.Warp is not null)
        {
            var warp = selected.Warp;
            MapOverlayPreviewBorder.Visibility = Visibility.Collapsed;
            MapOverlayPreviewImage.Source = null;
            MapOverlayPreviewLabel.Text = string.Empty;
            MapOverlayEditorStatusLabel.Text = $"Warp tile: ({warp.TileX}, {warp.TileY}){Environment.NewLine}Destination map: {warp.DestinationMapId:D3}{Environment.NewLine}Destination tile: ({warp.DestinationTileX}, {warp.DestinationTileY}){Environment.NewLine}Arrival facing: {warp.ArrivalFacing}{Environment.NewLine}Transition kind: {warp.TransitionKind}{Environment.NewLine}Unknown bytes: 0x{warp.Unknown4:X2} 0x{warp.Unknown5:X2}";
            return;
        }

        if (selected.Spawn is not null)
        {
            var spawn = selected.Spawn;
            var previewBitmap = TryBuildMapSpawnPreview(spawn, out var previewLabel);
            if (previewBitmap is not null)
            {
                MapOverlayPreviewBorder.Visibility = Visibility.Visible;
                MapOverlayPreviewImage.Source = previewBitmap;
                MapOverlayPreviewLabel.Text = previewLabel;
            }
            else
            {
                MapOverlayPreviewBorder.Visibility = Visibility.Collapsed;
                MapOverlayPreviewImage.Source = null;
                MapOverlayPreviewLabel.Text = string.Empty;
            }

            MapOverlayEditorStatusLabel.Text = $"Tile: ({spawn.TileX}, {spawn.TileY}){Environment.NewLine}Record kind: 0x{spawn.RecordKind:X}{Environment.NewLine}Event/Object id: 0x{spawn.EventOrObjectId:X3}{Environment.NewLine}Spawn group: {spawn.SpawnGroupIndex}{Environment.NewLine}Chapter mask: 0x{spawn.ChapterVisibilityMask:X4}{Environment.NewLine}Sprite/facing packed: 0x{spawn.SpriteAndFacingPacked:X2}";
        }
    }

    private BitmapSource? TryBuildMapSpawnPreview(MapEntitySpawnRecord spawn, out string label)
    {
        label = string.Empty;
        if (_session is null)
        {
            return null;
        }

        if (spawn.IsWalkOverTrigger || spawn.SpriteAndFacingPacked == 0xFF)
        {
            return null;
        }

        var inferredSpriteId = ResolveMapSpawnOverworldSheetId(spawn);
        if (inferredSpriteId < 0 || inferredSpriteId >= MedabotsRomSchema.SpriteCount)
        {
            return null;
        }

        try
        {
            var asset = GetCurrentOverworldSpriteAsset(inferredSpriteId);
            if (asset.Image.TileWidth <= 0 || asset.Image.TileHeight < 12)
            {
                return null;
            }

            var facingVariant = GetMapSpawnFacingVariant(spawn);
            var baseFrameIndex = OverworldSpriteFrameExtractor.GetFacingBaseFrameIndex(facingVariant);
            var frameImage = OverworldSpriteFrameExtractor.ExtractFacingFrame(asset.Image, facingVariant);
            var swatches = BuildPaletteSwatches(frameImage.PaletteBytes);
            label = $"Overworld sheet {inferredSpriteId:D3}  |  Facing {GetMapSpawnFacingName(facingVariant)}  |  Frame {baseFrameIndex}";
            return CreateBitmapSource(frameImage.PixelIndices, frameImage.TileWidth, swatches);
        }
        catch
        {
            return null;
        }
    }

    private void DrawMapSpawnSpriteOverlay(byte[] pixels, int bitmapWidth, int bitmapHeight, MapEntitySpawnRecord spawn)
    {
        if (_session is null)
        {
            return;
        }

        if (spawn.IsWalkOverTrigger || spawn.SpriteAndFacingPacked == 0xFF)
        {
            return;
        }

        var spriteId = ResolveMapSpawnOverworldSheetId(spawn);
        if (spriteId < 0 || spriteId >= MedabotsRomSchema.SpriteCount)
        {
            return;
        }

        try
        {
            var asset = GetCurrentOverworldSpriteAsset(spriteId);
            var facingVariant = GetMapSpawnFacingVariant(spawn);
            var frameImage = OverworldSpriteFrameExtractor.ExtractFacingFrame(asset.Image, facingVariant);
            var swatches = BuildPaletteSwatches(frameImage.PaletteBytes);
            var overlayOffset = GetMapOverlayPixelOffset();
            var destX = (int)(spawn.TileX * 16 + overlayOffset.X);
            var destY = (int)(spawn.TileY * 16 + overlayOffset.Y - Math.Max(0, frameImage.Height - 16));
            BlitIndexedImageOverlay(frameImage, bitmapWidth, bitmapHeight, destX, destY, swatches, pixels);
        }
        catch
        {
        }
    }

    private MapOverlayRecordItem? ResolveMapOverlaySelection(string editLayerKey, int tileX, int tileY)
    {
        if (_loadedMapTileset is null || !_mapOverlayCache.TryGetValue(_loadedMapTileset.MapId, out var overlays))
        {
            return null;
        }

        if (editLayerKey == "warps")
        {
            var warp = overlays.Warps.FirstOrDefault(record => record.TileX == tileX && record.TileY == tileY);
            return warp is null
                ? null
                : new MapOverlayRecordItem
                {
                    Key = $"warp:{warp.TileX}:{warp.TileY}",
                    TileX = warp.TileX,
                    TileY = warp.TileY,
                    Warp = warp,
                    DisplayName = $"({warp.TileX}, {warp.TileY}) -> Map {warp.DestinationMapId:D3}",
                    Description = $"Dest ({warp.DestinationTileX}, {warp.DestinationTileY})  Facing {warp.ArrivalFacing}  Transition {warp.TransitionKind}"
                };
        }

        var chapterIndex = (MapEventChapterFilterComboBox?.SelectedItem as MapChapterFilterOption)?.ChapterIndex;
        var spawn = overlays.EntitySpawns.FirstOrDefault(record =>
            record.TileX == tileX &&
            record.TileY == tileY &&
            (!chapterIndex.HasValue || record.IsVisibleInChapter(chapterIndex.Value)));

        return spawn is null
            ? null
            : new MapOverlayRecordItem
            {
                Key = $"spawn:{spawn.TileX}:{spawn.TileY}:{spawn.RecordKindAndEventId:X4}",
                TileX = spawn.TileX,
                TileY = spawn.TileY,
                Spawn = spawn,
                DisplayName = $"({spawn.TileX}, {spawn.TileY})  Kind {spawn.RecordKind:X}  Id {spawn.EventOrObjectId:X3}",
                Description = $"Group {spawn.SpawnGroupIndex}  ChapterMask 0x{spawn.ChapterVisibilityMask:X4}  SpriteFacing 0x{spawn.SpriteAndFacingPacked:X2}"
            };
    }

    private static void DrawOverlayMarker(byte[] pixels, int bitmapWidth, int bitmapHeight, int tileX, int tileY, System.Windows.Media.Color color, System.Windows.Point offset)
    {
        var startX = tileX * 16 + (int)offset.X;
        var startY = tileY * 16 + (int)offset.Y;
        for (var y = 0; y < 16; y++)
        {
            var pixelY = startY + y;
            if ((uint)pixelY >= (uint)bitmapHeight)
            {
                continue;
            }

            for (var x = 0; x < 16; x++)
            {
                var pixelX = startX + x;
                if ((uint)pixelX >= (uint)bitmapWidth)
                {
                    continue;
                }

                var isBorder = x == 0 || y == 0 || x == 15 || y == 15;
                var index = (pixelY * bitmapWidth + pixelX) * 4;
                pixels[index] = isBorder ? color.B : (byte)((pixels[index] + color.B) / 2);
                pixels[index + 1] = isBorder ? color.G : (byte)((pixels[index + 1] + color.G) / 2);
                pixels[index + 2] = isBorder ? color.R : (byte)((pixels[index + 2] + color.R) / 2);
                pixels[index + 3] = 0xFF;
            }
        }
    }

    private static void DrawSelectedOverlayHighlight(byte[] pixels, int bitmapWidth, int bitmapHeight, int tileX, int tileY, System.Windows.Point offset)
    {
        var startX = tileX * 16 + (int)offset.X;
        var startY = tileY * 16 + (int)offset.Y;
        for (var y = -1; y < 17; y++)
        {
            var pixelY = startY + y;
            if ((uint)pixelY >= (uint)bitmapHeight)
            {
                continue;
            }

            for (var x = -1; x < 17; x++)
            {
                var pixelX = startX + x;
                if ((uint)pixelX >= (uint)bitmapWidth)
                {
                    continue;
                }

                var outerBorder = x == -1 || y == -1 || x == 16 || y == 16;
                var innerBorder = x == 0 || y == 0 || x == 15 || y == 15;
                if (!outerBorder && !innerBorder)
                {
                    continue;
                }

                var index = (pixelY * bitmapWidth + pixelX) * 4;
                var color = outerBorder ? Colors.Black : Colors.White;
                pixels[index] = color.B;
                pixels[index + 1] = color.G;
                pixels[index + 2] = color.R;
                pixels[index + 3] = 0xFF;
            }
        }
    }

    private int ResolveMapSpawnOverworldSheetId(MapEntitySpawnRecord spawn)
    {
        if (_session is null || _loadedMapTileset is null)
        {
            return -1;
        }

        var resourceIndex = (spawn.SpriteAndFacingPacked >> 4) & 0x0F;
        var pointerOffset = MedabotsRomSchema.MapEventObjectResourcePointerTableOffset + (_loadedMapTileset.MapId * sizeof(uint));
        if (!GbaPointer.TryReadFileOffset(_session.RomFile.Data, pointerOffset, out var resourceTableOffset))
        {
            return -1;
        }

        var resourceOffset = resourceTableOffset + resourceIndex;
        if ((uint)resourceOffset >= (uint)_session.RomFile.Data.Length)
        {
            return -1;
        }

        var sheetId = _session.RomFile.Data[resourceOffset];
        if (sheetId != 0xFF)
        {
            return sheetId;
        }

        return resourceIndex < MedabotsRomSchema.SpriteCount ? resourceIndex : -1;
    }

    private static int GetMapSpawnFacingVariant(MapEntitySpawnRecord spawn) => spawn.SpriteAndFacingPacked & 0x03;

    private System.Windows.Point GetMapOverlayPixelOffset()
    {
        if (_loadedMapTileset is null)
        {
            return default;
        }

        var compactWidthInMetaTiles = _loadedMapTileset.WidthInTiles / 2;
        var compactHeightInMetaTiles = _loadedMapTileset.HeightInTiles / 2;
        var offsetX = compactWidthInMetaTiles < 15 ? (15 - compactWidthInMetaTiles) * 8 : 0;
        var offsetY = compactHeightInMetaTiles < 10 ? (10 - compactHeightInMetaTiles) * 8 : 0;
        return new System.Windows.Point(offsetX, offsetY);
    }

    private static string GetMapSpawnFacingName(int facingVariant) => facingVariant switch
    {
        0 => "Up",
        1 => "Down",
        2 => "Left",
        3 => "Right",
        _ => $"Unknown ({facingVariant})"
    };

    private void SetMapCompositePreviewSource(BitmapSource bitmap)
    {
        MapCompositePreviewImage.Source = bitmap;
        UpdateMapPreviewLayout(bitmap.PixelWidth, bitmap.PixelHeight);
        UpdateMapGridOverlay(bitmap.PixelWidth, bitmap.PixelHeight);
    }

    private void OnMapPreviewViewportSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (MapCompositePreviewImage?.Source is BitmapSource bitmap)
        {
            UpdateMapPreviewLayout(bitmap.PixelWidth, bitmap.PixelHeight);
            UpdateMapGridOverlay(bitmap.PixelWidth, bitmap.PixelHeight);
        }
    }

    private void OnMapPreviewMouseWheel(object? sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
        {
            return;
        }

        var delta = e.Delta > 0 ? 1 : -1;
        SetMapZoom(_mapPreviewZoom + delta, scrollViewer, e.GetPosition(scrollViewer));
        e.Handled = true;
    }

    private void OnMapPreviewScrollViewerMouseDown(object? sender, MouseButtonEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer || e.ChangedButton != MouseButton.Middle)
        {
            return;
        }

        _isPanningMapPreview = true;
        _mapPanStartPoint = e.GetPosition(scrollViewer);
        _mapPanStartHorizontalOffset = scrollViewer.HorizontalOffset;
        _mapPanStartVerticalOffset = scrollViewer.VerticalOffset;
        scrollViewer.Cursor = System.Windows.Input.Cursors.SizeAll;
        scrollViewer.CaptureMouse();
        e.Handled = true;
    }

    private void OnMapPreviewScrollViewerMouseMove(object? sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isPanningMapPreview || sender is not ScrollViewer scrollViewer)
        {
            return;
        }

        var point = e.GetPosition(scrollViewer);
        var deltaX = point.X - _mapPanStartPoint.X;
        var deltaY = point.Y - _mapPanStartPoint.Y;
        scrollViewer.ScrollToHorizontalOffset(Math.Max(0, _mapPanStartHorizontalOffset - deltaX));
        scrollViewer.ScrollToVerticalOffset(Math.Max(0, _mapPanStartVerticalOffset - deltaY));
        e.Handled = true;
    }

    private void OnMapPreviewScrollViewerMouseUp(object? sender, MouseButtonEventArgs e)
    {
        if (!_isPanningMapPreview || sender is not ScrollViewer scrollViewer || e.ChangedButton != MouseButton.Middle)
        {
            return;
        }

        _isPanningMapPreview = false;
        scrollViewer.Cursor = System.Windows.Input.Cursors.Arrow;
        scrollViewer.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void SetMapZoom(int zoom, ScrollViewer? scrollViewer = null, System.Windows.Point? pointer = null)
    {
        var clamped = Math.Clamp(zoom, 1, 12);
        if (clamped == _mapPreviewZoom || MapCompositePreviewImage?.Source is not BitmapSource bitmap)
        {
            return;
        }

        var previousZoom = _mapPreviewZoom;
        _mapPreviewZoom = clamped;
        MapZoomValueLabel.Text = $"Zoom {_mapPreviewZoom}x";
        UpdateMapPreviewLayout(bitmap.PixelWidth, bitmap.PixelHeight);
        UpdateMapGridOverlay(bitmap.PixelWidth, bitmap.PixelHeight);

        if (scrollViewer is not null && pointer.HasValue && previousZoom > 0)
        {
            var anchorPoint = pointer.Value;
            var contentX = (scrollViewer.HorizontalOffset + anchorPoint.X - MapCompositePreviewImage.Margin.Left) / previousZoom;
            var contentY = (scrollViewer.VerticalOffset + anchorPoint.Y - MapCompositePreviewImage.Margin.Top) / previousZoom;
            var targetHorizontalOffset = (MapCompositePreviewImage.Margin.Left + (contentX * _mapPreviewZoom)) - anchorPoint.X;
            var targetVerticalOffset = (MapCompositePreviewImage.Margin.Top + (contentY * _mapPreviewZoom)) - anchorPoint.Y;
            scrollViewer.ScrollToHorizontalOffset(Math.Max(0, targetHorizontalOffset));
            scrollViewer.ScrollToVerticalOffset(Math.Max(0, targetVerticalOffset));
        }
    }

    private void UpdateMapPreviewLayout(int pixelWidth, int pixelHeight)
    {
        var scaledWidth = pixelWidth * _mapPreviewZoom;
        var scaledHeight = pixelHeight * _mapPreviewZoom;
        MapCompositePreviewImage.Width = scaledWidth;
        MapCompositePreviewImage.Height = scaledHeight;
        MapGridCanvas.Width = scaledWidth;
        MapGridCanvas.Height = scaledHeight;

        var viewportWidth = Math.Max(0d, MapPreviewScrollViewer?.ViewportWidth ?? 0d);
        var viewportHeight = Math.Max(0d, MapPreviewScrollViewer?.ViewportHeight ?? 0d);
        var surfaceWidth = Math.Max(scaledWidth + (MapViewportPadding * 2), viewportWidth + (MapViewportPadding * 2));
        var surfaceHeight = Math.Max(scaledHeight + (MapViewportPadding * 2), viewportHeight + (MapViewportPadding * 2));
        var offsetX = Math.Max(MapViewportPadding, (surfaceWidth - scaledWidth) / 2d);
        var offsetY = Math.Max(MapViewportPadding, (surfaceHeight - scaledHeight) / 2d);

        MapPreviewSurface.Width = surfaceWidth;
        MapPreviewSurface.Height = surfaceHeight;
        MapCompositePreviewImage.Margin = new Thickness(offsetX, offsetY, 0, 0);
        MapGridCanvas.Margin = new Thickness(offsetX, offsetY, 0, 0);
    }

    private void UpdateMapGridOverlay(int pixelWidth, int pixelHeight)
    {
        MapGridCanvas.Children.Clear();
        MapGridCanvas.Width = pixelWidth * _mapPreviewZoom;
        MapGridCanvas.Height = pixelHeight * _mapPreviewZoom;

        if (_mapPreviewZoom < 4)
        {
            return;
        }

        var editLayerKey = (MapEditLayerComboBox?.SelectedItem as MapLayerOption)?.Key ?? "layer1";
        var cellSize = editLayerKey is "events" or "warps" ? 16 : 8;
        var gridBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(80, 107, 114, 128));

        for (var x = 0; x <= pixelWidth; x += cellSize)
        {
            MapGridCanvas.Children.Add(new System.Windows.Shapes.Line
            {
                X1 = x * _mapPreviewZoom,
                Y1 = 0,
                X2 = x * _mapPreviewZoom,
                Y2 = pixelHeight * _mapPreviewZoom,
                Stroke = gridBrush,
                StrokeThickness = 1.0
            });
        }

        for (var y = 0; y <= pixelHeight; y += cellSize)
        {
            MapGridCanvas.Children.Add(new System.Windows.Shapes.Line
            {
                X1 = 0,
                Y1 = y * _mapPreviewZoom,
                X2 = pixelWidth * _mapPreviewZoom,
                Y2 = y * _mapPreviewZoom,
                Stroke = gridBrush,
                StrokeThickness = 1.0
            });
        }
    }

    private static void BlitIndexedImageOverlay(IndexedImage image, int bitmapWidth, int bitmapHeight, int destX, int destY, IReadOnlyList<PaletteSwatchItem> swatches, byte[] output)
    {
        for (var tileY = 0; tileY < image.TileHeight; tileY++)
        {
            for (var tileX = 0; tileX < image.TileWidth; tileX++)
            {
                var tileIndex = (tileY * image.TileWidth) + tileX;
                BlitTileOverlay(image.PixelIndices, tileIndex, bitmapWidth, bitmapHeight, destX + (tileX * 8), destY + (tileY * 8), swatches, output);
            }
        }
    }

    private static void BlitTileOverlay(byte[] pixelIndices, int tileIndex, int bitmapWidth, int bitmapHeight, int destX, int destY, IReadOnlyList<PaletteSwatchItem> swatches, byte[] output)
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

                var colorIndex = pixelIndices[sourceIndex];
                if (colorIndex == 0)
                {
                    continue;
                }

                var pixelX = destX + localX;
                var pixelY = destY + localY;
                if (pixelX < 0 || pixelY < 0 || pixelX >= bitmapWidth || pixelY >= bitmapHeight)
                {
                    continue;
                }

                var color = colorIndex < swatches.Count ? swatches[colorIndex].Color : Colors.Transparent;
                var outputIndex = ((pixelY * bitmapWidth) + pixelX) * 4;
                output[outputIndex + 0] = color.B;
                output[outputIndex + 1] = color.G;
                output[outputIndex + 2] = color.R;
                output[outputIndex + 3] = 0xFF;
            }
        }
    }
}
