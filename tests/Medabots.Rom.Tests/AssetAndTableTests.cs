using Medabots.Rom.Compression;
using Medabots.Rom.Encounters;
using Medabots.Rom.Images;
using Medabots.Rom.Metadata;
using Medabots.Rom.Parts;
using Medabots.Rom.Shops;
using Medabots.Rom.Starter;
using System.Security.Cryptography;
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

        WritePointer(baseRomBytes, MedabotsRomSchema.PortraitPointerTableOffset, 0x200000);
        WritePointer(baseRomBytes, MedabotsRomSchema.PortraitPaletteTableOffset, 0x210000);
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

        WritePointer(baseRomBytes, MedabotsRomSchema.CompositeBattleSpritePointerTableOffset, 0x200000);
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

    [Fact]
    public void ImageRepository_ReadLargePartDisplay_ResolvesDescriptorDrivenPiece()
    {
        var romBytes = new byte[0x510000];
        var palette = Enumerable.Range(0, ImageAssetRepository.PaletteSize).Select(index => (byte)index).ToArray();
        var packedPixels = Enumerable.Range(0, 0x80).Select(index => (byte)(index & 0x0F)).ToArray();
        var compressed = GbaLz77.Compress(TileImageCodec.Pack4BppTiles(packedPixels));

        romBytes[MedabotsRomSchema.CompositeBattleSpritePaletteFamilyTableOffset] = 0;
        Array.Copy(palette, 0, romBytes, 0x1220, palette.Length);

        romBytes[MedabotsRomSchema.CompositePreviewHeadAppearanceTableOffset] = 0x07;
        WritePointer(romBytes, MedabotsRomSchema.CompositePreviewDescriptorPointerTableOffset + (7 * sizeof(uint)), 0x1000);
        WritePointer(romBytes, 0x1000, 0x1100);
        BitConverter.GetBytes(0).CopyTo(romBytes, 0x1004);
        BitConverter.GetBytes(0).CopyTo(romBytes, 0x1008);
        romBytes[0x1012] = 0x20;
        romBytes[0x1013] = 0x20;
        romBytes[0x1014] = 0x11;
        WritePointer(romBytes, 0x1100, 0x1200);
        WritePointer(romBytes, 0x1104, 0x1220);
        Array.Copy(compressed, 0, romBytes, 0x1200, compressed.Length);

        var repository = new ImageAssetRepository();
        var rom = new RomFile("large-display.gba", romBytes);
        var part = new PartDefinition(0, 0, PartKind.Head, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        var asset = repository.ReadLargePartDisplay(rom, part);

        Assert.Equal(7, asset.RootDescriptorId);
        Assert.Contains(8, asset.InitialPaletteBanks.Keys);
        Assert.Single(asset.Pieces);
        Assert.Equal(0x1100, asset.Pieces[0].ImagePointerOffset);
        Assert.Equal(0x1104, asset.Pieces[0].PalettePointerOffset);
        Assert.Equal(0x1200, asset.Pieces[0].ImageOffset);
        Assert.Equal(0x20, asset.Pieces[0].Image.Width);
        Assert.Equal(0x20, asset.Pieces[0].Image.Height);
        Assert.Equal(palette, asset.Pieces[0].PaletteBytes);
    }

    [Fact]
    public void LargePartDisplayPatcher_AppliedActions_ReplayEditedLargeDisplayIntoFreshRom()
    {
        var baseRomBytes = new byte[0x900000];
        var palette = Enumerable.Range(0, ImageAssetRepository.PaletteSize).Select(index => (byte)index).ToArray();
        var sourcePixels = Enumerable.Range(0, 0x80).Select(index => (byte)(index & 0x0F)).ToArray();
        var sourceCompressed = GbaLz77.Compress(TileImageCodec.Pack4BppTiles(sourcePixels));

        baseRomBytes[MedabotsRomSchema.CompositePreviewHeadAppearanceTableOffset] = 0x07;
        WritePointer(baseRomBytes, MedabotsRomSchema.CompositePreviewDescriptorPointerTableOffset + (7 * sizeof(uint)), 0x1000);
        WritePointer(baseRomBytes, 0x1000, 0x1100);
        baseRomBytes[0x1012] = 0x20;
        baseRomBytes[0x1013] = 0x20;
        baseRomBytes[0x1014] = 0x11;
        WritePointer(baseRomBytes, 0x1100, 0x1200);
        WritePointer(baseRomBytes, 0x1104, 0x1220);
        Array.Copy(sourceCompressed, 0, baseRomBytes, 0x1200, sourceCompressed.Length);
        Array.Copy(palette, 0, baseRomBytes, 0x1220, palette.Length);
        Array.Copy(palette, 0, baseRomBytes, MedabotsRomSchema.PartDetailObjPaletteBlockAOffset, palette.Length);
        Array.Copy(palette, 0, baseRomBytes, MedabotsRomSchema.PartDetailObjPaletteBlockBOffset, palette.Length);
        Array.Copy(palette, 0, baseRomBytes, MedabotsRomSchema.PartDetailObjPaletteBlockCOffset, palette.Length);

        var repository = new ImageAssetRepository();
        var patcher = new ImageAssetPatcher();
        var part = new PartDefinition(0, 0, PartKind.Head, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        var workingSession = RomHackSession.FromRomFile(new RomFile("working-large-display.gba", baseRomBytes.ToArray()));
        var asset = repository.ReadLargePartDisplay(workingSession.RomFile, part);
        asset.Pieces[0].Image.PixelIndices[0] = (byte)((asset.Pieces[0].Image.PixelIndices[0] + 5) & 0x0F);

        patcher.ApplyLargePartDisplaySmart(workingSession, asset, 0x800000);

        var exportedSession = RomHackSession.FromRomFile(new RomFile("exported-large-display.gba", baseRomBytes.ToArray()));
        exportedSession.ApplyPatches(workingSession.AppliedActions);
        var reread = repository.ReadLargePartDisplay(exportedSession.RomFile, part);

        Assert.Equal(asset.Pieces[0].Image.PixelIndices, reread.Pieces[0].Image.PixelIndices);
    }

    [Fact]
    public void ImageRepository_ReadLargePartDisplay_ResolvesMultipleRootDescriptorsFromAppearanceRow()
    {
        var romBytes = new byte[0x520000];
        var paletteA = Enumerable.Range(0, ImageAssetRepository.PaletteSize).Select(index => (byte)index).ToArray();
        var paletteB = Enumerable.Range(0, ImageAssetRepository.PaletteSize).Select(index => (byte)(0x80 + index)).ToArray();
        var packedPixelsA = Enumerable.Repeat((byte)1, 0x80).ToArray();
        var packedPixelsB = Enumerable.Repeat((byte)2, 0x80).ToArray();
        var compressedA = GbaLz77.Compress(TileImageCodec.Pack4BppTiles(packedPixelsA));
        var compressedB = GbaLz77.Compress(TileImageCodec.Pack4BppTiles(packedPixelsB));

        romBytes[MedabotsRomSchema.CompositePreviewRightArmAppearanceTableOffset] = 0x51;
        romBytes[MedabotsRomSchema.CompositePreviewRightArmAppearanceTableOffset + 4] = 0x52;

        WritePointer(romBytes, MedabotsRomSchema.CompositePreviewDescriptorPointerTableOffset + (0x11 * sizeof(uint)), 0x1000);
        WritePointer(romBytes, MedabotsRomSchema.CompositePreviewDescriptorPointerTableOffset + (0x12 * sizeof(uint)), 0x1020);
        WritePointer(romBytes, 0x1000, 0x1100);
        WritePointer(romBytes, 0x1020, 0x1140);
        romBytes[0x1012] = 0x20;
        romBytes[0x1013] = 0x20;
        romBytes[0x1014] = 0x11;
        romBytes[0x1032] = 0x20;
        romBytes[0x1033] = 0x20;
        romBytes[0x1034] = 0x11;
        BitConverter.GetBytes(0x20).CopyTo(romBytes, 0x1024);

        WritePointer(romBytes, 0x1108, 0x1200);
        WritePointer(romBytes, 0x110C, 0x1220);
        WritePointer(romBytes, 0x1148, 0x1240);
        WritePointer(romBytes, 0x114C, 0x1260);

        Array.Copy(compressedA, 0, romBytes, 0x1200, compressedA.Length);
        Array.Copy(paletteA, 0, romBytes, 0x1220, paletteA.Length);
        Array.Copy(compressedB, 0, romBytes, 0x1240, compressedB.Length);
        Array.Copy(paletteB, 0, romBytes, 0x1260, paletteB.Length);

        var repository = new ImageAssetRepository();
        var rom = new RomFile("large-display-multi.gba", romBytes);
        var part = new PartDefinition(1, 0, PartKind.RightArm, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        var asset = repository.ReadLargePartDisplay(rom, part);

        Assert.Equal(2, asset.Pieces.Count);
        Assert.Contains(asset.Pieces, piece => piece.ImageOffset == 0x1200);
        Assert.Contains(asset.Pieces, piece => piece.ImageOffset == 0x1240);
    }

    [Fact]
    public void ImageRepository_ReadLargePartDisplay_UsesVariantSelectorBitForLeftArm()
    {
        var romBytes = new byte[0x540000];
        var palette = Enumerable.Range(0, ImageAssetRepository.PaletteSize).Select(index => (byte)index).ToArray();
        var packedPixelsA = Enumerable.Repeat((byte)3, 0x80).ToArray();
        var packedPixelsB = Enumerable.Repeat((byte)7, 0x80).ToArray();
        var compressedA = GbaLz77.Compress(TileImageCodec.Pack4BppTiles(packedPixelsA));
        var compressedB = GbaLz77.Compress(TileImageCodec.Pack4BppTiles(packedPixelsB));

        var selectorEntry = (uint)(0x0E | (1 << 6) | (1 << 15));
        BitConverter.GetBytes(selectorEntry).CopyTo(romBytes, MedabotsRomSchema.CompositePreviewLeftArmAppearanceTableOffset);
        WritePointer(romBytes, MedabotsRomSchema.CompositePreviewDescriptorPointerTableOffset + (0x0E * sizeof(uint)), 0x1000);
        WritePointer(romBytes, 0x1000, 0x1100);
        romBytes[0x1012] = 0x20;
        romBytes[0x1013] = 0x20;
        romBytes[0x1014] = 0x11;
        WritePointer(romBytes, 0x1108, 0x1200);
        WritePointer(romBytes, 0x110C, 0x1220);
        WritePointer(romBytes, 0x1110, 0x1240);
        WritePointer(romBytes, 0x1114, 0x1260);
        Array.Copy(compressedA, 0, romBytes, 0x1200, compressedA.Length);
        Array.Copy(compressedB, 0, romBytes, 0x1240, compressedB.Length);
        Array.Copy(palette, 0, romBytes, 0x1220, palette.Length);
        Array.Copy(palette, 0, romBytes, 0x1260, palette.Length);
        Array.Copy(palette, 0, romBytes, MedabotsRomSchema.PartDetailObjPaletteBlockAOffset, palette.Length);
        Array.Copy(palette, 0, romBytes, MedabotsRomSchema.PartDetailObjPaletteBlockBOffset, palette.Length);
        Array.Copy(palette, 0, romBytes, MedabotsRomSchema.PartDetailObjPaletteBlockCOffset, palette.Length);

        var repository = new ImageAssetRepository();
        var rom = new RomFile("large-display-left-arm-variant.gba", romBytes);
        var part = new PartDefinition(2, 0, PartKind.LeftArm, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        var asset = repository.ReadLargePartDisplay(rom, part);

        Assert.Single(asset.Pieces);
        Assert.Equal(0x1240, asset.Pieces[0].ImageOffset);
        Assert.All(asset.Pieces[0].Image.PixelIndices.Take(0x80), pixel => Assert.Equal((byte)7, pixel));
    }

    [Fact]
    public void ImageRepository_ReadLargePartDisplay_LeftArmOverlayPassOverwritesTargetPieceTiles()
    {
        var romBytes = new byte[0x560000];
        var palette = Enumerable.Range(0, ImageAssetRepository.PaletteSize).Select(index => (byte)(index + 1)).ToArray();
        var basePixels = Enumerable.Repeat((byte)1, 0x80).ToArray();
        var overlayPixels = Enumerable.Repeat((byte)9, 0x80).ToArray();
        var baseCompressed = GbaLz77.Compress(TileImageCodec.Pack4BppTiles(basePixels));
        var overlayCompressed = GbaLz77.Compress(TileImageCodec.Pack4BppTiles(overlayPixels));

        BitConverter.GetBytes((uint)0x4E).CopyTo(romBytes, MedabotsRomSchema.CompositePreviewLeftArmAppearanceTableOffset);
        BitConverter.GetBytes((uint)0x4F).CopyTo(romBytes, MedabotsRomSchema.CompositePreviewRightArmAppearanceTableOffset);

        WritePointer(romBytes, MedabotsRomSchema.CompositePreviewDescriptorPointerTableOffset + (0x0E * sizeof(uint)), 0x1000);
        WritePointer(romBytes, MedabotsRomSchema.CompositePreviewDescriptorPointerTableOffset + (0x0F * sizeof(uint)), 0x1020);
        WritePointer(romBytes, 0x1000, 0x1100);
        WritePointer(romBytes, 0x1020, 0x1140);
        romBytes[0x1012] = 0x20;
        romBytes[0x1013] = 0x20;
        romBytes[0x1014] = 0x11;
        romBytes[0x1032] = 0x20;
        romBytes[0x1033] = 0x20;
        romBytes[0x1034] = 0x11;
        WritePointer(romBytes, 0x1108, 0x1200);
        WritePointer(romBytes, 0x110C, 0x1220);
        WritePointer(romBytes, 0x1148, 0x1240);
        WritePointer(romBytes, 0x114C, 0x1260);
        Array.Copy(baseCompressed, 0, romBytes, 0x1200, baseCompressed.Length);
        Array.Copy(overlayCompressed, 0, romBytes, 0x1240, overlayCompressed.Length);
        Array.Copy(palette, 0, romBytes, 0x1220, palette.Length);
        Array.Copy(palette, 0, romBytes, 0x1260, palette.Length);
        Array.Copy(palette, 0, romBytes, MedabotsRomSchema.PartDetailObjPaletteBlockAOffset, palette.Length);
        Array.Copy(palette, 0, romBytes, MedabotsRomSchema.PartDetailObjPaletteBlockBOffset, palette.Length);
        Array.Copy(palette, 0, romBytes, MedabotsRomSchema.PartDetailObjPaletteBlockCOffset, palette.Length);

        var repository = new ImageAssetRepository();
        var rom = new RomFile("large-display-left-arm-overlay.gba", romBytes);
        var part = new PartDefinition(2, 0, PartKind.LeftArm, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        var asset = repository.ReadLargePartDisplay(rom, part);

        Assert.Single(asset.Pieces);
        Assert.Equal(0x1200, asset.Pieces[0].ImageOffset);
        Assert.All(asset.Pieces[0].Image.PixelIndices.Take(0x80), pixel => Assert.Equal((byte)9, pixel));
    }

    [Fact]
    public void ImageRepository_ReadLargePartDisplay_IgnoresPaletteUploadWhenPalettePointerEqualsImagePointer()
    {
        var romBytes = new byte[0x540000];
        var initPalette = Enumerable.Range(0, ImageAssetRepository.PaletteSize).Select(index => (byte)(index + 2)).ToArray();
        var uploadedPalette = Enumerable.Range(0, ImageAssetRepository.PaletteSize).Select(index => (byte)(0x80 + index)).ToArray();
        var packedPixelsA = Enumerable.Repeat((byte)4, 0x40).ToArray();
        var packedPixelsB = Enumerable.Repeat((byte)6, 0x100).ToArray();
        var compressedA = GbaLz77.Compress(TileImageCodec.Pack4BppTiles(packedPixelsA));
        var compressedB = GbaLz77.Compress(TileImageCodec.Pack4BppTiles(packedPixelsB));

        romBytes[MedabotsRomSchema.CompositePreviewHeadAppearanceTableOffset] = 0x47;
        romBytes[MedabotsRomSchema.CompositePreviewHeadAppearanceTableOffset + 4] = 0x48;

        WritePointer(romBytes, MedabotsRomSchema.CompositePreviewDescriptorPointerTableOffset + (7 * sizeof(uint)), 0x1000);
        WritePointer(romBytes, MedabotsRomSchema.CompositePreviewDescriptorPointerTableOffset + (8 * sizeof(uint)), 0x1020);
        WritePointer(romBytes, 0x1000, 0x1100);
        WritePointer(romBytes, 0x1020, 0x1140);
        romBytes[0x1012] = 0x10;
        romBytes[0x1013] = 0x10;
        romBytes[0x1014] = 0x11;
        romBytes[0x1032] = 0x20;
        romBytes[0x1033] = 0x20;
        romBytes[0x1034] = 0x11;
        WritePointer(romBytes, 0x1108, 0x1200);
        WritePointer(romBytes, 0x110C, 0x1220);
        WritePointer(romBytes, 0x1148, 0x1240);
        WritePointer(romBytes, 0x114C, 0x1240);
        Array.Copy(compressedA, 0, romBytes, 0x1200, compressedA.Length);
        Array.Copy(compressedB, 0, romBytes, 0x1240, compressedB.Length);
        Array.Copy(uploadedPalette, 0, romBytes, 0x1220, uploadedPalette.Length);
        Array.Copy(initPalette, 0, romBytes, MedabotsRomSchema.PartDetailObjPaletteBlockAOffset, initPalette.Length);
        Array.Copy(initPalette, 0, romBytes, MedabotsRomSchema.PartDetailObjPaletteBlockBOffset, initPalette.Length);
        Array.Copy(initPalette, 0, romBytes, MedabotsRomSchema.PartDetailObjPaletteBlockCOffset, initPalette.Length);

        var repository = new ImageAssetRepository();
        var rom = new RomFile("large-display-head-palette-equals-image.gba", romBytes);
        var part = new PartDefinition(0, 0, PartKind.Head, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        var asset = repository.ReadLargePartDisplay(rom, part);

        Assert.Equal(2, asset.Pieces.Count);
        Assert.Equal(0x1220, asset.Pieces[0].PaletteOffset);
        Assert.Equal(0, asset.Pieces[1].PaletteOffset);
    }

    [Fact]
    public async Task ImageRepository_ReadLargePartDisplay_ExtractsRealRollertankLegs()
    {
        var rom = await RomFile.LoadAsync(FindWorkspaceRom());
        var repository = new ImageAssetRepository();
        var parts = new PartTableReader().ReadAll(rom);

        var asset = repository.ReadLargePartDisplay(rom, parts[375]);

        Assert.Equal(5, asset.Pieces.Count);
        Assert.Equal(new[] { 25, 26, 27, 28, 29 }, asset.Pieces.Select(piece => piece.DescriptorId).ToArray());
        Assert.All(asset.Pieces, piece => Assert.Equal(16, piece.LoadedTileCount));
        Assert.True(asset.Pieces[0].PaletteOffset > 0);
        Assert.All(asset.Pieces.Skip(1), piece => Assert.Equal(0, piece.PaletteOffset));
        Assert.All(asset.Pieces, piece => Assert.Equal(3, piece.PaletteBank));
        Assert.Equal("FB05D277618A9CCD25A81E3ED957CF42C6F8386BD3F1792A6D07C7BFAE3D59F7", ComputeLargePartDisplaySignature(asset));
    }

    [Fact]
    public async Task ImageRepository_ReadLargePartDisplay_ExtractsRealPipoHammerLargeArm()
    {
        var rom = await RomFile.LoadAsync(FindWorkspaceRom());
        var repository = new ImageAssetRepository();
        var parts = new PartTableReader().ReadAll(rom);

        var asset = repository.ReadLargePartDisplay(rom, parts[170]);

        Assert.Equal(2, asset.Pieces.Count);
        Assert.Equal(new[] { 14, 15 }, asset.Pieces.Select(piece => piece.DescriptorId).ToArray());
        Assert.All(asset.Pieces, piece => Assert.Equal(16, piece.LoadedTileCount));
        Assert.True(asset.Pieces[0].PaletteOffset > 0);
        Assert.Equal(0, asset.Pieces[1].PaletteOffset);
        Assert.All(asset.Pieces, piece => Assert.Equal(2, piece.PaletteBank));
        Assert.Equal("0994425EFA39100232493D4DB065EFF6500952A2A53E432741D3331B165CF6FE", ComputeLargePartDisplaySignature(asset));
    }

    [Fact]
    public async Task ImageRepository_ReadLargePartDisplay_ExtractsRealTatackerLegs()
    {
        var rom = await RomFile.LoadAsync(FindWorkspaceRom());
        var repository = new ImageAssetRepository();
        var parts = new PartTableReader().ReadAll(rom);

        var asset = repository.ReadLargePartDisplay(rom, parts[171]);

        Assert.Equal(5, asset.Pieces.Count);
        Assert.Equal(new[] { 31, 32, 33, 34, 35 }, asset.Pieces.Select(piece => piece.DescriptorId).ToArray());
        Assert.Equal("F8EF834E2B7F0865548A35C5B505C5BD22ABFEF57AB97240D694E82996A65475", ComputeLargePartDisplaySignature(asset));
    }

    [Fact]
    public async Task ImageRepository_ReadLargePartDisplay_ExtractsRealDeathcrawlerLegs()
    {
        var rom = await RomFile.LoadAsync(FindWorkspaceRom());
        var repository = new ImageAssetRepository();
        var parts = new PartTableReader().ReadAll(rom);

        var asset = repository.ReadLargePartDisplay(rom, parts[131]);

        Assert.Equal(5, asset.Pieces.Count);
        Assert.Equal(new[] { 45, 46, 47, 48, 49 }, asset.Pieces.Select(piece => piece.DescriptorId).ToArray());
        Assert.Equal("2A0B4A66CF4B2FB87C8E0988950D4CDC0A97ABEC9254658EF7E90009947141FC", ComputeLargePartDisplaySignature(asset));
    }

    private static void WritePointer(byte[] romBytes, int offset, int fileOffset)
    {
        var pointer = BitConverter.GetBytes(GbaPointer.ToRomAddress(fileOffset));
        Array.Copy(pointer, 0, romBytes, offset, pointer.Length);
    }

    private static string ComputeLargePartDisplaySignature(LargePartDisplayAsset asset)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(asset.Pieces.Count);
        foreach (var piece in asset.Pieces)
        {
            writer.Write(piece.DescriptorId);
            writer.Write(piece.PaletteBank);
            writer.Write(piece.LoadedTileCount);
            writer.Write(piece.Image.TileWidth);
            writer.Write(piece.Image.TileHeight);
            writer.Write(piece.Image.PixelIndices.Length);
            writer.Write(piece.Image.PixelIndices);
        }

        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
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
