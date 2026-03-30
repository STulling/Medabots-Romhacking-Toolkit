using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Windows.Input;
using Medabots.Rom.Editor;
using Medabots.Rom.Encounters;
using Medabots.Rom.Events;
using Medabots.Rom.Images;
using Medabots.Rom.Maps;
using Medabots.Rom.Metadata;
using Medabots.Rom.Projects;
using Medabots.Rom.WPFEditor.Models;

namespace Medabots.Rom.WPFEditor;

public partial class MainWindow
{
    private readonly MapTilesetRepository _mapTilesetRepository = new();
    private readonly MapEntitySpawnProjectEditor _mapEntitySpawnProjectEditor = new();
    private readonly MapWarpProjectEditor _mapWarpProjectEditor = new();
    private readonly MapCollisionProjectEditor _mapCollisionProjectEditor = new();
    private readonly MapEncounterProjectEditor _mapEncounterProjectEditor = new();
    private readonly MapEncounterStateProjectEditor _mapEncounterStateProjectEditor = new();
    private readonly MapMusicProjectEditor _mapMusicProjectEditor = new();
    private readonly MapEventObjectResourceProjectEditor _mapEventObjectResourceProjectEditor = new();
    private readonly MapMetadataProjectEditor _mapMetadataProjectEditor = new();
    private readonly EventScriptProjectEditor _eventScriptProjectEditor = new();
    private readonly List<MapTilesetOption> _mapTilesetOptions = [];
    private readonly List<BrowserItem> _mapMusicOptions = [];
    private readonly List<MapCollisionValueOption> _mapCollisionValueOptions = [];
    private readonly List<MapSpawnSpriteOption> _mapSpawnSpriteOptions = [];
    private readonly List<MapSpawnFacingOption> _mapSpawnFacingOptions = [];
    private readonly ObservableCollection<MapSpriteSlotEditorItem> _mapSpriteSlotEditorItems = [];
    private readonly List<MapLayerOption> _mapLayerOptions =
    [
        new() { Key = "metadata", DisplayName = "Metadata", IsEditable = true, HasRuntimeData = false },
        new() { Key = "layer1", DisplayName = "Layer 1", IsEditable = true, HasRuntimeData = true },
        new() { Key = "layer2", DisplayName = "Layer 2", IsEditable = true, HasRuntimeData = true },
        new() { Key = "layer3", DisplayName = "Layer 3", IsEditable = true, HasRuntimeData = true },
        new() { Key = "collision", DisplayName = "Collision", IsEditable = true, HasRuntimeData = true },
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
    private int? _hoveredMapTileX;
    private int? _hoveredMapTileY;
    private int? _selectedMapTilesetTileIndex;
    private int _selectedMapTilesetTilePaletteBank;
    private int? _selectedMapTilesetTileEndIndex;
    private byte _selectedMapCollisionValue;
    private bool _isRefreshingMapCollisionEditor;
    private bool _isRefreshingMapSpawnSelectors;
    private bool _isRefreshingMapMetadataEditor;
    private bool _isRefreshingSelectedMapSpriteSlotEditor;
    private bool _isDraggingMapTilesetSelection;
    private int _selectedMapSpriteSlotIndex;
    private int? _mapSpriteSlotEditorMapId;
    private MapOverlayRecordItem? _selectedMapOverlayRecord;
    private readonly Dictionary<int, int> _selectedMapTilesetSourceMapIds = [];
    private readonly Dictionary<int, MapTilesetAsset> _sourceMapTilesetCache = [];
    private bool _isPanningMapPreview;
    private System.Windows.Point _mapPanStartPoint;
    private double _mapPanStartHorizontalOffset;
    private double _mapPanStartVerticalOffset;
    private int _mapPreviewZoom = 2;
    private BitmapSource? _mapCompositeBaseBitmap;
    private int? _mapMetadataDraftMapId;
    private byte? _mapMetadataDraftEncounterEnabledByte;
    private byte? _mapMetadataDraftMusicId;
    private byte[]? _mapMetadataDraftEncounterBattleIds;
    private byte[]? _mapMetadataDraftEventObjectResourceIds;

    private void RefreshCurrentMapMetadataDraft()
    {
        if (_isRefreshingMapMetadataEditor || !_isWindowFullyInitialized || _loadedMapTileset is null)
        {
            return;
        }

        if (MapMusicComboBox.SelectedValue is not int musicId)
        {
            return;
        }

        var hasEncounters = MapHasEncountersCheckBox.IsChecked == true;
        var encounterBattleIds = new byte[4];
        if (MapEncounterBattle1ComboBox.SelectedValue is int battle1 &&
            MapEncounterBattle2ComboBox.SelectedValue is int battle2 &&
            MapEncounterBattle3ComboBox.SelectedValue is int battle3 &&
            MapEncounterBattle4ComboBox.SelectedValue is int battle4)
        {
            encounterBattleIds[0] = (byte)battle1;
            encounterBattleIds[1] = (byte)battle2;
            encounterBattleIds[2] = (byte)battle3;
            encounterBattleIds[3] = (byte)battle4;
        }
        else
        {
            var effectiveEncounter = _project.MapEncounterPatches.FirstOrDefault(candidate => candidate.MapId == _loadedMapTileset.MapId);
            if (effectiveEncounter is not null)
            {
                encounterBattleIds[0] = effectiveEncounter.Battle1;
                encounterBattleIds[1] = effectiveEncounter.Battle2;
                encounterBattleIds[2] = effectiveEncounter.Battle3;
                encounterBattleIds[3] = effectiveEncounter.Battle4;
            }
            else
            {
                var loadedEncounter = _loadedEncounters.FirstOrDefault(candidate => candidate.Id == _loadedMapTileset.MapId);
                if (loadedEncounter is not null)
                {
                    encounterBattleIds[0] = loadedEncounter.Battle1;
                    encounterBattleIds[1] = loadedEncounter.Battle2;
                    encounterBattleIds[2] = loadedEncounter.Battle3;
                    encounterBattleIds[3] = loadedEncounter.Battle4;
                }
            }
        }

        var spriteResourceIds = GetCurrentMapMetadataEditorSpriteResourceIds();
        _mapMetadataDraftMapId = _loadedMapTileset.MapId;
        _mapMetadataDraftEncounterEnabledByte = (byte)(hasEncounters ? 1 : 0);
        _mapMetadataDraftMusicId = (byte)musicId;
        _mapMetadataDraftEncounterBattleIds = encounterBattleIds;
        _mapMetadataDraftEventObjectResourceIds = spriteResourceIds;
        MapMetadataStatusLabel.Text = $"Draft metadata for map {_loadedMapTileset.MapId:D3}.{Environment.NewLine}Music: {_metadata.GetSongName((byte)musicId)}  |  Encounters: {(hasEncounters ? "enabled" : "disabled")}{Environment.NewLine}Use Apply Changes to stage this map metadata into the project.";
        UpdateMapOverlayStatus();
        RefreshMapCompositePreview();
        if (((MapEditLayerComboBox?.SelectedItem as MapLayerOption)?.Key ?? "layer1") != "metadata")
        {
            UpdateMapEditorSidebar();
        }
    }

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
        if (_mapMetadataDraftMapId != asset.MapId)
        {
            ClearCurrentMapMetadataDraft();
        }

        EnsureMapUiOptions();
        var maxLayerWidth = asset.Layers.Count == 0 ? asset.WidthInTiles : asset.Layers.Max(layer => layer.HeaderWidthInTiles);
        var maxLayerHeight = asset.Layers.Count == 0 ? asset.HeightInTiles : asset.Layers.Max(layer => layer.HeaderHeightInTiles);
        var musicId = GetEffectiveMapMusicId(asset);
        MapSummaryLabel.Text = $"Map {asset.MapId:D3}  {asset.Name}{Environment.NewLine}Base size: {asset.WidthInTiles}x{asset.HeightInTiles} tiles ({asset.WidthInMetaTiles}x{asset.HeightInMetaTiles} meta-tiles){Environment.NewLine}Layer size: {maxLayerWidth}x{maxLayerHeight} tiles{Environment.NewLine}Music: {_metadata.GetSongName(musicId)} ({musicId})  |  Sprite slots: {GetEffectiveMapEventObjectResourceIds(asset).Count}{Environment.NewLine}Graphics @ 0x{asset.GraphicsDataOffset:X}  Palette @ 0x{asset.PaletteDataOffset:X}{Environment.NewLine}Collision @ {(asset.CollisionDataOffset >= 0 ? $"0x{asset.CollisionDataOffset:X}" : "none")}  Color Attr @ {(asset.ColorAttributeDataOffset >= 0 ? $"0x{asset.ColorAttributeDataOffset:X}" : "none")}";
        MapTilesetSummaryLabel.Text = "Pick a tileset source, choose a tile from the palette, then paint directly on the active tilemap layer.";

        _selectedMapTileX = null;
        _selectedMapTileY = null;
        _hoveredMapTileX = null;
        _hoveredMapTileY = null;
        _selectedMapTilesetTileIndex = null;
        _selectedMapTilesetTileEndIndex = null;
        _selectedMapTilesetTilePaletteBank = 0;
        _selectedMapOverlayRecord = null;
        SynchronizeSelectedMapTilesetOption(asset);
        RefreshMapTilesetPalettePreview();
        UpdateMapMetadataEditor();
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
            MapEditLayerComboBox.SelectedIndex = 1;
        }

        if (MapEventChapterFilterComboBox is not null && MapEventChapterFilterComboBox.ItemsSource is null)
        {
            MapEventChapterFilterComboBox.ItemsSource = _mapChapterFilterOptions;
            MapEventChapterFilterComboBox.DisplayMemberPath = nameof(MapChapterFilterOption.DisplayName);
            MapEventChapterFilterComboBox.SelectedIndex = 0;
        }

        EnsureMapCollisionValueOptions();
        if (MapCollisionValueComboBox is not null && MapCollisionValueComboBox.ItemsSource is null)
        {
            MapCollisionValueComboBox.ItemsSource = _mapCollisionValueOptions;
            MapCollisionValueComboBox.DisplayMemberPath = nameof(MapCollisionValueOption.DisplayName);
            MapCollisionValueComboBox.SelectedValuePath = nameof(MapCollisionValueOption.Value);
            MapCollisionValueComboBox.SelectedValue = _selectedMapCollisionValue;
        }

        EnsureMapTilesetOptions();
        if (MapTilesetComboBox is not null && MapTilesetComboBox.ItemsSource is null)
        {
            MapTilesetComboBox.ItemsSource = _mapTilesetOptions;
        }

