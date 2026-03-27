using Medabots.Rom.Maps;
using Medabots.Rom.Metadata;
using Xunit;

namespace Medabots.Rom.Tests;

public sealed class MapTilesetRepositoryTests
{
    [Fact]
    public async Task Repository_ReadsRealMapTilesetResources()
    {
        var rom = await RomFile.LoadAsync(TestRomLocator.FindWorkspaceRom());
        var repository = new MapTilesetRepository();

        var asset = repository.ReadMap(rom, 0, "Town");

        Assert.Equal(0, asset.MapId);
        Assert.Equal(MedabotsRomSchema.MapTilesetGraphicsPointerTableOffset, asset.GraphicsPointerOffset);
        Assert.Equal(0x5FC6D0, asset.GraphicsDataOffset);
        Assert.Equal(MedabotsRomSchema.MapTilesetPalettePointerTableOffset, asset.PalettePointerOffset);
        Assert.Equal(0x5FC0C0, asset.PaletteDataOffset);
        Assert.Equal(MedabotsRomSchema.MapColorAttributePointerTableOffset, asset.ColorAttributePointerOffset);
        Assert.Equal(0x5FC260, asset.ColorAttributeDataOffset);
        Assert.Equal(58, asset.WidthInTiles);
        Assert.Equal(56, asset.HeightInTiles);
        Assert.Equal(3, asset.Layers.Count);
        Assert.Equal(0x60007C, asset.Layers[0].DataOffset);
        Assert.Equal(0x600594, asset.Layers[1].DataOffset);
        Assert.Equal(0x600F74, asset.Layers[2].DataOffset);
        Assert.NotEmpty(asset.TilesetSheet.PixelIndices);
        Assert.Equal(MedabotsRomSchema.MapPaletteSize, asset.PaletteBytes.Length);
        Assert.True(asset.WidthInTiles > 0);
        Assert.True(asset.HeightInTiles > 0);
        Assert.All(asset.Layers, layer =>
        {
            Assert.Equal(layer.HeaderWidthInTiles, layer.Image.TileWidth);
            Assert.Equal(layer.HeaderHeightInTiles, layer.Image.TileHeight);
            Assert.Equal(layer.HeaderWidthInTiles * layer.HeaderHeightInTiles, layer.TileEntries.Length);
            Assert.NotEmpty(layer.Image.PixelIndices);
        });
    }

    [Fact]
    public async Task Repository_UsesLayerHeaderDimensionsForNarrowIndoorMaps()
    {
        var rom = await RomFile.LoadAsync(TestRomLocator.FindWorkspaceRom());
        var repository = new MapTilesetRepository();

        var asset = repository.ReadMap(rom, 16, "Ikki's house");

        Assert.Equal(20, asset.WidthInTiles);
        Assert.Equal(18, asset.HeightInTiles);
        Assert.All(asset.Layers, layer =>
        {
            Assert.Equal(30, layer.HeaderWidthInTiles);
            Assert.Equal(30, layer.HeaderHeightInTiles);
            Assert.Equal(900, layer.TileEntries.Length);
            Assert.Equal(30, layer.Image.TileWidth);
            Assert.Equal(30, layer.Image.TileHeight);
        });
    }

    [Fact]
    public async Task Repository_ReadsAllMapsWithoutThrowing()
    {
        var rom = await RomFile.LoadAsync(TestRomLocator.FindWorkspaceRom());
        var repository = new MapTilesetRepository();

        for (var mapId = 0; mapId < MedabotsRomSchema.MapCount; mapId++)
        {
            var asset = repository.ReadMap(rom, mapId, $"Map {mapId:D2}");
            Assert.Equal(mapId, asset.MapId);
            Assert.Equal(MedabotsRomSchema.MapLayerCount, asset.Layers.Count);
        }
    }
}
