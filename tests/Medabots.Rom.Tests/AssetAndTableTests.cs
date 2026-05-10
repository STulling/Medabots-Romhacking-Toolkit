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
        var rom = await RomFile.LoadAsync(TestRomLocator.FindWorkspaceRom());
        var encounters = new EncounterTableReader().ReadAll(rom);

        Assert.Equal(EncounterTableReader.EncounterCount, encounters.Count);
        Assert.Contains(encounters, encounter => encounter.Battle1 != 0 || encounter.Battle2 != 0 || encounter.Battle3 != 0 || encounter.Battle4 != 0);
    }

    [Fact]
    public async Task StarterReader_LoadsRealStarterData()
    {
        var rom = await RomFile.LoadAsync(TestRomLocator.FindWorkspaceRom());
        var profile = MedabotsRomTextProfiles.Detect(rom);

        Assert.NotNull(profile);

        var starter = new StarterReader().Read(rom, profile!);

        Assert.InRange(starter.PartId, 0, 0x77);
        Assert.InRange(starter.MedalId, 0, 0xFF);
    }

    [Fact]
    public async Task ShopReader_LocatesShopTable()
    {
        var rom = await RomFile.LoadAsync(TestRomLocator.FindWorkspaceRom());
        var reader = new ShopTableReader();
        var shop = reader.Read(rom, 0);
        var shops = reader.ReadAll(rom);

        Assert.Equal(0, shop.Id);
        Assert.Equal(ShopTableReader.ShopEntrySize, shop.Contents.Length);
        Assert.Equal([0x13, 0x00, 0xFF, 0xFF], shop.Contents);
        Assert.Equal(ShopTableReader.ShopCount, shops.Count);
        Assert.All(shops, entry => Assert.Equal(ShopTableReader.ShopEntrySize, entry.Contents.Length));
    }

    [Fact]
    public async Task ImageRepository_ReadsRealPortraitAndSpriteAssets()
    {
        var rom = await RomFile.LoadAsync(TestRomLocator.FindWorkspaceRom());
        var repository = new ImageAssetRepository();

        var portrait = repository.ReadPortrait(rom, 0, 0);
        var sprite = repository.ReadSprite(rom, 0);
        var composite = repository.ReadBattleCompositeSpriteComponent(rom, 0, 0);

        Assert.Equal(ImageAssetRepository.PaletteSize, portrait.Image.PaletteBytes.Length);
        Assert.NotEmpty(portrait.Image.PixelIndices);
        Assert.Equal(ImageAssetRepository.PaletteSize, sprite.Image.PaletteBytes.Length);
        Assert.NotEmpty(sprite.Image.PixelIndices);
        Assert.Equal(ImageAssetRepository.PaletteSize, composite.Image.PaletteBytes.Length);
        Assert.NotEmpty(composite.Image.PixelIndices);
        Assert.InRange(composite.AppearanceId, 0, 0x3F);
        Assert.True(composite.PalettePointerOffset > 0);
        Assert.True(composite.PaletteOffset > 0);
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

    [Fact]
    public void Malias2_CompressRoundTripsWithDecompressor()
    {
        byte[] original = Enumerable.Range(0, 0x190)
            .Select(index => (byte)((index * 7) & 0xFF))
            .ToArray();

        var compressed = Malias2.Compress(original);
        var decompressed = Malias2.Decompress(compressed, 0);

        Assert.NotNull(decompressed);
        Assert.Equal(original, decompressed);
    }

    [Fact]
    public void Malias2_ReportsEncodedLengthForCompressedStreams()
    {
        byte[] original = Enumerable.Range(0, 0x90)
            .Select(index => (byte)((index * 5) & 0xFF))
            .ToArray();

        var compressed = Malias2.Compress(original);
        var encodedLength = Malias2.TryGetEncodedLength(compressed, 0);

        Assert.NotNull(encodedLength);
        Assert.Equal(compressed.Length, encodedLength);
    }

    [Fact]
    public void PortraitPatcher_ApplyPortraitSmart_RelocatesWithoutCollidingImageAndPalette()
    {
        var romBytes = new byte[0x400];
        var originalPacked = Enumerable.Range(0, 0x20).Select(index => (byte)index).ToArray();
        var originalCompressed = Malias2.Compress(originalPacked);
        Array.Copy(originalCompressed, 0, romBytes, 0x100, originalCompressed.Length);

        var rom = new RomFile("test.gba", romBytes);
        var session = RomHackSession.FromRomFile(rom);
        var patcher = new ImageAssetPatcher();

        var editedPixels = Enumerable.Range(0, 0x190).Select(index => (byte)(index & 0x0F)).ToArray();
        var palette = Enumerable.Range(0, ImageAssetRepository.PaletteSize).Select(index => (byte)index).ToArray();
        var asset = new PortraitAsset(
            CharacterId: 0,
            PortraitIndex: 0,
            ImagePointerOffset: 0x20,
            PalettePointerOffset: 0x24,
            ImageOffset: 0x100,
            PaletteOffset: 0,
            Image: new IndexedImage(MedabotsRomSchema.PortraitTileWidth, editedPixels.Length / 0x40 / MedabotsRomSchema.PortraitTileWidth, editedPixels, palette));

        patcher.ApplyPortraitSmart(session, asset, 0x800);

        Assert.True(GbaPointer.TryReadFileOffset(session.RomFile.Data, asset.ImagePointerOffset, out var imageOffset));
        Assert.True(GbaPointer.TryReadFileOffset(session.RomFile.Data, asset.PalettePointerOffset, out var paletteOffset));
        Assert.True(imageOffset >= 0x800);
        Assert.True(paletteOffset >= imageOffset + Malias2.Compress(TileImageCodec.Pack4BppTiles(editedPixels)).Length);
        Assert.Equal((byte)'L', session.RomFile.Data[imageOffset]);
        Assert.Equal((byte)'e', session.RomFile.Data[imageOffset + 1]);
        Assert.Equal(palette, session.RomFile.ReadBytes(paletteOffset, palette.Length).ToArray());
    }

    [Fact]
    public void PortraitPatcher_AppliedActions_ReplayEditedPortraitIntoFreshRom()
    {
        var baseRomBytes = new byte[0x3B2000];
        var sourcePalette = Enumerable.Range(0, ImageAssetRepository.PaletteSize).Select(index => (byte)(index ^ 0x55)).ToArray();
        var sourcePixels = Enumerable.Range(0, 0x190).Select(index => (byte)(index & 0x0F)).ToArray();
        var sourceCompressed = Malias2.Compress(TileImageCodec.Pack4BppTiles(sourcePixels));

        LargePartDisplayTestHelpers.WritePointer(baseRomBytes, MedabotsRomSchema.PortraitPointerTableOffset, 0x200000);
        LargePartDisplayTestHelpers.WritePointer(baseRomBytes, MedabotsRomSchema.PortraitPaletteTableOffset, 0x210000);
        Array.Copy(sourceCompressed, 0, baseRomBytes, 0x200000, sourceCompressed.Length);
        Array.Copy(sourcePalette, 0, baseRomBytes, 0x210000, sourcePalette.Length);

        var workingSession = RomHackSession.FromRomFile(new RomFile("working.gba", baseRomBytes.ToArray()));
        var repository = new ImageAssetRepository();
        var patcher = new ImageAssetPatcher();

        var portrait = repository.ReadPortrait(workingSession.RomFile, 0, 0);
        portrait.Image.PixelIndices[0] = (byte)((portrait.Image.PixelIndices[0] + 3) & 0x0F);
        portrait.Image.PaletteBytes[2] ^= 0x1F;

        patcher.ApplyPortraitSmart(workingSession, portrait, 0x800000);

        var exportedSession = RomHackSession.FromRomFile(new RomFile("exported.gba", baseRomBytes.ToArray()));
        exportedSession.ApplyPatches(workingSession.AppliedActions);
        var reread = repository.ReadPortrait(exportedSession.RomFile, 0, 0);

        Assert.Equal(portrait.Image.PixelIndices, reread.Image.PixelIndices);
        Assert.Equal(portrait.Image.PaletteBytes, reread.Image.PaletteBytes);
    }

    [Fact]
    public void BattleCompositePatcher_AppliedActions_ReplayEditedComponentIntoFreshRom()
    {
        var baseRomBytes = new byte[0x510000];
        var sourcePalette = Enumerable.Range(0, ImageAssetRepository.PaletteSize).Select(index => (byte)(index ^ 0x33)).ToArray();
        var alternatePalette = Enumerable.Range(0, ImageAssetRepository.PaletteSize).Select(index => (byte)(index ^ 0x77)).ToArray();
        var sourcePixels = Enumerable.Range(0, 0x80).Select(index => (byte)(index & 0x0F)).ToArray();
        var sourceCompressed = Malias2.Compress(TileImageCodec.Pack4BppTiles(sourcePixels));

        LargePartDisplayTestHelpers.WritePointer(baseRomBytes, MedabotsRomSchema.CompositeBattleSpritePointerTableOffset, 0x200000);
        baseRomBytes[MedabotsRomSchema.CompositeBattleSpritePaletteFamilyTableOffset] = 0;
        Array.Copy(sourceCompressed, 0, baseRomBytes, 0x200000, sourceCompressed.Length);
        Array.Copy(sourcePalette, 0, baseRomBytes, MedabotsRomSchema.PartSelectionComponentPaletteSetOffset, sourcePalette.Length);
        Array.Copy(alternatePalette, 0, baseRomBytes, MedabotsRomSchema.PartSelectionComponentPaletteSetOffset + ImageAssetRepository.PaletteSize, alternatePalette.Length);

        var workingSession = RomHackSession.FromRomFile(new RomFile("working.gba", baseRomBytes.ToArray()));
        var repository = new ImageAssetRepository();
        var patcher = new ImageAssetPatcher();

        var component = repository.ReadBattleCompositeSpriteComponent(workingSession.RomFile, 0, 0);
        component.Image.PixelIndices[0] = (byte)((component.Image.PixelIndices[0] + 5) & 0x0F);
        component = component with
        {
            PaletteFamily = 1,
            PaletteOffset = MedabotsRomSchema.PartSelectionComponentPaletteSetOffset + ImageAssetRepository.PaletteSize,
            PaletteSelector = 5,
            Image = component.Image with { PaletteBytes = alternatePalette.ToArray() }
        };

        patcher.ApplyBattleCompositeSpriteComponentSmart(workingSession, component, 0x800000);

        var exportedSession = RomHackSession.FromRomFile(new RomFile("exported.gba", baseRomBytes.ToArray()));
        exportedSession.ApplyPatches(workingSession.AppliedActions);
        var reread = repository.ReadBattleCompositeSpriteComponent(exportedSession.RomFile, 0, 0);

        Assert.Equal(component.Image.PixelIndices, reread.Image.PixelIndices);
        Assert.Equal(component.Image.PaletteBytes, reread.Image.PaletteBytes);
        Assert.Equal(component.PaletteFamily, reread.PaletteFamily);
    }
}