        EnsureMapMetadataOptions();
    }

    private void EnsureMapTilesetOptions()
    {
        if (_session is null || _mapTilesetOptions.Count > 0)
        {
            return;
        }

        var seen = new HashSet<(int GraphicsOffset, int PaletteOffset, int ColorOffset)>();
        for (var mapId = 0; mapId < MedabotsRomSchema.MapCount; mapId++)
        {
            var graphicsPointerOffset = MedabotsRomSchema.MapTilesetGraphicsPointerTableOffset + (mapId * sizeof(uint));
            var palettePointerOffset = MedabotsRomSchema.MapTilesetPalettePointerTableOffset + (mapId * sizeof(uint));
            var colorPointerOffset = MedabotsRomSchema.MapColorAttributePointerTableOffset + (mapId * sizeof(uint));
            var graphicsOffset = TryReadMapPointerOffset(graphicsPointerOffset);
            var paletteOffset = TryReadMapPointerOffset(palettePointerOffset);
            var colorOffset = TryReadMapPointerOffset(colorPointerOffset);
            if (!seen.Add((graphicsOffset, paletteOffset, colorOffset)))
            {
                continue;
            }

            _mapTilesetOptions.Add(new MapTilesetOption(
                mapId,
                graphicsOffset,
                paletteOffset,
                colorOffset,
                $"{mapId:D3}  {_metadata.GetMapName(mapId)}"));
        }
    }

    private void EnsureMapMetadataOptions()
    {
        if (_mapMusicOptions.Count == 0)
        {
            for (var songId = 0; songId < _metadata.Catalog.SongNames.Count; songId++)
            {
                _mapMusicOptions.Add(new BrowserItem(songId, $"{songId:D3}  {_metadata.GetSongName(songId)}"));
            }
        }

        if (MapMusicComboBox is not null && MapMusicComboBox.ItemsSource is null)
        {
            MapMusicComboBox.ItemsSource = _mapMusicOptions;
        }

        if (MapEncounterBattle1ComboBox is not null && MapEncounterBattle1ComboBox.ItemsSource is null)
        {
            MapEncounterBattle1ComboBox.ItemsSource = _allBattleItems;
            MapEncounterBattle2ComboBox.ItemsSource = _allBattleItems;
            MapEncounterBattle3ComboBox.ItemsSource = _allBattleItems;
            MapEncounterBattle4ComboBox.ItemsSource = _allBattleItems;
        }

        if (MapSpriteSlotListBox is not null && MapSpriteSlotListBox.ItemsSource is null)
        {
            MapSpriteSlotListBox.ItemsSource = _mapSpriteSlotEditorItems;
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
        RefreshMapCompositePreview();
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

    private void OnMapCollisionValueSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_isWindowFullyInitialized || _isRefreshingMapCollisionEditor)
        {
            return;
        }

        if (MapCollisionValueComboBox?.SelectedValue is byte value)
        {
            _selectedMapCollisionValue = value;
            UpdateMapEditorSidebar();
            DrawMapOverlays();
        }
    }

    private void OnMapCompositePreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_loadedMapTileset is null || sender is not System.Windows.Controls.Image image || image.Source is not BitmapSource)
        {
            return;
        }

        if (!TrySelectMapCellFromPreview(image, e.GetPosition(image)))
        {
            return;
        }

        var editLayerKey = (MapEditLayerComboBox?.SelectedItem as MapLayerOption)?.Key ?? "layer1";
        if (editLayerKey is "layer1" or "layer2" or "layer3" && _selectedMapTileX.HasValue && _selectedMapTileY.HasValue)
        {
            TryPaintSelectedMapTile(editLayerKey, _selectedMapTileX.Value, _selectedMapTileY.Value);
        }
        else if (editLayerKey == "collision" && _selectedMapTileX.HasValue && _selectedMapTileY.HasValue)
        {
            TryPaintSelectedCollisionCell(_selectedMapTileX.Value, _selectedMapTileY.Value);
        }

        UpdateMapOverlayStatus();
        RefreshMapCompositePreview();
        UpdateMapEditorSidebar();
    }

    private void OnMapCompositePreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_loadedMapTileset is null || sender is not System.Windows.Controls.Image image || image.Source is not BitmapSource)
        {
            return;
        }

        if (TrySelectMapCellFromPreview(image, e.GetPosition(image)))
        {
            UpdateMapOverlayStatus();
            RefreshMapCompositePreview();
            UpdateMapEditorSidebar();
        }
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
            _mapCompositeBaseBitmap = null;
            MapCompositePreviewImage.Source = null;
            return;
        }

        var effectiveTilesetAsset = GetSelectedMapTilesetAsset();
        var effectiveLayers = GetEffectiveMapLayers(effectiveTilesetAsset);
        var visibleLayers = new List<MapLayerAsset>(3);
        if (MapLayer3VisibilityCheckBox.IsChecked == true && effectiveLayers.Count > 2)
        {
            visibleLayers.Add(effectiveLayers[2]);
        }

        if (MapLayer2VisibilityCheckBox.IsChecked == true && effectiveLayers.Count > 1)
        {
            visibleLayers.Add(effectiveLayers[1]);
        }

        if (MapLayer1VisibilityCheckBox.IsChecked == true && effectiveLayers.Count > 0)
        {
            visibleLayers.Add(effectiveLayers[0]);
        }

        var activeLayerIndex = GetActiveMapTileLayerIndex();
        if (((MapEditLayerComboBox?.SelectedItem as MapLayerOption)?.Key ?? "layer1") == "collision")
        {
            activeLayerIndex = -2;
        }
        _mapCompositeBaseBitmap = CreateCompositeMapBitmap(visibleLayers, activeLayerIndex);
        DrawMapOverlays();
    }

    private void DrawMapOverlays()
    {
        if (_loadedMapTileset is null || !_mapOverlayCache.TryGetValue(_loadedMapTileset.MapId, out var overlays))
        {
            return;
        }

        if (_mapCompositeBaseBitmap is not BitmapSource source)
        {
            return;
        }

        var width = source.PixelWidth;
        var height = source.PixelHeight;
        var pixels = new byte[width * height * 4];
        source.CopyPixels(pixels, width * 4, 0);
        var overlayOffset = GetMapOverlayPixelOffset();
        var editLayerKey = (MapEditLayerComboBox?.SelectedItem as MapLayerOption)?.Key ?? "layer1";

        if (MapCollisionVisibilityCheckBox?.IsChecked == true)
        {
            DrawCollisionOverlay(pixels, width, height, editLayerKey == "collision" ? 0.7 : 0.4);
        }

        if (MapWarpsVisibilityCheckBox?.IsChecked == true)
        {
            foreach (var warp in overlays.Warps)
            {
                DrawOverlayMarker(pixels, width, height, warp.TileX, warp.TileY, Colors.Magenta, overlayOffset);
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
                DrawOverlayMarker(pixels, width, height, spawn.TileX, spawn.TileY, color, overlayOffset);
                DrawMapSpawnSpriteOverlay(pixels, width, height, spawn);
            }
        }

        if (_selectedMapOverlayRecord is not null)
        {
            DrawSelectedOverlayHighlight(pixels, width, height, _selectedMapOverlayRecord.TileX, _selectedMapOverlayRecord.TileY, overlayOffset);
        }

        if (editLayerKey == "collision")
        {
            DrawCollisionPlacementPreview(pixels, width, height);
        }

        DrawMapTilePlacementPreview(pixels, width, height);

        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        bitmap.Freeze();
        SetMapCompositePreviewSource(bitmap, updateLayout: false);
    }

    private BitmapSource CreateCompositeMapBitmap(IReadOnlyList<MapLayerAsset> visibleLayers, int activeLayerIndex)
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
            var dimInactiveLayer = activeLayerIndex == -2 || (activeLayerIndex >= 0 && layer.LayerIndex != activeLayerIndex);
            BlitMapLayer(layer, width, height, pixels, transparentZeroIndex, dimInactiveLayer ? 0.55 : 1.0);
        }

        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        bitmap.Freeze();
        return bitmap;
    }

    private static IndexedImage RenderEffectiveMapLayerImage(int widthInTiles, int heightInTiles, ushort[] tileEntries, byte[] tilesetPixelIndices, byte[] paletteBytes)
    {
        var tileCount = tilesetPixelIndices.Length / 64;
        var pixelIndices = new byte[widthInTiles * heightInTiles * 64];

        for (var tileY = 0; tileY < heightInTiles; tileY++)
        {
            for (var tileX = 0; tileX < widthInTiles; tileX++)
            {
                var entry = tileEntries[(tileY * widthInTiles) + tileX];
                var tileIndex = entry & 0x03FF;
                if ((uint)tileIndex >= (uint)tileCount)
                {
                    continue;
                }

                var hFlip = (entry & 0x0400) != 0;
                var vFlip = (entry & 0x0800) != 0;
                var paletteBank = (entry >> 12) & 0xF;
                var sourceTileStart = tileIndex * 64;
                var destinationTileStart = (tileY * widthInTiles + tileX) * 64;

                for (var localY = 0; localY < 8; localY++)
                {
                    for (var localX = 0; localX < 8; localX++)
                    {
                        var sourceX = hFlip ? 7 - localX : localX;
                        var sourceY = vFlip ? 7 - localY : localY;
                        var sourcePixel = tilesetPixelIndices[sourceTileStart + (sourceY * 8) + sourceX];
                        pixelIndices[destinationTileStart + (localY * 8) + localX] = (byte)(sourcePixel + (paletteBank * 16));
                    }
                }
            }
        }

        return new IndexedImage(widthInTiles, heightInTiles, pixelIndices, paletteBytes);
    }

    private static void BlitMapLayer(MapLayerAsset layer, int bitmapWidth, int bitmapHeight, byte[] output, bool transparentZeroIndex, double opacity)
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
                        if (opacity >= 1.0)
                        {
                            output[targetIndex] = color.B;
                            output[targetIndex + 1] = color.G;
                            output[targetIndex + 2] = color.R;
                        }
                        else
                        {
                            output[targetIndex] = (byte)((output[targetIndex] * (1.0 - opacity)) + (color.B * opacity));
                            output[targetIndex + 1] = (byte)((output[targetIndex + 1] * (1.0 - opacity)) + (color.G * opacity));
                            output[targetIndex + 2] = (byte)((output[targetIndex + 2] * (1.0 - opacity)) + (color.R * opacity));
                        }

                        output[targetIndex + 3] = 0xFF;
                    }
                }
            }
        }
    }

    private void ClearMapPreview()
    {
        _loadedMapTileset = null;
        _mapCompositeBaseBitmap = null;
        _selectedMapTileX = null;
        _selectedMapTileY = null;
        _hoveredMapTileX = null;
        _hoveredMapTileY = null;
        _selectedMapTilesetTileIndex = null;
        _selectedMapTilesetTileEndIndex = null;
        _selectedMapTilesetTilePaletteBank = 0;
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
        MapMetadataEditorPanel.Visibility = Visibility.Collapsed;
        _isRefreshingMapMetadataEditor = false;
        MapMusicComboBox.SelectedIndex = -1;
        MapHasEncountersCheckBox.IsChecked = false;
        MapEncounterBattle1ComboBox.SelectedIndex = -1;
        MapEncounterBattle2ComboBox.SelectedIndex = -1;
        MapEncounterBattle3ComboBox.SelectedIndex = -1;
        MapEncounterBattle4ComboBox.SelectedIndex = -1;
        _mapSpriteSlotEditorItems.Clear();
        _mapSpriteSlotEditorMapId = null;
        _selectedMapSpriteSlotIndex = 0;
        MapSelectedSpriteSlotOptionsListBox.ItemsSource = null;
        if (MapSpriteSlotPickerPopup is not null)
        {
            MapSpriteSlotPickerPopup.IsOpen = false;
        }
        MapMetadataStatusLabel.Text = "Select a map to edit map metadata.";
        MapTileEditorPanel.Visibility = Visibility.Visible;
        MapCollisionEditorPanel.Visibility = Visibility.Collapsed;
        MapTilesetComboBox.SelectedIndex = -1;
        MapCollisionValueComboBox.SelectedIndex = -1;
        MapTilesetPaletteImage.Source = null;
        MapTilesetPaletteStatusLabel.Text = "Select a map to browse its tileset.";
        MapOverlayEditorPanel.Visibility = Visibility.Collapsed;
        MapTileEditorStatusLabel.Text = "Select a map and click the composite preview to inspect tiles.";
        MapCollisionEditorStatusLabel.Text = "Select a map to inspect collision cells.";
        MapOverlayPreviewBorder.Visibility = Visibility.Collapsed;
        MapOverlayPreviewImage.Source = null;
        MapOverlayPreviewLabel.Text = string.Empty;
        MapWarpFieldsPanel.Visibility = Visibility.Collapsed;
        MapEventSpawnFieldsPanel.Visibility = Visibility.Collapsed;
        ApplyMapOverlayChangesButton.Visibility = Visibility.Collapsed;
        MapOverlayScriptSummaryHeader.Visibility = Visibility.Collapsed;
        MapOverlayScriptSummaryTextBox.Visibility = Visibility.Collapsed;
        MapOverlayScriptSummaryTextBox.Text = string.Empty;
        OpenMapOverlayEventScriptEditorButton.Visibility = Visibility.Collapsed;
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

        if (MapCollisionVisibilityCheckBox?.IsChecked == true)
        {
            visibleLayers.Add("Collision");
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

    private void UpdateMapMetadataEditor()
    {
        if (MapMetadataEditorPanel is null)
        {
            return;
        }

        if (_loadedMapTileset is null)
        {
            MapMetadataEditorPanel.Visibility = Visibility.Collapsed;
            return;
        }

        EnsureMapMetadataOptions();
        _isRefreshingMapMetadataEditor = true;
        try
        {
            var encounter = GetEffectiveMapEncounter(_loadedMapTileset.MapId);
            if (encounter is not null)
            {
                MapEncounterBattle1ComboBox.SelectedValue = encounter.Battle1;
                MapEncounterBattle2ComboBox.SelectedValue = encounter.Battle2;
                MapEncounterBattle3ComboBox.SelectedValue = encounter.Battle3;
                MapEncounterBattle4ComboBox.SelectedValue = encounter.Battle4;
            }
            else
            {
                MapEncounterBattle1ComboBox.SelectedIndex = -1;
                MapEncounterBattle2ComboBox.SelectedIndex = -1;
                MapEncounterBattle3ComboBox.SelectedIndex = -1;
                MapEncounterBattle4ComboBox.SelectedIndex = -1;
            }

            MapHasEncountersCheckBox.IsChecked = GetEffectiveMapHasEncounters(_loadedMapTileset);
            MapMusicComboBox.SelectedValue = GetEffectiveMapMusicId(_loadedMapTileset);
            PopulateMapSpriteSlotEditorItems(_loadedMapTileset);
            if (_selectedMapSpriteSlotIndex < 0 || _selectedMapSpriteSlotIndex >= _mapSpriteSlotEditorItems.Count)
            {
                _selectedMapSpriteSlotIndex = 0;
            }

            if (MapSpriteSlotListBox is not null)
            {
                MapSpriteSlotListBox.SelectedIndex = _selectedMapSpriteSlotIndex;
            }

            UpdateSelectedMapSpriteSlotEditor();
        }
        finally
        {
            _isRefreshingMapMetadataEditor = false;
        }

        MapMetadataStatusLabel.Text = $"Encounter slot: {_loadedMapTileset.MapId:D3}{Environment.NewLine}Music: {_metadata.GetSongName(GetEffectiveMapMusicId(_loadedMapTileset))}{Environment.NewLine}Edit metadata here, then use Apply Changes to stage it into the project.";
    }

    private void PopulateMapSpriteSlotEditorItems(MapTilesetAsset asset)
    {
        var resourceIds = GetEffectiveMapEventObjectResourceIds(asset);
        var normalizedResourceIds = resourceIds.Take(16)
            .Concat(Enumerable.Repeat((byte)0xFF, Math.Max(0, 16 - resourceIds.Count)))
            .Take(16)
            .ToArray();
        var currentValues = _mapSpriteSlotEditorItems
            .Select(item => (byte)(item.SelectedOption?.RawValue ?? 0xFF))
            .ToArray();
        if (_mapSpriteSlotEditorMapId == asset.MapId &&
            currentValues.Length == 16 &&
            currentValues.SequenceEqual(normalizedResourceIds))
        {
            return;
        }

        _mapSpriteSlotEditorItems.Clear();
        _mapSpriteSlotEditorMapId = asset.MapId;
        for (var slotIndex = 0; slotIndex < 16; slotIndex++)
        {
            var rawValue = normalizedResourceIds[slotIndex];
            var item = new MapSpriteSlotEditorItem
            {
                SlotIndex = slotIndex,
                ResolvedDisplay = DescribeMapSpriteSlotValue(rawValue)
            };

            foreach (var option in BuildMapSpriteSlotOptions(rawValue))
            {
                item.Options.Add(option);
            }

            item.SelectedOption = item.Options.FirstOrDefault(option => option.RawValue == rawValue);
            item.DisplayTitle = item.SelectedOption?.Title ?? "Hidden";
            item.Thumbnail = item.SelectedOption?.Thumbnail ?? CreateMapSpawnHiddenThumbnail();
            _mapSpriteSlotEditorItems.Add(item);
        }
    }

    private IReadOnlyList<MapSpriteSlotOption> BuildMapSpriteSlotOptions(byte selectedRawValue)
    {
        var options = new List<MapSpriteSlotOption>
        {
            new()
            {
                RawValue = 0xFF,
                Title = "Hidden",
                Subtitle = "Unused slot",
                Thumbnail = CreateMapSpawnHiddenThumbnail()
            }
        };

        for (var rawValue = 0; rawValue <= 0xBF; rawValue++)
        {
            options.Add(new MapSpriteSlotOption
            {
                RawValue = rawValue,
                Title = $"Sprite {rawValue:X2}",
                Subtitle = DescribeMapSpriteSlotValue((byte)rawValue),
                Thumbnail = TryBuildMapSpawnSelectorThumbnail(rawValue, 1) ?? CreateMapSpawnHiddenThumbnail()
            });
        }

        if (selectedRawValue != 0xFF && selectedRawValue > 0xBF)
        {
            options.Add(new MapSpriteSlotOption
            {
                RawValue = selectedRawValue,
                Title = $"Special {selectedRawValue:X2}",
                Subtitle = DescribeMapSpriteSlotValue(selectedRawValue),
                Thumbnail = CreateMapSpawnHiddenThumbnail()
            });
        }

        return options;
    }

    private string DescribeMapSpriteSlotValue(byte rawValue)
    {
        if (rawValue == 0xFF)
        {
            return "Unused slot";
        }

        if (rawValue <= 0xBF)
        {
            return $"Direct sprite resource 0x{rawValue:X2}";
        }

        return $"Special sprite resource 0x{rawValue:X2}";
    }

    private EncounterDefinition? GetEffectiveMapEncounter(int mapId)
    {
        if (_mapMetadataDraftMapId == mapId && _mapMetadataDraftEncounterBattleIds is { Length: 4 } draftBattles)
        {
            var dataOffset = _loadedEncounters.FirstOrDefault(candidate => candidate.Id == mapId)?.DataOffset ?? -1;
            return new EncounterDefinition(mapId, dataOffset, draftBattles[0], draftBattles[1], draftBattles[2], draftBattles[3]);
        }

        var patch = _project.MapEncounterPatches.FirstOrDefault(candidate => candidate.MapId == mapId);
        if (patch is not null)
        {
            var dataOffset = _loadedEncounters.FirstOrDefault(candidate => candidate.Id == mapId)?.DataOffset ?? -1;
            return new EncounterDefinition(mapId, dataOffset, patch.Battle1, patch.Battle2, patch.Battle3, patch.Battle4);
        }

        return _loadedEncounters.FirstOrDefault(candidate => candidate.Id == mapId);
    }

    private byte GetEffectiveMapMusicId(MapTilesetAsset asset)
    {
        if (_mapMetadataDraftMapId == asset.MapId && _mapMetadataDraftMusicId.HasValue)
        {
            return _mapMetadataDraftMusicId.Value;
        }

        return _project.MapMusicPatches.FirstOrDefault(candidate => candidate.MapId == asset.MapId)?.MusicId ?? asset.MusicId;
    }

    private bool GetEffectiveMapHasEncounters(MapTilesetAsset asset)
    {
        if (_mapMetadataDraftMapId == asset.MapId && _mapMetadataDraftEncounterEnabledByte.HasValue)
        {
            return _mapMetadataDraftEncounterEnabledByte.Value != 0;
        }

        return (_project.MapEncounterStatePatches.FirstOrDefault(candidate => candidate.MapId == asset.MapId)?.EncounterEnabledByte ?? asset.EncounterEnabledByte) != 0;
    }

    private IReadOnlyList<byte> GetEffectiveMapEventObjectResourceIds(MapTilesetAsset asset)
    {
        if (_mapMetadataDraftMapId == asset.MapId && _mapMetadataDraftEventObjectResourceIds is { Length: > 0 } draftResources)
        {
            return draftResources;
        }

        if (_loadedMapTileset is not null &&
            _mapSpriteSlotEditorMapId == asset.MapId &&
            asset.MapId == _loadedMapTileset.MapId &&
            _mapSpriteSlotEditorItems.Count == 16 &&
            MapMetadataEditorPanel.Visibility == Visibility.Visible)
        {
            return _mapSpriteSlotEditorItems
                .Select(item => (byte)(item.SelectedOption?.RawValue ?? 0xFF))
                .ToArray();
        }

        return _project.MapEventObjectResourcePatches.FirstOrDefault(candidate => candidate.MapId == asset.MapId)?.ResourceIds
            ?? asset.EventObjectResourceIds;
    }

    private void ClearCurrentMapMetadataDraft()
    {
        _mapMetadataDraftMapId = null;
        _mapMetadataDraftEncounterEnabledByte = null;
        _mapMetadataDraftMusicId = null;
        _mapMetadataDraftEncounterBattleIds = null;
        _mapMetadataDraftEventObjectResourceIds = null;
    }

    private byte[] GetCurrentMapMetadataEditorSpriteResourceIds()
    {
        if (_mapSpriteSlotEditorItems.Count == 16)
        {
            return _mapSpriteSlotEditorItems
                .Select(item => (byte)(item.SelectedOption?.RawValue ?? 0xFF))
                .ToArray();
        }

        if (_mapMetadataDraftEventObjectResourceIds is { Length: > 0 } draftResourceIds)
        {
            return draftResourceIds.ToArray();
        }

        if (_loadedMapTileset is null)
        {
            return Enumerable.Repeat((byte)0xFF, 16).ToArray();
        }

        return _loadedMapTileset.EventObjectResourceIds
            .Take(16)
            .Concat(Enumerable.Repeat((byte)0xFF, Math.Max(0, 16 - _loadedMapTileset.EventObjectResourceIds.Count)))
            .Take(16)
            .ToArray();
    }

    private MapTilesetAsset? TryGetSourceMapTilesetAsset(int mapId)
    {
        if (_sourceMapTilesetCache.TryGetValue(mapId, out var cached))
        {
            return cached;
        }

        var sourcePath = _project.SourceRomPath ?? _session?.RomFile.FilePath;
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return null;
        }

        var romFile = new RomFile(sourcePath, File.ReadAllBytes(sourcePath));
        var asset = _mapTilesetRepository.ReadMap(romFile, mapId, _metadata.GetMapName(mapId));
        _sourceMapTilesetCache[mapId] = asset;
        return asset;
    }

    private void UpdateSelectedMapSpriteSlotEditor()
    {
        if (MapSelectedSpriteSlotOptionsListBox is null)
        {
            return;
        }

        if (_selectedMapSpriteSlotIndex < 0 || _selectedMapSpriteSlotIndex >= _mapSpriteSlotEditorItems.Count)
        {
            _isRefreshingSelectedMapSpriteSlotEditor = true;
            try
            {
                MapSelectedSpriteSlotOptionsListBox.ItemsSource = null;
            }
            finally
            {
                _isRefreshingSelectedMapSpriteSlotEditor = false;
            }

            return;
        }

        var item = _mapSpriteSlotEditorItems[_selectedMapSpriteSlotIndex];
        _isRefreshingSelectedMapSpriteSlotEditor = true;
        try
        {
            if (!ReferenceEquals(MapSelectedSpriteSlotOptionsListBox.ItemsSource, item.Options))
            {
                MapSelectedSpriteSlotOptionsListBox.ItemsSource = item.Options;
            }

            MapSelectedSpriteSlotOptionsListBox.SelectedItem = item.SelectedOption;
        }
        finally
        {
            _isRefreshingSelectedMapSpriteSlotEditor = false;
        }
    }

    private void UpdateMapEditorSidebar()
    {
        var editLayerKey = (MapEditLayerComboBox?.SelectedItem as MapLayerOption)?.Key ?? "layer1";
        var metadataMode = editLayerKey == "metadata";
        var overlayMode = editLayerKey is "events" or "warps";
        var collisionMode = editLayerKey == "collision";
        var metadataWasVisible = MapMetadataEditorPanel.Visibility == Visibility.Visible;
        MapMetadataEditorPanel.Visibility = metadataMode ? Visibility.Visible : Visibility.Collapsed;
        MapTileEditorPanel.Visibility = overlayMode || collisionMode || metadataMode ? Visibility.Collapsed : Visibility.Visible;
        MapCollisionEditorPanel.Visibility = collisionMode ? Visibility.Visible : Visibility.Collapsed;
        MapOverlayEditorPanel.Visibility = overlayMode ? Visibility.Visible : Visibility.Collapsed;

        if (metadataMode)
        {
            if (!metadataWasVisible)
            {
                UpdateMapMetadataEditor();
            }
        }
        else if (overlayMode)
        {
            if (MapSpriteSlotPickerPopup is not null)
            {
                MapSpriteSlotPickerPopup.IsOpen = false;
            }

            UpdateMapOverlayEditor(editLayerKey);
        }
        else if (collisionMode)
        {
            if (MapSpriteSlotPickerPopup is not null)
            {
                MapSpriteSlotPickerPopup.IsOpen = false;
            }

            UpdateMapCollisionEditor();
        }
        else
        {
            if (MapSpriteSlotPickerPopup is not null)
            {
                MapSpriteSlotPickerPopup.IsOpen = false;
            }

            UpdateMapTileLayerEditor(editLayerKey);
        }
    }

    private void UpdateMapCollisionEditor()
    {
        MapEditorTitleLabel.Text = "Collision Editor";
        if (_loadedMapTileset is null)
        {
            MapCollisionEditorStatusLabel.Text = "Select a map to inspect collision cells.";
            return;
        }

        EnsureMapCollisionValueOptions();
        if (MapCollisionValueComboBox is not null)
        {
            _isRefreshingMapCollisionEditor = true;
            try
            {
                MapCollisionValueComboBox.ItemsSource = null;
                MapCollisionValueComboBox.ItemsSource = _mapCollisionValueOptions;
                MapCollisionValueComboBox.SelectedValue = _selectedMapCollisionValue;
            }
            finally
            {
                _isRefreshingMapCollisionEditor = false;
            }
        }

        var (width, height) = GetCollisionGridDimensions(_loadedMapTileset);
        if (!_selectedMapTileX.HasValue || !_selectedMapTileY.HasValue)
        {
            MapCollisionEditorStatusLabel.Text = $"Collision grid: {width}x{height} cells (16x16).{Environment.NewLine}Selected value: 0x{_selectedMapCollisionValue:X2}{Environment.NewLine}Click a cell in the map preview to paint collision.";
            return;
        }

        var x = _selectedMapTileX.Value;
        var y = _selectedMapTileY.Value;
        if (x < 0 || y < 0 || x >= width || y >= height)
        {
            MapCollisionEditorStatusLabel.Text = $"Collision cell ({x}, {y}) is outside the current map.";
            return;
        }

        var bytes = GetEffectiveMapCollisionBytes();
        var value = bytes[(y * width) + x];
        MapCollisionEditorStatusLabel.Text = $"Collision cell ({x}, {y}){Environment.NewLine}Current value: 0x{value:X2}{Environment.NewLine}Paint value: 0x{_selectedMapCollisionValue:X2}";
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
            var selectedTileText = _selectedMapTilesetTileIndex.HasValue
                ? $"Selected paint tile: {_selectedMapTilesetTileIndex.Value} (bank {_selectedMapTilesetTilePaletteBank})"
                : "No paint tile selected yet.";
            MapTileEditorStatusLabel.Text = $"Layer size: {layer.HeaderWidthInTiles}x{layer.HeaderHeightInTiles} tiles.{Environment.NewLine}{selectedTileText}{Environment.NewLine}Click the tileset palette to choose a tile, then click the map to paint.";
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
        var selectedPaintTileText = _selectedMapTilesetTileIndex.HasValue
            ? $"Selected paint tile: {_selectedMapTilesetTileIndex.Value} (bank {_selectedMapTilesetTilePaletteBank})"
            : "Selected paint tile: none";
        MapTileEditorStatusLabel.Text = $"Tile ({tileX}, {tileY}){Environment.NewLine}Entry: 0x{tileEntry:X4}{Environment.NewLine}Tile Index: {tileEntry & 0x03FF}{Environment.NewLine}HFlip: {((tileEntry & 0x0400) != 0)}  VFlip: {((tileEntry & 0x0800) != 0)}{Environment.NewLine}Palette Bank: {(tileEntry >> 12) & 0xF}{Environment.NewLine}{selectedPaintTileText}";
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
            MapWarpFieldsPanel.Visibility = Visibility.Collapsed;
            MapEventSpawnFieldsPanel.Visibility = Visibility.Collapsed;
            ApplyMapOverlayChangesButton.Visibility = Visibility.Collapsed;
            MapOverlayScriptSummaryHeader.Visibility = Visibility.Collapsed;
            MapOverlayScriptSummaryTextBox.Visibility = Visibility.Collapsed;
            MapOverlayScriptSummaryTextBox.Text = string.Empty;
            OpenMapOverlayEventScriptEditorButton.Visibility = Visibility.Collapsed;
            MapOverlayEditorStatusLabel.Text = editLayerKey == "warps"
                ? "Click a highlighted warp tile in the composite preview to inspect that warp."
                : "Click a highlighted event/spawn tile in the composite preview to inspect that record.";
            return;
        }

        if (selected.Warp is not null)
        {
            var warp = selected.Warp;
            MapWarpFieldsPanel.Visibility = Visibility.Visible;
            MapEventSpawnFieldsPanel.Visibility = Visibility.Collapsed;
            ApplyMapOverlayChangesButton.Visibility = Visibility.Visible;
            MapOverlayPreviewBorder.Visibility = Visibility.Collapsed;
            MapOverlayPreviewImage.Source = null;
            MapOverlayPreviewLabel.Text = string.Empty;
            MapOverlayScriptSummaryHeader.Visibility = Visibility.Collapsed;
            MapOverlayScriptSummaryTextBox.Visibility = Visibility.Collapsed;
            MapOverlayScriptSummaryTextBox.Text = string.Empty;
            OpenMapOverlayEventScriptEditorButton.Visibility = Visibility.Collapsed;
            PopulateMapWarpEditorFields(warp);
            MapOverlayEditorStatusLabel.Text = $"Warp tile: ({warp.TileX}, {warp.TileY}){Environment.NewLine}Destination map: {warp.DestinationMapId:D3}{Environment.NewLine}Destination tile: ({warp.DestinationTileX}, {warp.DestinationTileY}){Environment.NewLine}Arrival facing: {warp.ArrivalFacing}{Environment.NewLine}Transition kind: {warp.TransitionKind}{Environment.NewLine}Unknown bytes: 0x{warp.Unknown4:X2} 0x{warp.Unknown5:X2}";
            return;
        }

        if (selected.Spawn is not null)
        {
            var spawn = selected.Spawn;
            MapWarpFieldsPanel.Visibility = Visibility.Collapsed;
            MapEventSpawnFieldsPanel.Visibility = Visibility.Visible;
            ApplyMapOverlayChangesButton.Visibility = Visibility.Visible;
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

            if (TryGetMapSpawnLinkedEventId(spawn, out var eventId))
            {
                MapOverlayScriptSummaryHeader.Visibility = Visibility.Visible;
                MapOverlayScriptSummaryTextBox.Visibility = Visibility.Visible;
                MapOverlayScriptSummaryTextBox.Text = BuildMapSpawnScriptBreakdown(eventId);
                OpenMapOverlayEventScriptEditorButton.Visibility = Visibility.Visible;
                OpenMapOverlayEventScriptEditorButton.Tag = eventId;
            }
            else
            {
                MapOverlayScriptSummaryHeader.Visibility = Visibility.Collapsed;
                MapOverlayScriptSummaryTextBox.Visibility = Visibility.Collapsed;
                MapOverlayScriptSummaryTextBox.Text = string.Empty;
                OpenMapOverlayEventScriptEditorButton.Visibility = Visibility.Collapsed;
                OpenMapOverlayEventScriptEditorButton.Tag = null;
            }

            PopulateMapSpawnEditorFields(spawn);
            MapOverlayEditorStatusLabel.Text = $"Tile: ({spawn.TileX}, {spawn.TileY}){Environment.NewLine}Record kind: 0x{spawn.RecordKind:X}{Environment.NewLine}Event/Object id: 0x{spawn.EventOrObjectId:X3}{Environment.NewLine}Spawn group: {spawn.SpawnGroupIndex}{Environment.NewLine}Chapter mask: 0x{spawn.ChapterVisibilityMask:X4}{Environment.NewLine}Sprite/facing packed: 0x{spawn.SpriteAndFacingPacked:X2}";
        }
    }

    private void PopulateMapWarpEditorFields(MapWarpRecord warp)
    {
        MapWarpTileXEntry.Text = warp.TileX.ToString();
        MapWarpTileYEntry.Text = warp.TileY.ToString();
        MapWarpDestinationMapEntry.Text = warp.DestinationMapId.ToString();
        MapWarpDestinationTileXEntry.Text = warp.DestinationTileX.ToString();
        MapWarpDestinationTileYEntry.Text = warp.DestinationTileY.ToString();
        MapWarpArrivalFacingEntry.Text = warp.ArrivalFacing.ToString();
        MapWarpTransitionKindEntry.Text = warp.TransitionKind.ToString();
        MapWarpUnknown4Entry.Text = $"0x{warp.Unknown4:X2}";
        MapWarpUnknown5Entry.Text = $"0x{warp.Unknown5:X2}";
    }

    private void PopulateMapSpawnEditorFields(MapEntitySpawnRecord spawn)
    {
        MapSpawnTileXEntry.Text = spawn.TileX.ToString();
        MapSpawnTileYEntry.Text = spawn.TileY.ToString();
        MapSpawnRecordKindEntry.Text = $"0x{spawn.RecordKind:X}";
        MapSpawnEventOrObjectIdEntry.Text = spawn.EventOrObjectId.ToString();
        MapSpawnGroupEntry.Text = spawn.SpawnGroupIndex.ToString();
        MapSpawnChapterMaskEntry.Text = $"0x{spawn.ChapterVisibilityMask:X4}";
        PopulateMapSpawnSpriteSelectors(spawn);
    }

    private void PopulateMapSpawnSpriteSelectors(MapEntitySpawnRecord spawn)
    {
        if (MapSpawnSpriteComboBox is null || MapSpawnFacingComboBox is null)
        {
            return;
        }

        _isRefreshingMapSpawnSelectors = true;
        try
        {
            BuildMapSpawnSpriteOptions(spawn);
            MapSpawnSpriteComboBox.ItemsSource = null;
            MapSpawnSpriteComboBox.ItemsSource = _mapSpawnSpriteOptions;

            var selectedResourceIndex = spawn.SpriteAndFacingPacked == 0xFF
                ? -1
                : (spawn.SpriteAndFacingPacked >> 4) & 0x0F;
            MapSpawnSpriteComboBox.SelectedItem = _mapSpawnSpriteOptions.FirstOrDefault(option => option.ResourceIndex == selectedResourceIndex);

            BuildMapSpawnFacingOptions(selectedResourceIndex, GetMapSpawnFacingVariant(spawn));
        }
        finally
        {
            _isRefreshingMapSpawnSelectors = false;
        }
    }

    private void BuildMapSpawnSpriteOptions(MapEntitySpawnRecord spawn)
    {
        _mapSpawnSpriteOptions.Clear();
        _mapSpawnSpriteOptions.Add(new MapSpawnSpriteOption(
            -1,
            -1,
            "Hidden",
            "No visible sprite",
            CreateMapSpawnHiddenThumbnail(),
            true));

        for (var resourceIndex = 0; resourceIndex < 16; resourceIndex++)
        {
            var spriteId = ResolveMapSpawnOverworldSheetId(resourceIndex);
            var title = spriteId >= 0
                ? $"Sprite {spriteId:D3}"
                : $"Slot {resourceIndex:X}";
            var subtitle = $"Map slot {resourceIndex:X}";
            var thumbnail = TryBuildMapSpawnSelectorThumbnail(spriteId, 1) ?? CreateMapSpawnHiddenThumbnail();
            _mapSpawnSpriteOptions.Add(new MapSpawnSpriteOption(
                resourceIndex,
                spriteId,
                title,
                subtitle,
                thumbnail,
                false));
        }
    }

    private void BuildMapSpawnFacingOptions(int resourceIndex, int selectedFacingVariant)
    {
        _mapSpawnFacingOptions.Clear();
        var spriteId = resourceIndex >= 0 ? ResolveMapSpawnOverworldSheetId(resourceIndex) : -1;
        if (resourceIndex < 0 || spriteId < 0)
        {
            MapSpawnFacingComboBox.ItemsSource = null;
            MapSpawnFacingComboBox.IsEnabled = false;
            return;
        }

        for (var facingVariant = 0; facingVariant < 4; facingVariant++)
        {
            _mapSpawnFacingOptions.Add(new MapSpawnFacingOption(
                facingVariant,
                GetMapSpawnFacingName(facingVariant),
                TryBuildMapSpawnSelectorThumbnail(spriteId, facingVariant) ?? CreateMapSpawnHiddenThumbnail()));
        }

        MapSpawnFacingComboBox.ItemsSource = null;
        MapSpawnFacingComboBox.ItemsSource = _mapSpawnFacingOptions;
        MapSpawnFacingComboBox.IsEnabled = true;
        MapSpawnFacingComboBox.SelectedItem = _mapSpawnFacingOptions.FirstOrDefault(option => option.FacingVariant == selectedFacingVariant)
            ?? _mapSpawnFacingOptions.FirstOrDefault();
    }

    private BitmapSource? TryBuildMapSpawnSelectorThumbnail(int spriteId, int facingVariant)
    {
        if (spriteId < 0 || spriteId >= MedabotsRomSchema.SpriteCount)
        {
            return null;
        }

        try
        {
            var asset = GetCurrentOverworldSpriteAsset(spriteId);
            var frameImage = OverworldSpriteFrameExtractor.ExtractFacingFrame(asset.Image, facingVariant);
            var swatches = BuildPaletteSwatches(frameImage.PaletteBytes);
            return CreateBitmapSource(frameImage.PixelIndices, frameImage.TileWidth, swatches);
        }
        catch
        {
            return null;
        }
    }

    private static BitmapSource CreateMapSpawnHiddenThumbnail()
    {
        const int width = 16;
        const int height = 24;
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var index = (y * width + x) * 4;
                var border = x == 0 || y == 0 || x == width - 1 || y == height - 1;
                var slash = x == y / 2 || x == width - 1 - (y / 2);
                var color = border || slash ? Colors.Red : Colors.Transparent;
                pixels[index] = color.B;
                pixels[index + 1] = color.G;
                pixels[index + 2] = color.R;
                pixels[index + 3] = color.A;
            }
        }

        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        bitmap.Freeze();
        return bitmap;
    }

    private void OnMapSpawnSpriteSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_isWindowFullyInitialized || _isRefreshingMapSpawnSelectors || MapSpawnSpriteComboBox?.SelectedItem is not MapSpawnSpriteOption option)
        {
            return;
        }

        _isRefreshingMapSpawnSelectors = true;
        try
        {
            var selectedFacing = MapSpawnFacingComboBox?.SelectedItem as MapSpawnFacingOption;
            BuildMapSpawnFacingOptions(option.ResourceIndex, selectedFacing?.FacingVariant ?? 1);
        }
        finally
        {
            _isRefreshingMapSpawnSelectors = false;
        }
    }

    private async void OnApplyMapOverlayChangesClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_loadedMapTileset is null || _selectedMapOverlayRecord is null || _selectedMapOverlayRecord.OriginalIndex < 0)
            {
                await DisplayAlertAsync("No Overlay Selected", "Select a warp or event/spawn first.", "OK");
                return;
            }

            if (_selectedMapOverlayRecord.Warp is not null)
            {
                var updated = new MapWarpRecord(
                    ParseRequiredByte(MapWarpTileXEntry.Text, "Tile X"),
                    ParseRequiredByte(MapWarpTileYEntry.Text, "Tile Y"),
                    ParseRequiredByte(MapWarpDestinationMapEntry.Text, "Destination Map"),
                    (byte)(((ParseRequiredByte(MapWarpTransitionKindEntry.Text, "Transition Kind") & 0x0F) << 4) |
                           (ParseRequiredByte(MapWarpArrivalFacingEntry.Text, "Arrival Facing") & 0x07)),
                    ParseRequiredByte(MapWarpUnknown4Entry.Text, "Unknown 4"),
                    ParseRequiredByte(MapWarpUnknown5Entry.Text, "Unknown 5"),
                    ParseRequiredByte(MapWarpDestinationTileXEntry.Text, "Destination Tile X"),
                    ParseRequiredByte(MapWarpDestinationTileYEntry.Text, "Destination Tile Y"));
                ReplaceCurrentWarpRecord(_selectedMapOverlayRecord.OriginalIndex, updated);
                _selectedMapOverlayRecord = ResolveMapOverlaySelection("warps", updated.TileX, updated.TileY);
            }
            else if (_selectedMapOverlayRecord.Spawn is not null)
            {
                var recordKind = ParseRequiredByte(MapSpawnRecordKindEntry.Text, "Record Kind");
                var eventOrObjectId = ParseRequiredUShort(MapSpawnEventOrObjectIdEntry.Text, "Event / Object Id");
                if (eventOrObjectId > 0x0FFF)
                {
                    throw new InvalidOperationException("Event / Object Id must be between 0 and 4095.");
                }

                var updated = new MapEntitySpawnRecord(
                    ParseRequiredByte(MapSpawnTileXEntry.Text, "Tile X"),
                    ParseRequiredByte(MapSpawnTileYEntry.Text, "Tile Y"),
                    (ushort)(((recordKind & 0x0F) << 12) | (eventOrObjectId & 0x0FFF)),
                    BuildMapSpawnPackedSpriteFacingByte(),
                    ParseRequiredByte(MapSpawnGroupEntry.Text, "Spawn Group"),
                    ParseRequiredUShort(MapSpawnChapterMaskEntry.Text, "Chapter Mask"));
                ReplaceCurrentSpawnRecord(_selectedMapOverlayRecord.OriginalIndex, updated);
                _selectedMapOverlayRecord = ResolveMapOverlaySelection("events", updated.TileX, updated.TileY);
            }

            UpdateMapOverlayStatus();
            RefreshMapCompositePreview();
            UpdateMapEditorSidebar();
            UpdateStatus();
            await DisplayAlertAsync("Overlay Updated", "Stored the selected overlay changes in the project.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Overlay Update Failed", ex.Message, "OK");
        }
    }

    private void OnMapMetadataChanged(object? sender, RoutedEventArgs e)
    {
        RefreshCurrentMapMetadataDraft();
    }

    private void OnMapMetadataChanged(object? sender, SelectionChangedEventArgs e)
    {
        RefreshCurrentMapMetadataDraft();
    }

    private void OnMapSpriteSlotSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isRefreshingMapMetadataEditor || MapSpriteSlotListBox?.SelectedIndex is not int selectedIndex || selectedIndex < 0)
        {
            return;
        }

        _selectedMapSpriteSlotIndex = selectedIndex;
        OpenMapSpriteSlotPickerForIndex(selectedIndex);
    }

    private void OnMapSelectedSpriteSlotOptionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isRefreshingSelectedMapSpriteSlotEditor || _isRefreshingMapMetadataEditor)
        {
            return;
        }

        if (_selectedMapSpriteSlotIndex < 0 || _selectedMapSpriteSlotIndex >= _mapSpriteSlotEditorItems.Count)
        {
            return;
        }

        if (MapSelectedSpriteSlotOptionsListBox?.SelectedItem is not MapSpriteSlotOption option)
        {
            return;
        }

        var item = _mapSpriteSlotEditorItems[_selectedMapSpriteSlotIndex];
        item.SelectedOption = option;
        item.DisplayTitle = option.Title;
        item.Thumbnail = option.Thumbnail;
        item.ResolvedDisplay = option.Subtitle;
        MapSpriteSlotListBox?.Items.Refresh();
        RefreshCurrentMapMetadataDraft();
        if (MapSpriteSlotPickerPopup is not null)
        {
            MapSpriteSlotPickerPopup.IsOpen = false;
        }
    }

    private void OnMapSelectedSpriteSlotOptionsListBoxPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not DependencyObject dependencyObject)
        {
            return;
        }

        var scrollViewer = FindAncestor<ScrollViewer>(dependencyObject);
        if (scrollViewer is null)
        {
            return;
        }

        scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
        e.Handled = true;
    }

    private async void OnApplyMapMetadataChangesClicked(object? sender, RoutedEventArgs e)
    {
        StageCurrentMapMetadataIntoProject();
        if (_loadedMapTileset is not null)
        {
            await DisplayAlertAsync("Metadata Applied", $"Staged metadata changes for map {_loadedMapTileset.MapId:D3} in the project.", "OK");
        }
    }

    private void OnMapSpriteSlotListBoxPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (MapSpriteSlotListBox is null)
        {
            return;
        }

        var container = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (container is null)
        {
            return;
        }

        var index = MapSpriteSlotListBox.ItemContainerGenerator.IndexFromContainer(container);
        if (index < 0)
        {
            return;
        }

        _selectedMapSpriteSlotIndex = index;
        MapSpriteSlotListBox.SelectedIndex = index;
        OpenMapSpriteSlotPickerForIndex(index);
        e.Handled = true;
    }

    private static T? FindAncestor<T>(DependencyObject? current)
        where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T typed)
            {
                return typed;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private void OpenMapSpriteSlotPickerForIndex(int selectedIndex)
    {
        UpdateSelectedMapSpriteSlotEditor();
        if (MapSpriteSlotPickerPopup is not null && MapSpriteSlotListBox is not null)
        {
            MapSpriteSlotPickerPopup.PlacementTarget = MapSpriteSlotListBox.ItemContainerGenerator.ContainerFromIndex(selectedIndex) as FrameworkElement ?? MapSpriteSlotListBox;
            MapSpriteSlotPickerPopup.IsOpen = true;
        }
    }

    private void OnMapSpriteSlotPickerPopupClosed(object? sender, EventArgs e)
    {
        if (MapSelectedSpriteSlotOptionsListBox is not null)
        {
            _isRefreshingSelectedMapSpriteSlotEditor = true;
            try
            {
                MapSelectedSpriteSlotOptionsListBox.SelectedItem = null;
            }
            finally
            {
                _isRefreshingSelectedMapSpriteSlotEditor = false;
            }
        }
    }

    private void StageCurrentMapMetadataIntoProject()
    {
        if (!_isWindowFullyInitialized || _loadedMapTileset is null)
        {
            return;
        }

        var mapId = _loadedMapTileset.MapId;
        var musicId = _mapMetadataDraftMapId == mapId && _mapMetadataDraftMusicId.HasValue
            ? _mapMetadataDraftMusicId.Value
            : (MapMusicComboBox.SelectedValue is int comboMusicId ? (byte)comboMusicId : GetEffectiveMapMusicId(_loadedMapTileset));

        var hasEncounters = _mapMetadataDraftMapId == mapId && _mapMetadataDraftEncounterEnabledByte.HasValue
            ? _mapMetadataDraftEncounterEnabledByte.Value != 0
            : MapHasEncountersCheckBox.IsChecked == true;

        var encounterBattleIds = _mapMetadataDraftMapId == mapId && _mapMetadataDraftEncounterBattleIds is { Length: 4 }
            ? _mapMetadataDraftEncounterBattleIds
            : (MapEncounterBattle1ComboBox.SelectedValue is int battle1 &&
               MapEncounterBattle2ComboBox.SelectedValue is int battle2 &&
               MapEncounterBattle3ComboBox.SelectedValue is int battle3 &&
               MapEncounterBattle4ComboBox.SelectedValue is int battle4
                ? new[] { (byte)battle1, (byte)battle2, (byte)battle3, (byte)battle4 }
                : GetEffectiveMapEncounter(mapId) is { } effectiveEncounter
                    ? new[] { effectiveEncounter.Battle1, effectiveEncounter.Battle2, effectiveEncounter.Battle3, effectiveEncounter.Battle4 }
                    : null);
        var spriteResourceIds = GetCurrentMapMetadataEditorSpriteResourceIds();
        var sourceAsset = TryGetSourceMapTilesetAsset(mapId) ?? _loadedMapTileset;
        _mapMetadataProjectEditor.StageMetadata(
            _project,
            sourceAsset,
            (byte)(hasEncounters ? 1 : 0),
            musicId,
            spriteResourceIds,
            encounterBattleIds);
        MapMetadataStatusLabel.Text = $"Staged metadata changes for map {mapId:D3} in the project.{Environment.NewLine}Music: {_metadata.GetSongName(musicId)}  |  Encounters: {(hasEncounters ? "enabled" : "disabled")}";
        ClearCurrentMapMetadataDraft();
        UpdateMapMetadataEditor();
        UpdateMapOverlayStatus();
        RefreshMapCompositePreview();
        if (((MapEditLayerComboBox?.SelectedItem as MapLayerOption)?.Key ?? "layer1") != "metadata")
        {
            UpdateMapEditorSidebar();
        }

        UpdateStatus();
    }

    private async void OnRevertMapMetadataChangesClicked(object? sender, RoutedEventArgs e)
    {
        if (_loadedMapTileset is null || _session is null)
        {
            return;
        }

        var mapId = _loadedMapTileset.MapId;
        RemovePatchForMap(_project.MapEncounterPatches, mapId, patch => patch.MapId);
        RemovePatchForMap(_project.MapEncounterStatePatches, mapId, patch => patch.MapId);
        RemovePatchForMap(_project.MapMusicPatches, mapId, patch => patch.MapId);
        RemovePatchForMap(_project.MapEventObjectResourcePatches, mapId, patch => patch.MapId);
        ClearCurrentMapMetadataDraft();

        var freshAsset = _mapTilesetRepository.ReadMap(_session.RomFile, mapId, _metadata.GetMapName(mapId));
        _mapTilesetCache[mapId] = freshAsset;
        _loadedMapTileset = freshAsset;
        UpdateMapMetadataEditor();
        UpdateMapOverlayStatus();
        RefreshMapCompositePreview();
        UpdateMapEditorSidebar();
        UpdateStatus();
        await DisplayAlertAsync("Metadata Reverted", $"Removed staged metadata changes for map {mapId:D3}.", "OK");
    }

    private static void RemovePatchForMap<TPatch>(IList<TPatch> patches, int mapId, Func<TPatch, int> selector)
    {
        var existing = patches.FirstOrDefault(patch => selector(patch) == mapId);
        if (existing is not null)
        {
            patches.Remove(existing);
        }
    }

    private void ReplaceCurrentWarpRecord(int index, MapWarpRecord record)
    {
        if (_loadedMapTileset is null || !_mapOverlayCache.TryGetValue(_loadedMapTileset.MapId, out var overlay))
        {
            return;
        }

        var records = overlay.Warps.ToList();
        if ((uint)index >= (uint)records.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        records[index] = record;
        var existingPatch = _project.MapWarpPatches.FirstOrDefault(patch => patch.MapId == overlay.MapId);
        _mapWarpProjectEditor.UpsertWarpPatch(_project, overlay.MapId, records, existingPatch?.DeletedOriginalIndices);
        ReplaceCurrentMapOverlayWarps(records);
    }

    private void ReplaceCurrentSpawnRecord(int index, MapEntitySpawnRecord record)
    {
        if (_loadedMapTileset is null || !_mapOverlayCache.TryGetValue(_loadedMapTileset.MapId, out var overlay))
        {
            return;
        }

        var records = overlay.EntitySpawns.ToList();
        if ((uint)index >= (uint)records.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        records[index] = record;
        var existingPatch = _project.MapEntitySpawnPatches.FirstOrDefault(patch => patch.MapId == overlay.MapId);
        _mapEntitySpawnProjectEditor.UpsertEntitySpawnPatch(_project, overlay.MapId, records, existingPatch?.DeletedOriginalIndices);
        ReplaceCurrentMapOverlayEntitySpawns(records);
    }

    private static byte ParseRequiredByte(string? text, string fieldName)
    {
        var value = ParseInteger(text, fieldName);
        if (value < byte.MinValue || value > byte.MaxValue)
        {
            throw new InvalidOperationException($"{fieldName} must be between 0 and 255.");
        }

        return (byte)value;
    }

    private static ushort ParseRequiredUShort(string? text, string fieldName)
    {
        var value = ParseInteger(text, fieldName);
        if (value < ushort.MinValue || value > ushort.MaxValue)
        {
            throw new InvalidOperationException($"{fieldName} must be between 0 and 65535.");
        }

        return (ushort)value;
    }

    private static int ParseInteger(string? text, string fieldName)
    {
        var trimmed = text?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new InvalidOperationException($"{fieldName} is required.");
        }

        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return Convert.ToInt32(trimmed[2..], 16);
        }

        return int.Parse(trimmed, System.Globalization.CultureInfo.InvariantCulture);
    }

    private byte BuildMapSpawnPackedSpriteFacingByte()
    {
        if (MapSpawnSpriteComboBox?.SelectedItem is not MapSpawnSpriteOption spriteOption)
        {
            throw new InvalidOperationException("Select a character sprite.");
        }

        if (spriteOption.IsHidden || spriteOption.ResourceIndex < 0)
        {
            return 0xFF;
        }

        if (MapSpawnFacingComboBox?.SelectedItem is not MapSpawnFacingOption facingOption)
        {
            throw new InvalidOperationException("Select a facing direction.");
        }

        return (byte)(((spriteOption.ResourceIndex & 0x0F) << 4) | (facingOption.FacingVariant & 0x03));
    }

    private void OnOpenMapOverlayEventScriptEditorClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: short eventId })
        {
            return;
        }

        OpenDetailedEventScriptEditor(eventId);
    }

    private bool TryGetMapSpawnLinkedEventId(MapEntitySpawnRecord spawn, out short eventId)
    {
        eventId = default;
        var profile = RequireProfile();
        if (profile is null || _session is null)
        {
            return false;
        }

        var installedTable = EventScriptReader.ResolveInstalledEventTable(_session.RomFile.Data, profile);
        if (spawn.EventOrObjectId < 0 || spawn.EventOrObjectId >= installedTable.EventCount)
        {
            return false;
        }

        eventId = (short)spawn.EventOrObjectId;
        return true;
    }

    private string BuildMapSpawnScriptBreakdown(short eventId)
    {
        try
        {
            var visualState = EnsureEventVisualState(eventId);
            var lines = new List<string> { $"Event {eventId:D4}" };
            foreach (var instruction in visualState.Instructions.Take(12))
            {
                var line = instruction.HasLabelDisplay
                    ? $"{instruction.LabelDisplay}: {instruction.Summary}"
                    : instruction.Summary;
                if (instruction.HasDetail)
                {
                    line = $"{line} [{instruction.Detail.Replace(Environment.NewLine, " | ")}]";
                }

                lines.Add(line);
            }

            if (visualState.Instructions.Count > 12)
            {
                lines.Add($"... {visualState.Instructions.Count - 12} more instructions");
            }

            return string.Join(Environment.NewLine, lines);
        }
        catch (Exception ex)
        {
            return $"Failed to build script preview for event {eventId:D4}:{Environment.NewLine}{ex.Message}";
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
            BlitIndexedImageOverlay(frameImage, bitmapWidth, bitmapHeight, destX, destY, swatches, pixels, 0.5);
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
            var warpIndex = overlays.Warps
                .Select((record, index) => (record, index))
                .FirstOrDefault(entry => entry.record.TileX == tileX && entry.record.TileY == tileY);
            return warpIndex.record is null
                ? null
                : new MapOverlayRecordItem
                {
                    Key = $"warp:{warpIndex.record.TileX}:{warpIndex.record.TileY}:{warpIndex.index}",
                    OriginalIndex = warpIndex.index,
                    TileX = warpIndex.record.TileX,
                    TileY = warpIndex.record.TileY,
                    Warp = warpIndex.record,
                    DisplayName = $"({warpIndex.record.TileX}, {warpIndex.record.TileY}) -> Map {warpIndex.record.DestinationMapId:D3}",
                    Description = $"Dest ({warpIndex.record.DestinationTileX}, {warpIndex.record.DestinationTileY})  Facing {warpIndex.record.ArrivalFacing}  Transition {warpIndex.record.TransitionKind}"
                };
        }

        var chapterIndex = (MapEventChapterFilterComboBox?.SelectedItem as MapChapterFilterOption)?.ChapterIndex;
        var spawnIndex = overlays.EntitySpawns
            .Select((record, index) => (record, index))
            .FirstOrDefault(entry =>
                entry.record.TileX == tileX &&
                entry.record.TileY == tileY &&
                (!chapterIndex.HasValue || entry.record.IsVisibleInChapter(chapterIndex.Value)));

        return spawnIndex.record is null
            ? null
            : new MapOverlayRecordItem
            {
                Key = $"spawn:{spawnIndex.record.TileX}:{spawnIndex.record.TileY}:{spawnIndex.record.RecordKindAndEventId:X4}:{spawnIndex.index}",
                OriginalIndex = spawnIndex.index,
                TileX = spawnIndex.record.TileX,
                TileY = spawnIndex.record.TileY,
                Spawn = spawnIndex.record,
                DisplayName = $"({spawnIndex.record.TileX}, {spawnIndex.record.TileY})  Kind {spawnIndex.record.RecordKind:X}  Id {spawnIndex.record.EventOrObjectId:X3}",
                Description = $"Group {spawnIndex.record.SpawnGroupIndex}  ChapterMask 0x{spawnIndex.record.ChapterVisibilityMask:X4}  SpriteFacing 0x{spawnIndex.record.SpriteAndFacingPacked:X2}"
            };
    }

    private bool TrySelectMapCellFromPreview(System.Windows.Controls.Image image, System.Windows.Point position)
    {
        if (image.Source is not BitmapSource source)
        {
            return false;
        }

        var editLayerKey = (MapEditLayerComboBox?.SelectedItem as MapLayerOption)?.Key ?? "layer1";
        var coordinateDivisor = editLayerKey is "events" or "warps" or "collision" ? 16 : 8;
        var overlayOffset = editLayerKey is "events" or "warps" or "collision" ? GetMapOverlayPixelOffset() : default;
        var sourceX = position.X / _mapPreviewZoom;
        var sourceY = position.Y / _mapPreviewZoom;
        var adjustedX = sourceX - overlayOffset.X;
        var adjustedY = sourceY - overlayOffset.Y;
        var tileX = (int)(adjustedX / coordinateDivisor);
        var tileY = (int)(adjustedY / coordinateDivisor);
        if (tileX < 0 || tileY < 0 || sourceX < 0 || sourceY < 0 || sourceX >= source.PixelWidth || sourceY >= source.PixelHeight)
        {
            return false;
        }

        _selectedMapTileX = tileX;
        _selectedMapTileY = tileY;
        _selectedMapOverlayRecord = ResolveMapOverlaySelection(editLayerKey, tileX, tileY);
        return true;
    }

    private void OnMapCompositePreviewContextMenuOpening(object? sender, ContextMenuEventArgs e)
    {
        var editLayerKey = (MapEditLayerComboBox?.SelectedItem as MapLayerOption)?.Key ?? "layer1";
        var eventMode = editLayerKey == "events";
        var warpMode = editLayerKey == "warps";

        AddMapEventContextMenuItem.Visibility = eventMode ? Visibility.Visible : Visibility.Collapsed;
        DeleteMapEventContextMenuItem.Visibility = eventMode ? Visibility.Visible : Visibility.Collapsed;
        MapEventContextMenuSeparator.Visibility = (eventMode && warpMode) ? Visibility.Visible : Visibility.Collapsed;
        AddMapWarpContextMenuItem.Visibility = warpMode ? Visibility.Visible : Visibility.Collapsed;
        DeleteMapWarpContextMenuItem.Visibility = warpMode ? Visibility.Visible : Visibility.Collapsed;

        AddMapEventContextMenuItem.IsEnabled = eventMode && _selectedMapTileX.HasValue && _selectedMapTileY.HasValue;
        DeleteMapEventContextMenuItem.IsEnabled = eventMode && _selectedMapOverlayRecord?.Spawn is not null;
        AddMapWarpContextMenuItem.IsEnabled = warpMode && _selectedMapTileX.HasValue && _selectedMapTileY.HasValue;
        DeleteMapWarpContextMenuItem.IsEnabled = warpMode && _selectedMapOverlayRecord?.Warp is not null;
    }

    private void OnAddMapEventHereClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_session is null || _loadedMapTileset is null || !_selectedMapTileX.HasValue || !_selectedMapTileY.HasValue)
            {
                return;
            }

            var profile = RequireProfile();
            if (profile is null)
            {
                return;
            }

            var overlay = GetCurrentMapOverlayAsset();
            if (overlay is null)
            {
                return;
            }

            var eventId = _eventScriptProjectEditor.AddFreshEventScript(_project, _session.RomFile, profile, [MedabotsRomSchema.EventEndOpcode]);
            var record = new MapEntitySpawnRecord(
                (byte)_selectedMapTileX.Value,
                (byte)_selectedMapTileY.Value,
                (ushort)(0x4000 | (ushort)eventId),
                0xFF,
                0,
                0xFFFF);

            var patch = _mapEntitySpawnProjectEditor.AddEntitySpawnRecord(_project, overlay, record);
            ReplaceCurrentMapOverlayEntitySpawns(patch.Records);
            _selectedMapOverlayRecord = ResolveMapOverlaySelection("events", record.TileX, record.TileY);
            UpdateMapOverlayStatus();
            RefreshMapCompositePreview();
            UpdateMapEditorSidebar();
            UpdateStatus();
        }
        catch (Exception ex)
        {
            _ = DisplayAlertAsync("Add Event Failed", ex.Message, "OK");
        }
    }

    private void OnDeleteMapEventClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_loadedMapTileset is null || _selectedMapOverlayRecord?.Spawn is null || _selectedMapOverlayRecord.OriginalIndex < 0)
            {
                return;
            }

            var overlay = GetCurrentMapOverlayAsset();
            if (overlay is null)
            {
                return;
            }

            var patch = _mapEntitySpawnProjectEditor.DeleteExistingEntitySpawnRecord(_project, overlay, _selectedMapOverlayRecord.OriginalIndex);
            ReplaceCurrentMapOverlayEntitySpawns(patch.Records);
            _selectedMapOverlayRecord = null;
            UpdateMapOverlayStatus();
            RefreshMapCompositePreview();
            UpdateMapEditorSidebar();
            UpdateStatus();
        }
        catch (Exception ex)
        {
            _ = DisplayAlertAsync("Delete Event Failed", ex.Message, "OK");
        }
    }

    private void OnAddMapWarpHereClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_loadedMapTileset is null || !_selectedMapTileX.HasValue || !_selectedMapTileY.HasValue)
            {
                return;
            }

            var overlay = GetCurrentMapOverlayAsset();
            if (overlay is null)
            {
                return;
            }

            var record = new MapWarpRecord(
                (byte)_selectedMapTileX.Value,
                (byte)_selectedMapTileY.Value,
                (byte)_loadedMapTileset.MapId,
                0x01,
                0x00,
                0x00,
                (byte)_selectedMapTileX.Value,
                (byte)_selectedMapTileY.Value);

            var patch = _mapWarpProjectEditor.AddWarpRecord(_project, overlay, record);
            ReplaceCurrentMapOverlayWarps(patch.Records);
            _selectedMapOverlayRecord = ResolveMapOverlaySelection("warps", record.TileX, record.TileY);
            UpdateMapOverlayStatus();
            RefreshMapCompositePreview();
            UpdateMapEditorSidebar();
            UpdateStatus();
        }
        catch (Exception ex)
        {
            _ = DisplayAlertAsync("Add Warp Failed", ex.Message, "OK");
        }
    }

    private void OnDeleteMapWarpClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_loadedMapTileset is null || _selectedMapOverlayRecord?.Warp is null || _selectedMapOverlayRecord.OriginalIndex < 0)
            {
                return;
            }

            var overlay = GetCurrentMapOverlayAsset();
            if (overlay is null)
            {
                return;
            }

            var patch = _mapWarpProjectEditor.DeleteExistingWarpRecord(_project, overlay, _selectedMapOverlayRecord.OriginalIndex);
            ReplaceCurrentMapOverlayWarps(patch.Records);
            _selectedMapOverlayRecord = null;
            UpdateMapOverlayStatus();
            RefreshMapCompositePreview();
            UpdateMapEditorSidebar();
            UpdateStatus();
        }
        catch (Exception ex)
        {
            _ = DisplayAlertAsync("Delete Warp Failed", ex.Message, "OK");
        }
    }

    private void OnMapTilesetSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_isWindowFullyInitialized || _loadedMapTileset is null || MapTilesetComboBox?.SelectedItem is not MapTilesetOption option)
        {
            return;
        }

        _selectedMapTilesetSourceMapIds[_loadedMapTileset.MapId] = option.RepresentativeMapId;
        RefreshMapTilesetPalettePreview();
        RefreshMapCompositePreview();
        UpdateMapEditorSidebar();
    }

    private void OnMapTilesetPaletteMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.Image image || image.Source is not BitmapSource source)
        {
            return;
        }

        var point = e.GetPosition(image);
        var scaleX = source.PixelWidth / Math.Max(1d, image.ActualWidth);
        var scaleY = source.PixelHeight / Math.Max(1d, image.ActualHeight);
        var tileX = (int)((point.X * scaleX) / 8);
        var tileY = (int)((point.Y * scaleY) / 8);
        var tilesetAsset = GetSelectedMapTilesetAsset();
        if (tileX < 0 || tileY < 0 || tileX >= tilesetAsset.TilesetSheet.TileWidth || tileY >= tilesetAsset.TilesetSheet.TileHeight)
        {
            return;
        }

        var tileIndex = (tileY * tilesetAsset.TilesetSheet.TileWidth) + tileX;
        _selectedMapTilesetTileIndex = tileIndex;
        _selectedMapTilesetTileEndIndex = tileIndex;
        _selectedMapTilesetTilePaletteBank = tileIndex < tilesetAsset.TilePaletteBanks.Count && tilesetAsset.TilePaletteBanks[tileIndex] >= 0
            ? tilesetAsset.TilePaletteBanks[tileIndex]
            : 0;
        _isDraggingMapTilesetSelection = true;
        image.CaptureMouse();
        RefreshMapTilesetPalettePreview();
        UpdateMapEditorSidebar();
    }

    private void OnMapTilesetPaletteMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isDraggingMapTilesetSelection || sender is not System.Windows.Controls.Image image || image.Source is not BitmapSource source || _selectedMapTilesetTileIndex is null)
        {
            return;
        }

        var tilesetAsset = GetSelectedMapTilesetAsset();
        var point = e.GetPosition(image);
        var scaleX = source.PixelWidth / Math.Max(1d, image.ActualWidth);
        var scaleY = source.PixelHeight / Math.Max(1d, image.ActualHeight);
        var tileX = (int)((point.X * scaleX) / 8);
        var tileY = (int)((point.Y * scaleY) / 8);
        if (tileX < 0 || tileY < 0 || tileX >= tilesetAsset.TilesetSheet.TileWidth || tileY >= tilesetAsset.TilesetSheet.TileHeight)
        {
            return;
        }

        _selectedMapTilesetTileEndIndex = (tileY * tilesetAsset.TilesetSheet.TileWidth) + tileX;
        RefreshMapTilesetPalettePreview();
        UpdateMapEditorSidebar();
    }

    private void OnMapTilesetPaletteMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDraggingMapTilesetSelection || sender is not System.Windows.Controls.Image image)
        {
            return;
        }

        _isDraggingMapTilesetSelection = false;
        image.ReleaseMouseCapture();
        RefreshMapTilesetPalettePreview();
        UpdateMapEditorSidebar();
    }

    private void SynchronizeSelectedMapTilesetOption(MapTilesetAsset asset)
    {
        if (MapTilesetComboBox is null)
        {
            return;
        }

        if (_selectedMapTilesetSourceMapIds.TryGetValue(asset.MapId, out var sourceMapId))
        {
            MapTilesetComboBox.SelectedValue = sourceMapId;
            return;
        }

        var matched = _mapTilesetOptions.FirstOrDefault(option =>
            option.GraphicsDataOffset == asset.GraphicsDataOffset &&
            option.PaletteDataOffset == asset.PaletteDataOffset &&
            option.ColorAttributeDataOffset == asset.ColorAttributeDataOffset);
        MapTilesetComboBox.SelectedValue = matched?.RepresentativeMapId ?? asset.MapId;
    }

    private void RefreshMapTilesetPalettePreview()
    {
        if (_loadedMapTileset is null || MapTilesetPaletteImage is null)
        {
            return;
        }

        var tilesetAsset = GetSelectedMapTilesetAsset();
        MapTilesetPaletteImage.Source = CreateMapTilesetPaletteBitmap(tilesetAsset);
        MapTilesetPaletteStatusLabel.Text = _selectedMapTilesetTileIndex.HasValue
            ? BuildMapTilesetSelectionStatus(tilesetAsset)
            : $"Tileset source: {tilesetAsset.MapId:D3}  {tilesetAsset.Name}. Click or drag to choose a paint source.";
    }

    private MapTilesetAsset GetSelectedMapTilesetAsset()
    {
        if (_loadedMapTileset is null || _session is null)
        {
            throw new InvalidOperationException("No map is loaded.");
        }

        if (!_selectedMapTilesetSourceMapIds.TryGetValue(_loadedMapTileset.MapId, out var sourceMapId))
        {
            return _loadedMapTileset;
        }

        if (!_mapTilesetCache.TryGetValue(sourceMapId, out var asset))
        {
            asset = _mapTilesetRepository.ReadMap(_session.RomFile, sourceMapId, _metadata.GetMapName(sourceMapId));
            _mapTilesetCache[sourceMapId] = asset;
        }

        return asset;
    }

    private BitmapSource CreateMapTilesetPaletteBitmap(MapTilesetAsset tilesetAsset)
    {
        var swatches = BuildPaletteSwatches(tilesetAsset.TilesetSheet.PaletteBytes);
        var bitmap = CreateBitmapSource(tilesetAsset.TilesetSheet.PixelIndices, tilesetAsset.TilesetSheet.TileWidth, swatches);
        if (_selectedMapTilesetTileIndex is null)
        {
            return bitmap;
        }

        var width = bitmap.PixelWidth;
        var height = bitmap.PixelHeight;
        var pixels = new byte[width * height * 4];
        bitmap.CopyPixels(pixels, width * 4, 0);
        var selection = GetSelectedMapTilesetTileSelection(tilesetAsset);
        DrawSelectionRectangle(pixels, width, height, selection.StartTileX * 8, selection.StartTileY * 8, selection.WidthInTiles * 8, selection.HeightInTiles * 8);
        var highlighted = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        highlighted.Freeze();
        return highlighted;
    }

    private string BuildMapTilesetSelectionStatus(MapTilesetAsset tilesetAsset)
    {
        var selection = GetSelectedMapTilesetTileSelection(tilesetAsset);
        return $"Selected tiles: {selection.WidthInTiles}x{selection.HeightInTiles}  |  Start tile {selection.StartTileIndex}  |  Suggested palette bank {_selectedMapTilesetTilePaletteBank}";
    }

    private IReadOnlyList<MapLayerAsset> GetEffectiveMapLayers(MapTilesetAsset tilesetSource)
    {
        if (_loadedMapTileset is null)
        {
            return [];
        }

        return _loadedMapTileset.Layers
            .Select(layer =>
            {
                var entries = GetEffectiveMapLayerEntries(layer);
                var image = RenderEffectiveMapLayerImage(layer.HeaderWidthInTiles == 0 ? layer.Image.TileWidth : layer.HeaderWidthInTiles, layer.HeaderHeightInTiles == 0 ? layer.Image.TileHeight : layer.HeaderHeightInTiles, entries, tilesetSource.RawTilesetPixelIndices, tilesetSource.PaletteBytes);
                return new MapLayerAsset(layer.LayerIndex, layer.PointerOffset, layer.DataOffset, layer.HeaderWidthInTiles, layer.HeaderHeightInTiles, layer.HeaderOriginX, layer.HeaderOriginY, layer.HeaderOriginX2, layer.HeaderOriginY2, entries, image);
            })
            .ToArray();
    }

    private byte[] GetEffectiveMapCollisionBytes()
    {
        if (_loadedMapTileset is null)
        {
            return [];
        }

        var (width, height) = GetCollisionGridDimensions(_loadedMapTileset);
        var expectedLength = width * height;
        var source = _loadedMapTileset.CollisionBytes ?? [];
        var values = new byte[expectedLength];
        Array.Copy(source, values, Math.Min(source.Length, values.Length));
        return values;
    }

    private static (int WidthInCells, int HeightInCells) GetCollisionGridDimensions(MapTilesetAsset asset)
    {
        return (Math.Max(1, asset.WidthInMetaTiles), Math.Max(1, asset.HeightInMetaTiles));
    }

    private void EnsureMapCollisionValueOptions()
    {
        if (_loadedMapTileset is null)
        {
            if (_mapCollisionValueOptions.Count == 0)
            {
                _mapCollisionValueOptions.Add(new MapCollisionValueOption { Value = 0x00, DisplayName = "0x00" });
            }

            return;
        }

        var values = new SortedSet<byte>(GetEffectiveMapCollisionBytes());
        for (byte value = 0; value < 0x10; value++)
        {
            values.Add(value);
        }

        if (values.Count == 0)
        {
            values.Add(0x00);
        }

        _mapCollisionValueOptions.Clear();
        foreach (var value in values)
        {
            _mapCollisionValueOptions.Add(new MapCollisionValueOption
            {
                Value = value,
                DisplayName = $"0x{value:X2}"
            });
        }

        if (!_mapCollisionValueOptions.Any(option => option.Value == _selectedMapCollisionValue))
        {
            _selectedMapCollisionValue = _mapCollisionValueOptions[0].Value;
        }
    }

    private bool TryPaintSelectedCollisionCell(int cellX, int cellY)
    {
        if (_loadedMapTileset is null)
        {
            return false;
        }

        var (width, height) = GetCollisionGridDimensions(_loadedMapTileset);
        if (cellX < 0 || cellY < 0 || cellX >= width || cellY >= height)
        {
            return false;
        }

        var edited = GetEffectiveMapCollisionBytes();
        edited[(cellY * width) + cellX] = _selectedMapCollisionValue;
        ReplaceCurrentMapCollisionBytes(edited);
        return true;
    }

    private void ReplaceCurrentMapCollisionBytes(byte[] colorAttributeBytes)
    {
        if (_loadedMapTileset is null)
        {
            return;
        }

        var patch = _mapCollisionProjectEditor.UpsertCollisionPatch(_project, _loadedMapTileset.MapId, colorAttributeBytes);
        var updatedAsset = _loadedMapTileset with { CollisionBytes = patch.ColorAttributeBytes.ToArray() };
        _loadedMapTileset = updatedAsset;
        _mapTilesetCache[updatedAsset.MapId] = updatedAsset;
        EnsureMapCollisionValueOptions();
    }

    private ushort[] GetEffectiveMapLayerEntries(MapLayerAsset layer)
    {
        if (_loadedMapTileset is not null)
        {
            var patch = ProjectEditCollection.Find(_project, ProjectEditAdapters.MapLayer, (_loadedMapTileset.MapId, layer.LayerIndex));
            if (patch is not null)
            {
                return patch.TileEntries.ToArray();
            }
        }

        return layer.TileEntries.ToArray();
    }

    private bool TryPaintSelectedMapTile(string editLayerKey, int tileX, int tileY)
    {
        if (_loadedMapTileset is null || !_selectedMapTilesetTileIndex.HasValue)
        {
            return false;
        }

        var layerIndex = editLayerKey switch
        {
            "layer1" => 0,
            "layer2" => 1,
            "layer3" => 2,
            _ => -1
        };
        if (layerIndex < 0 || layerIndex >= _loadedMapTileset.Layers.Count)
        {
            return false;
        }

        var layer = _loadedMapTileset.Layers[layerIndex];
        var width = layer.HeaderWidthInTiles == 0 ? layer.Image.TileWidth : layer.HeaderWidthInTiles;
        var height = layer.HeaderHeightInTiles == 0 ? layer.Image.TileHeight : layer.HeaderHeightInTiles;
        if (tileX < 0 || tileY < 0 || tileX >= width || tileY >= height)
        {
            return false;
        }

        var edited = GetEffectiveMapLayerEntries(layer);

        var tilesetAsset = GetSelectedMapTilesetAsset();
        var selection = GetSelectedMapTilesetTileSelection(tilesetAsset);
        for (var offsetY = 0; offsetY < selection.HeightInTiles; offsetY++)
        {
            for (var offsetX = 0; offsetX < selection.WidthInTiles; offsetX++)
            {
                var destTileX = tileX + offsetX;
                var destTileY = tileY + offsetY;
                if (destTileX < 0 || destTileY < 0 || destTileX >= width || destTileY >= height)
                {
                    continue;
                }

                var sourceTileIndex = selection.StartTileIndex + offsetX + (offsetY * tilesetAsset.TilesetSheet.TileWidth);
                var paletteBank = sourceTileIndex < tilesetAsset.TilePaletteBanks.Count && tilesetAsset.TilePaletteBanks[sourceTileIndex] >= 0
                    ? tilesetAsset.TilePaletteBanks[sourceTileIndex]
                    : _selectedMapTilesetTilePaletteBank;
                edited[(destTileY * width) + destTileX] = (ushort)((paletteBank << 12) | (sourceTileIndex & 0x03FF));
            }
        }

        _mapLayerProjectEditor.StageLayer(_project, _loadedMapTileset.MapId, layer, edited);
        return true;
    }

    private MapOverlayAsset? GetCurrentMapOverlayAsset()
    {
        if (_loadedMapTileset is null)
        {
            return null;
        }

        return _mapOverlayCache.GetValueOrDefault(_loadedMapTileset.MapId);
    }

    private void ReplaceCurrentMapOverlayEntitySpawns(IEnumerable<MapEntitySpawnRecord> records)
    {
        if (_loadedMapTileset is null || !_mapOverlayCache.TryGetValue(_loadedMapTileset.MapId, out var overlay))
        {
            return;
        }

        _mapOverlayCache[_loadedMapTileset.MapId] = overlay with { EntitySpawns = records.ToArray() };
    }

    private void ReplaceCurrentMapOverlayWarps(IEnumerable<MapWarpRecord> records)
    {
        if (_loadedMapTileset is null || !_mapOverlayCache.TryGetValue(_loadedMapTileset.MapId, out var overlay))
        {
            return;
        }

        _mapOverlayCache[_loadedMapTileset.MapId] = overlay with { Warps = records.ToArray() };
    }

    private int TryReadMapPointerOffset(int pointerOffset)
    {
        if (_session is null)
        {
            return -1;
        }

        return GbaPointer.TryReadFileOffset(_session.RomFile.Data, pointerOffset, out var dataOffset)
            ? dataOffset
            : -1;
    }

    private (int StartTileIndex, int StartTileX, int StartTileY, int WidthInTiles, int HeightInTiles) GetSelectedMapTilesetTileSelection(MapTilesetAsset tilesetAsset)
    {
        if (!_selectedMapTilesetTileIndex.HasValue)
        {
            return (0, 0, 0, 1, 1);
        }

        var startIndex = _selectedMapTilesetTileIndex.Value;
        var endIndex = _selectedMapTilesetTileEndIndex ?? startIndex;
        var startX = startIndex % tilesetAsset.TilesetSheet.TileWidth;
        var startY = startIndex / tilesetAsset.TilesetSheet.TileWidth;
        var endX = endIndex % tilesetAsset.TilesetSheet.TileWidth;
        var endY = endIndex / tilesetAsset.TilesetSheet.TileWidth;
        var minX = Math.Min(startX, endX);
        var minY = Math.Min(startY, endY);
        var maxX = Math.Max(startX, endX);
        var maxY = Math.Max(startY, endY);
        return ((minY * tilesetAsset.TilesetSheet.TileWidth) + minX, minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    private static void DrawSelectionRectangle(byte[] pixels, int bitmapWidth, int bitmapHeight, int x, int y, int width, int height)
    {
        for (var pixelY = y - 1; pixelY <= y + height; pixelY++)
        {
            if ((uint)pixelY >= (uint)bitmapHeight)
            {
                continue;
            }

            for (var pixelX = x - 1; pixelX <= x + width; pixelX++)
            {
                if ((uint)pixelX >= (uint)bitmapWidth)
                {
                    continue;
                }

                var outerBorder = pixelX == x - 1 || pixelY == y - 1 || pixelX == x + width || pixelY == y + height;
                var innerBorder = pixelX == x || pixelY == y || pixelX == x + width - 1 || pixelY == y + height - 1;
                if (!outerBorder && !innerBorder)
                {
                    continue;
                }

                var color = outerBorder ? Colors.Black : Colors.White;
                var index = ((pixelY * bitmapWidth) + pixelX) * 4;
                pixels[index + 0] = color.B;
                pixels[index + 1] = color.G;
                pixels[index + 2] = color.R;
                pixels[index + 3] = 0xFF;
            }
        }
    }

    private int GetActiveMapTileLayerIndex()
    {
        return (MapEditLayerComboBox?.SelectedItem as MapLayerOption)?.Key switch
        {
            "layer1" => 0,
            "layer2" => 1,
            "layer3" => 2,
            _ => -1
        };
    }

    private void OnMapCompositePreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_loadedMapTileset is null || _mapCompositeBaseBitmap is null || sender is not System.Windows.Controls.Image image || image.Source is not BitmapSource)
        {
            return;
        }

        var editLayerKey = (MapEditLayerComboBox?.SelectedItem as MapLayerOption)?.Key ?? "layer1";
        if (editLayerKey is not ("layer1" or "layer2" or "layer3" or "collision"))
        {
            return;
        }

        if (TrySelectMapHoverCell(image, e.GetPosition(image)))
        {
            DrawMapOverlays();
        }
    }

    private void OnMapCompositePreviewMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_hoveredMapTileX.HasValue || _hoveredMapTileY.HasValue)
        {
            _hoveredMapTileX = null;
            _hoveredMapTileY = null;
            DrawMapOverlays();
        }
    }

    private bool TrySelectMapHoverCell(System.Windows.Controls.Image image, System.Windows.Point position)
    {
        if (image.Source is not BitmapSource source)
        {
            return false;
        }

        var editLayerKey = (MapEditLayerComboBox?.SelectedItem as MapLayerOption)?.Key ?? "layer1";
        var coordinateDivisor = editLayerKey == "collision" ? 16 : 8;
        var overlayOffset = editLayerKey == "collision" ? GetMapOverlayPixelOffset() : default;
        var sourceX = position.X / _mapPreviewZoom;
        var sourceY = position.Y / _mapPreviewZoom;
        var tileX = (int)((sourceX - overlayOffset.X) / coordinateDivisor);
        var tileY = (int)((sourceY - overlayOffset.Y) / coordinateDivisor);
        if (tileX < 0 || tileY < 0 || sourceX < 0 || sourceY < 0 || sourceX >= source.PixelWidth || sourceY >= source.PixelHeight)
        {
            return false;
        }

        var changed = _hoveredMapTileX != tileX || _hoveredMapTileY != tileY;
        _hoveredMapTileX = tileX;
        _hoveredMapTileY = tileY;
        return changed;
    }

    private void DrawMapTilePlacementPreview(byte[] pixels, int bitmapWidth, int bitmapHeight)
    {
        var activeLayerIndex = GetActiveMapTileLayerIndex();
        if (activeLayerIndex < 0 || !_hoveredMapTileX.HasValue || !_hoveredMapTileY.HasValue || !_selectedMapTilesetTileIndex.HasValue || _loadedMapTileset is null)
        {
            return;
        }

        var tilesetAsset = GetSelectedMapTilesetAsset();
        var selection = GetSelectedMapTilesetTileSelection(tilesetAsset);
        var swatches = BuildPaletteSwatches(tilesetAsset.TilesetSheet.PaletteBytes);

        for (var offsetY = 0; offsetY < selection.HeightInTiles; offsetY++)
        {
            for (var offsetX = 0; offsetX < selection.WidthInTiles; offsetX++)
            {
                var sourceTileIndex = selection.StartTileIndex + offsetX + (offsetY * tilesetAsset.TilesetSheet.TileWidth);
                var destX = (_hoveredMapTileX.Value + offsetX) * 8;
                var destY = (_hoveredMapTileY.Value + offsetY) * 8;
                BlitTileOverlay(tilesetAsset.TilesetSheet.PixelIndices, sourceTileIndex, bitmapWidth, bitmapHeight, destX, destY, swatches, pixels, 0.45);
            }
        }

        DrawSelectionRectangle(pixels, bitmapWidth, bitmapHeight, _hoveredMapTileX.Value * 8, _hoveredMapTileY.Value * 8, selection.WidthInTiles * 8, selection.HeightInTiles * 8);
    }

    private void DrawCollisionOverlay(byte[] pixels, int bitmapWidth, int bitmapHeight, double opacity)
    {
        if (_loadedMapTileset is null)
        {
            return;
        }

        var values = GetEffectiveMapCollisionBytes();
        var (widthInCells, heightInCells) = GetCollisionGridDimensions(_loadedMapTileset);
        var overlayOffset = GetMapOverlayPixelOffset();
        var clampedOpacity = Math.Clamp(opacity, 0.0, 1.0);

        for (var cellY = 0; cellY < heightInCells; cellY++)
        {
            for (var cellX = 0; cellX < widthInCells; cellX++)
            {
                var value = values[(cellY * widthInCells) + cellX];
                if (value == 0)
                {
                    continue;
                }

                DrawCollisionCell(pixels, bitmapWidth, bitmapHeight, cellX, cellY, GetCollisionColor(value), clampedOpacity, overlayOffset);
            }
        }
    }

    private void DrawCollisionPlacementPreview(byte[] pixels, int bitmapWidth, int bitmapHeight)
    {
        if (_loadedMapTileset is null || !_hoveredMapTileX.HasValue || !_hoveredMapTileY.HasValue)
        {
            return;
        }

        var overlayOffset = GetMapOverlayPixelOffset();
        DrawCollisionCell(pixels, bitmapWidth, bitmapHeight, _hoveredMapTileX.Value, _hoveredMapTileY.Value, GetCollisionColor(_selectedMapCollisionValue), 0.35, overlayOffset);
        DrawSelectionRectangle(
            pixels,
            bitmapWidth,
            bitmapHeight,
            _hoveredMapTileX.Value * 16 + (int)overlayOffset.X,
            _hoveredMapTileY.Value * 16 + (int)overlayOffset.Y,
            16,
            16);
    }

    private static void DrawCollisionCell(byte[] pixels, int bitmapWidth, int bitmapHeight, int cellX, int cellY, System.Windows.Media.Color color, double opacity, System.Windows.Point offset)
    {
        var startX = cellX * 16 + (int)offset.X;
        var startY = cellY * 16 + (int)offset.Y;
        var blend = Math.Clamp(opacity, 0.0, 1.0);
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

                var index = (pixelY * bitmapWidth + pixelX) * 4;
                pixels[index] = (byte)((pixels[index] * (1.0 - blend)) + (color.B * blend));
                pixels[index + 1] = (byte)((pixels[index + 1] * (1.0 - blend)) + (color.G * blend));
                pixels[index + 2] = (byte)((pixels[index + 2] * (1.0 - blend)) + (color.R * blend));
                pixels[index + 3] = 0xFF;
            }
        }
    }

    private static System.Windows.Media.Color GetCollisionColor(byte value)
    {
        if (value == 0)
        {
            return Colors.Transparent;
        }

        var r = (byte)(80 + ((value * 73) % 156));
        var g = (byte)(60 + ((value * 151) % 156));
        var b = (byte)(40 + ((value * 199) % 156));
        return System.Windows.Media.Color.FromArgb(255, r, g, b);
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
        return ResolveMapSpawnOverworldSheetId(resourceIndex);
    }

    private int ResolveMapSpawnOverworldSheetId(int resourceIndex)
    {
        if (_loadedMapTileset is null || resourceIndex < 0 || resourceIndex > 0x0F)
        {
            return -1;
        }

        var resourceIds = GetEffectiveMapEventObjectResourceIds(_loadedMapTileset);
        if (resourceIndex >= resourceIds.Count)
        {
            return -1;
        }

        var sheetId = resourceIds[resourceIndex];
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

    private void SetMapCompositePreviewSource(BitmapSource bitmap, bool updateLayout = true)
    {
        MapCompositePreviewImage.Source = bitmap;
        if (updateLayout)
        {
            UpdateMapPreviewLayout(bitmap.PixelWidth, bitmap.PixelHeight);
            UpdateMapGridOverlay(bitmap.PixelWidth, bitmap.PixelHeight);
        }
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
        var cellSize = editLayerKey is "events" or "warps" or "collision" ? 16 : 8;
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

    private static void BlitIndexedImageOverlay(IndexedImage image, int bitmapWidth, int bitmapHeight, int destX, int destY, IReadOnlyList<PaletteSwatchItem> swatches, byte[] output, double opacity = 1.0)
    {
        for (var tileY = 0; tileY < image.TileHeight; tileY++)
        {
            for (var tileX = 0; tileX < image.TileWidth; tileX++)
            {
                var tileIndex = (tileY * image.TileWidth) + tileX;
                BlitTileOverlay(image.PixelIndices, tileIndex, bitmapWidth, bitmapHeight, destX + (tileX * 8), destY + (tileY * 8), swatches, output, opacity);
            }
        }
    }

    private static void BlitTileOverlay(byte[] pixelIndices, int tileIndex, int bitmapWidth, int bitmapHeight, int destX, int destY, IReadOnlyList<PaletteSwatchItem> swatches, byte[] output, double opacity = 1.0)
    {
        var blend = Math.Clamp(opacity, 0.0, 1.0);
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
                output[outputIndex + 0] = (byte)Math.Round((output[outputIndex + 0] * (1.0 - blend)) + (color.B * blend));
                output[outputIndex + 1] = (byte)Math.Round((output[outputIndex + 1] * (1.0 - blend)) + (color.G * blend));
                output[outputIndex + 2] = (byte)Math.Round((output[outputIndex + 2] * (1.0 - blend)) + (color.R * blend));
                output[outputIndex + 3] = 0xFF;
            }
        }
    }
}
