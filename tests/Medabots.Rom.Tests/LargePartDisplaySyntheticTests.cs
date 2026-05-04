using Medabots.Rom.Compression;
using Medabots.Rom.Images;
using Medabots.Rom.Metadata;
using Medabots.Rom.Parts;
using Xunit;

namespace Medabots.Rom.Tests;

public sealed class LargePartDisplaySyntheticTests
{
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
        LargePartDisplayTestHelpers.WritePointer(romBytes, MedabotsRomSchema.CompositePreviewDescriptorPointerTableOffset + (7 * sizeof(uint)), 0x1000);
        LargePartDisplayTestHelpers.WritePointer(romBytes, 0x1000, 0x1100);
        BitConverter.GetBytes(0).CopyTo(romBytes, 0x1004);
        BitConverter.GetBytes(0).CopyTo(romBytes, 0x1008);
        romBytes[0x1012] = 0x20;
        romBytes[0x1013] = 0x20;
        romBytes[0x1014] = 0x11;
        LargePartDisplayTestHelpers.WritePointer(romBytes, 0x1100, 0x1200);
        LargePartDisplayTestHelpers.WritePointer(romBytes, 0x1104, 0x1220);
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
        LargePartDisplayTestHelpers.WritePointer(baseRomBytes, MedabotsRomSchema.CompositePreviewDescriptorPointerTableOffset + (7 * sizeof(uint)), 0x1000);
        LargePartDisplayTestHelpers.WritePointer(baseRomBytes, 0x1000, 0x1100);
        baseRomBytes[0x1012] = 0x20;
        baseRomBytes[0x1013] = 0x20;
        baseRomBytes[0x1014] = 0x11;
        LargePartDisplayTestHelpers.WritePointer(baseRomBytes, 0x1100, 0x1200);
        LargePartDisplayTestHelpers.WritePointer(baseRomBytes, 0x1104, 0x1220);
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
        asset.Pieces[0].PaletteBytes[2] ^= 0x1F;
        asset.Pieces[0].Image.PaletteBytes[2] ^= 0x1F;

        patcher.ApplyLargePartDisplaySmart(workingSession, asset, 0x800000);

        var exportedSession = RomHackSession.FromRomFile(new RomFile("exported-large-display.gba", baseRomBytes.ToArray()));
        exportedSession.ApplyPatches(workingSession.AppliedActions);
        var reread = repository.ReadLargePartDisplay(exportedSession.RomFile, part);

