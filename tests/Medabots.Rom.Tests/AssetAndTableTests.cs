using Medabots.Rom.Compression;
using Medabots.Rom.Encounters;
using Medabots.Rom.Images;
using Medabots.Rom.Metadata;
using Medabots.Rom.Shops;
using Medabots.Rom.Starter;
using Xunit;

namespace Medabots.Rom.Tests;

public sealed class AssetAndTableTests
{
    [Fact]
    public async Task EncounterReader_LoadsRealEncounterTable()
    {
        var rom = await RomFile.LoadAsync(FindWorkspaceRom());
        var encounters = new EncounterTableReader().ReadAll(rom);

        Assert.Equal(EncounterTableReader.EncounterCount, encounters.Count);
        Assert.Contains(encounters, encounter => encounter.Battle1 != 0 || encounter.Battle2 != 0 || encounter.Battle3 != 0 || encounter.Battle4 != 0);
    }

    [Fact]
    public async Task StarterReader_LoadsRealStarterData()
    {
        var rom = await RomFile.LoadAsync(FindWorkspaceRom());
        var profile = MedabotsRomTextProfiles.Detect(rom);

        Assert.NotNull(profile);

        var starter = new StarterReader().Read(rom, profile!);

        Assert.InRange(starter.PartId, 0, 0x77);
        Assert.InRange(starter.MedalId, 0, 0xFF);
    }

    [Fact]
    public async Task ShopReader_LocatesShopTable()
    {
        var rom = await RomFile.LoadAsync(FindWorkspaceRom());
        var shop = new ShopTableReader().Read(rom, 0, 4);

        Assert.Equal(0, shop.Id);
        Assert.Equal(4, shop.Contents.Length);
    }

    [Fact]
    public async Task ImageRepository_ReadsRealPortraitAndSpriteAssets()
    {
        var rom = await RomFile.LoadAsync(FindWorkspaceRom());
        var repository = new ImageAssetRepository();

        var portrait = repository.ReadPortrait(rom, 0, 0);
        var sprite = repository.ReadSprite(rom, 0);

        Assert.Equal(ImageAssetRepository.PaletteSize, portrait.Image.PaletteBytes.Length);
        Assert.NotEmpty(portrait.Image.PixelIndices);
        Assert.Equal(ImageAssetRepository.PaletteSize, sprite.Image.PaletteBytes.Length);
        Assert.NotEmpty(sprite.Image.PixelIndices);
    }

    [Fact]
    public void TileCodec_RoundTrips4BppPackedPixels()
    {
        byte[] packed = [0x21, 0x43, 0x65, 0x87];

        var unpacked = TileImageCodec.Split4BppTiles(packed);
        var repacked = TileImageCodec.Pack4BppTiles(unpacked);

        Assert.Equal(packed, repacked);
    }

    [Fact]
    public void Lz77_WrapUncompressedRoundTripsWithDecompressor()
    {
        byte[] original = [1, 2, 3, 4, 5, 6, 7, 8, 9];

        var compressed = GbaLz77.WrapUncompressed(original);
        var decompressed = GbaLz77.Decompress(compressed, 0);

        Assert.NotNull(decompressed);
        Assert.Equal(original, decompressed);
    }

    [Fact]
    public void Lz77_CompressRoundTripsWithDecompressor()
    {
        byte[] original = Enumerable.Repeat((byte)0x12, 64)
            .Concat(Enumerable.Repeat((byte)0x34, 64))
            .Concat(Enumerable.Repeat((byte)0x12, 64))
            .ToArray();

        var compressed = GbaLz77.Compress(original);
        var decompressed = GbaLz77.Decompress(compressed, 0);

        Assert.NotNull(decompressed);
        Assert.Equal(original, decompressed);
    }

    [Fact]
    public void Lz77_ReportsEncodedLengthForCompressedStreams()
    {
        byte[] original = [1, 2, 3, 1, 2, 3, 1, 2, 3, 4, 4, 4, 4, 4];

        var compressed = GbaLz77.Compress(original);
        var wrapped = GbaLz77.WrapUncompressed(original);
        var encodedLength = GbaLz77.TryGetEncodedLength(compressed, 0);
        var wrappedLength = GbaLz77.TryGetEncodedLength(wrapped, 0);

        Assert.NotNull(encodedLength);
        Assert.NotNull(wrappedLength);
        Assert.InRange(encodedLength!.Value, 4, compressed.Length);
        Assert.InRange(wrappedLength!.Value, 4, wrapped.Length);
    }

    private static string FindWorkspaceRom()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "Medabots Rokusho Version (E).gba");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not find the local Rokusho ROM used for integration testing.");
    }
}
