using Medabots.Rom.Maps;
using Medabots.Rom.Metadata;
using Xunit;

namespace Medabots.Rom.Tests;

public sealed class MapOverlayRepositoryTests
{
    [Fact]
    public async Task Repository_ReadsRealMapOverlays()
    {
        var rom = await RomFile.LoadAsync(TestRomLocator.FindWorkspaceRom());
        var repository = new MapOverlayRepository();

        var asset = repository.ReadMap(rom, 16);

        Assert.Equal(16, asset.MapId);
        Assert.Equal(MedabotsRomSchema.MapWarpPointerTableOffset + (16 * 4), asset.WarpPointerOffset);
        Assert.Equal(MedabotsRomSchema.MapEntitySpawnPointerTableOffset + (16 * 4), asset.EntitySpawnPointerOffset);
        Assert.NotEmpty(asset.Warps);
        Assert.NotEmpty(asset.EntitySpawns);
        Assert.All(asset.Warps, warp =>
        {
            Assert.NotEqual(0xFF, warp.TileX);
            Assert.True(warp.DestinationMapId < MedabotsRomSchema.MapCount);
        });
        Assert.All(asset.EntitySpawns, spawn =>
        {
            Assert.NotEqual(0xFF, spawn.TileX);
            Assert.InRange(spawn.RecordKind, 0, 0xF);
        });
    }

    [Fact]
    public async Task Repository_ReadsAllMapOverlayTablesWithoutThrowing()
    {
        var rom = await RomFile.LoadAsync(TestRomLocator.FindWorkspaceRom());
        var repository = new MapOverlayRepository();

        for (var mapId = 0; mapId < MedabotsRomSchema.MapCount; mapId++)
        {
            var asset = repository.ReadMap(rom, mapId);
            Assert.Equal(mapId, asset.MapId);
        }
    }
}