        Assert.Equal(asset.Pieces[0].Image.PixelIndices, reread.Pieces[0].Image.PixelIndices);
        Assert.Equal(asset.Pieces[0].PaletteBytes, reread.Pieces[0].PaletteBytes);
    }

    [Fact]
    public void ImageRepository_ReadLargePartDisplay_UsesAllNonRedundantMappedRootDescriptorsFromAppearanceRow()
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

        LargePartDisplayTestHelpers.WritePointer(romBytes, MedabotsRomSchema.CompositePreviewDescriptorPointerTableOffset + (0x11 * sizeof(uint)), 0x1000);
        LargePartDisplayTestHelpers.WritePointer(romBytes, MedabotsRomSchema.CompositePreviewDescriptorPointerTableOffset + (0x12 * sizeof(uint)), 0x1020);
        LargePartDisplayTestHelpers.WritePointer(romBytes, 0x1000, 0x1100);
        LargePartDisplayTestHelpers.WritePointer(romBytes, 0x1020, 0x1140);
        romBytes[0x1012] = 0x20;
        romBytes[0x1013] = 0x20;
        romBytes[0x1014] = 0x11;
        romBytes[0x1032] = 0x20;
        romBytes[0x1033] = 0x20;
        romBytes[0x1034] = 0x11;
        BitConverter.GetBytes(0x20).CopyTo(romBytes, 0x1024);

        LargePartDisplayTestHelpers.WritePointer(romBytes, 0x1108, 0x1200);
        LargePartDisplayTestHelpers.WritePointer(romBytes, 0x110C, 0x1220);
        LargePartDisplayTestHelpers.WritePointer(romBytes, 0x1148, 0x1240);
        LargePartDisplayTestHelpers.WritePointer(romBytes, 0x114C, 0x1260);

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
        LargePartDisplayTestHelpers.WritePointer(romBytes, MedabotsRomSchema.CompositePreviewDescriptorPointerTableOffset + (0x0E * sizeof(uint)), 0x1000);
        LargePartDisplayTestHelpers.WritePointer(romBytes, 0x1000, 0x1100);
        romBytes[0x1012] = 0x20;
        romBytes[0x1013] = 0x20;
        romBytes[0x1014] = 0x11;
        LargePartDisplayTestHelpers.WritePointer(romBytes, 0x1108, 0x1200);
        LargePartDisplayTestHelpers.WritePointer(romBytes, 0x110C, 0x1220);
        LargePartDisplayTestHelpers.WritePointer(romBytes, 0x1110, 0x1240);
        LargePartDisplayTestHelpers.WritePointer(romBytes, 0x1114, 0x1260);
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
    public void ImageRepository_ReadLargePartDisplayDescriptorRecords_ParsesRawBytesAndBothArmVariants()
    {
        var romBytes = new byte[0x540000];
        var selectorEntry = (uint)(0x0E | (1 << 6) | (1 << 15));
        BitConverter.GetBytes(selectorEntry).CopyTo(romBytes, MedabotsRomSchema.CompositePreviewLeftArmAppearanceTableOffset);

        LargePartDisplayTestHelpers.WritePointer(romBytes, MedabotsRomSchema.CompositePreviewDescriptorPointerTableOffset + (0x0E * sizeof(uint)), 0x1000);
        LargePartDisplayTestHelpers.WritePointer(romBytes, 0x1000, 0x1100);
        BitConverter.GetBytes(-16).CopyTo(romBytes, 0x1004);
        BitConverter.GetBytes(24).CopyTo(romBytes, 0x1008);
        romBytes[0x100C] = 0xAA;
        romBytes[0x100D] = 0xBB;
        romBytes[0x100E] = 0xCC;
        romBytes[0x100F] = 0x00;
        romBytes[0x1010] = 0x00;
        romBytes[0x1011] = 0x05;
        romBytes[0x1012] = 0x20;
        romBytes[0x1013] = 0x40;
        romBytes[0x1014] = 0x21;
        romBytes[0x1015] = 0xDD;
        romBytes[0x1016] = 0xEE;
        romBytes[0x1017] = 0xFF;
        LargePartDisplayTestHelpers.WritePointer(romBytes, 0x1108, 0x1200);
        LargePartDisplayTestHelpers.WritePointer(romBytes, 0x110C, 0x1220);
        LargePartDisplayTestHelpers.WritePointer(romBytes, 0x1110, 0x1240);
        LargePartDisplayTestHelpers.WritePointer(romBytes, 0x1114, 0x1260);

        var repository = new ImageAssetRepository();
        var rom = new RomFile("large-display-descriptor-records.gba", romBytes);
        var part = new PartDefinition(2, 0, PartKind.LeftArm, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        var records = repository.ReadLargePartDisplayDescriptorRecords(rom, part, 1);

        var record = Assert.Single(records);
        Assert.Equal(0x1000, record.RecordOffset);
        Assert.Equal(0x3D1D98 + (0x0E * sizeof(uint)), record.DescriptorPointerOffset);
        Assert.Equal(0x1100, record.BlobPointerTableOffset);
        Assert.Equal(-16, record.RawX);
        Assert.Equal(24, record.RawY);
        Assert.Equal(16, record.X);
        Assert.Equal(-24, record.Y);
        Assert.Equal((byte)0xAA, record.RawByte0C);
        Assert.Equal((byte)0xBB, record.RawByte0D);
        Assert.Equal((byte)0xCC, record.RawByte0E);
        Assert.Equal((byte)0xDD, record.RawByte15);
        Assert.Equal((byte)0xEE, record.RawByte16);
        Assert.Equal((byte)0xFF, record.RawByte17);
        Assert.Equal(1, record.WidthDivisor);
        Assert.Equal(2, record.HeightDivisor);
        Assert.Equal(32, record.EffectiveWidth);
        Assert.Equal(32, record.EffectiveHeight);
        Assert.Equal(2, record.VariantResolutions.Count);
        Assert.Equal(0x1200, record.VariantResolutions.Single(entry => entry.VariantSelector == 0).ImageOffset);
        Assert.Equal(0x1240, record.VariantResolutions.Single(entry => entry.VariantSelector == 1).ImageOffset);
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

        LargePartDisplayTestHelpers.WritePointer(romBytes, MedabotsRomSchema.CompositePreviewDescriptorPointerTableOffset + (0x0E * sizeof(uint)), 0x1000);
        LargePartDisplayTestHelpers.WritePointer(romBytes, MedabotsRomSchema.CompositePreviewDescriptorPointerTableOffset + (0x0F * sizeof(uint)), 0x1020);
        LargePartDisplayTestHelpers.WritePointer(romBytes, 0x1000, 0x1100);
        LargePartDisplayTestHelpers.WritePointer(romBytes, 0x1020, 0x1140);
        romBytes[0x1012] = 0x20;
        romBytes[0x1013] = 0x20;
        romBytes[0x1014] = 0x11;
        romBytes[0x1032] = 0x20;
        romBytes[0x1033] = 0x20;
        romBytes[0x1034] = 0x11;
        LargePartDisplayTestHelpers.WritePointer(romBytes, 0x1108, 0x1200);
        LargePartDisplayTestHelpers.WritePointer(romBytes, 0x110C, 0x1220);
        LargePartDisplayTestHelpers.WritePointer(romBytes, 0x1148, 0x1240);
        LargePartDisplayTestHelpers.WritePointer(romBytes, 0x114C, 0x1260);
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

        LargePartDisplayTestHelpers.WritePointer(romBytes, MedabotsRomSchema.CompositePreviewDescriptorPointerTableOffset + (7 * sizeof(uint)), 0x1000);
        LargePartDisplayTestHelpers.WritePointer(romBytes, MedabotsRomSchema.CompositePreviewDescriptorPointerTableOffset + (8 * sizeof(uint)), 0x1020);
        LargePartDisplayTestHelpers.WritePointer(romBytes, 0x1000, 0x1100);
        LargePartDisplayTestHelpers.WritePointer(romBytes, 0x1020, 0x1140);
        romBytes[0x1012] = 0x10;
        romBytes[0x1013] = 0x10;
        romBytes[0x1014] = 0x11;
        romBytes[0x1032] = 0x20;
        romBytes[0x1033] = 0x20;
        romBytes[0x1034] = 0x11;
        LargePartDisplayTestHelpers.WritePointer(romBytes, 0x1108, 0x1200);
        LargePartDisplayTestHelpers.WritePointer(romBytes, 0x110C, 0x1220);
        LargePartDisplayTestHelpers.WritePointer(romBytes, 0x1148, 0x1240);
        LargePartDisplayTestHelpers.WritePointer(romBytes, 0x114C, 0x1240);
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
        Assert.Equal(0x1220, asset.Pieces.Single(piece => piece.DescriptorId == 7).PaletteOffset);
        Assert.Equal(0, asset.Pieces.Single(piece => piece.DescriptorId == 8).PaletteOffset);
    }
}
