using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows;
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
    private const int MinimumMapPreviewTileWidth = 32;

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
        EnsureMapTilesetOptions();
        var matchingOption = _mapTilesetOptions.FirstOrDefault(option =>
            option.GraphicsDataOffset == asset.GraphicsDataOffset &&
            option.PaletteDataOffset == asset.PaletteDataOffset &&
            option.ColorAttributeDataOffset == asset.ColorAttributeDataOffset);

        MapTilesetSelectorComboBox.ItemsSource = _mapTilesetOptions;
        MapTilesetSelectorComboBox.DisplayMemberPath = nameof(MapTilesetOption.DisplayName);
        MapTilesetSelectorComboBox.SelectedItem = matchingOption;
        var maxLayerWidth = asset.Layers.Count == 0 ? asset.WidthInTiles : asset.Layers.Max(layer => layer.HeaderWidthInTiles);
        var maxLayerHeight = asset.Layers.Count == 0 ? asset.HeightInTiles : asset.Layers.Max(layer => layer.HeaderHeightInTiles);
        MapSummaryLabel.Text = $"Map {asset.MapId:D3}  {asset.Name}{Environment.NewLine}Base size: {asset.WidthInTiles}x{asset.HeightInTiles} tiles ({asset.WidthInMetaTiles}x{asset.HeightInMetaTiles} meta-tiles){Environment.NewLine}Layer size: {maxLayerWidth}x{maxLayerHeight} tiles{Environment.NewLine}Graphics @ 0x{asset.GraphicsDataOffset:X}  Palette @ 0x{asset.PaletteDataOffset:X}  Color Attr @ {(asset.ColorAttributeDataOffset >= 0 ? $"0x{asset.ColorAttributeDataOffset:X}" : "none")}";
        MapTilesetSummaryLabel.Text = "Tileset graphics are browsed from the Sprites tab under Map Tilesets. The map tab stays focused on layout and layer data.";

        PopulateMapLayerPreview(MapLayer1PreviewImage, MapLayer1SummaryLabel, asset.Layers.ElementAtOrDefault(0));
        PopulateMapLayerPreview(MapLayer2PreviewImage, MapLayer2SummaryLabel, asset.Layers.ElementAtOrDefault(1));
        PopulateMapLayerPreview(MapLayer3PreviewImage, MapLayer3SummaryLabel, asset.Layers.ElementAtOrDefault(2));
        RefreshMapCompositePreview();
    }

    private void EnsureMapTilesetOptions()
    {
        if (_mapTilesetOptions.Count > 0 || _session is null)
        {
            return;
        }

        var groups = Enumerable.Range(0, Math.Min(MedabotsRomSchema.MapCount, _metadata.Catalog.Maps.Count))
            .Select(mapId => _mapTilesetRepository.ReadMap(_session.RomFile, mapId, _metadata.GetMapName(mapId)))
            .GroupBy(asset => (asset.GraphicsDataOffset, asset.PaletteDataOffset, asset.ColorAttributeDataOffset))
            .OrderBy(group => group.First().GraphicsDataOffset);

        foreach (var group in groups)
        {
            var representative = group.First();
            var usedBy = group.Select(asset => asset.MapId).OrderBy(id => id).ToArray();
            _mapTilesetOptions.Add(new MapTilesetOption(
                representative.MapId,
                representative.GraphicsDataOffset,
                representative.PaletteDataOffset,
                representative.ColorAttributeDataOffset,
                $"Tileset from {_metadata.GetMapName(representative.MapId)} ({usedBy.Length} maps)"));
        }
    }

    private void PopulateMapLayerPreview(System.Windows.Controls.Image imageControl, TextBlock summaryLabel, MapLayerAsset? layer)
    {
        if (layer is null)
        {
            imageControl.Source = null;
            summaryLabel.Text = "Missing layer";
            return;
        }

        imageControl.Source = CreateMapLayerBitmap(layer, transparentZeroIndex: layer.LayerIndex is 0 or 1);
        summaryLabel.Text = $"Layer {layer.LayerIndex + 1} @ 0x{layer.DataOffset:X}  |  Size: {layer.HeaderWidthInTiles}x{layer.HeaderHeightInTiles}  Origin A: ({layer.HeaderOriginX}, {layer.HeaderOriginY})  Origin B: ({layer.HeaderOriginX2}, {layer.HeaderOriginY2})";
    }

    private void OnMapLayerVisibilityChanged(object? sender, RoutedEventArgs e)
    {
        if (!_isWindowFullyInitialized)
        {
            return;
        }

        RefreshMapCompositePreview();
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

        MapCompositePreviewImage.Source = CreateCompositeMapBitmap(visibleLayers);
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

    private static BitmapSource CreateMapLayerBitmap(MapLayerAsset layer, bool transparentZeroIndex)
    {
        var width = Math.Max(layer.Image.Width, MinimumMapPreviewTileWidth * 8);
        var height = layer.Image.Height;
        var pixels = new byte[width * height * 4];
        BlitMapLayer(layer, width, height, pixels, transparentZeroIndex);
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
        MapSummaryLabel.Text = "Select a map to inspect its tileset and layers.";
        MapTilesetSelectorComboBox.ItemsSource = null;
        MapTilesetSummaryLabel.Text = string.Empty;
        MapCompositePreviewImage.Source = null;
        MapLayer1PreviewImage.Source = null;
        MapLayer2PreviewImage.Source = null;
        MapLayer3PreviewImage.Source = null;
        MapLayer1SummaryLabel.Text = string.Empty;
        MapLayer2SummaryLabel.Text = string.Empty;
        MapLayer3SummaryLabel.Text = string.Empty;
    }
}
