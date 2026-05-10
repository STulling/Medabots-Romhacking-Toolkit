using Medabots.Rom.Images;
using Medabots.Rom.Parts;
using Xunit;

namespace Medabots.Rom.Tests;

public sealed class LargePartDisplayRomRegressionTests
{
    [Fact]
    public async Task ImageRepository_ReadLargePartDisplay_ExtractsRealRollertankLegs()
    {
        var rom = await RomFile.LoadAsync(TestRomLocator.FindWorkspaceRom());
        var repository = new ImageAssetRepository();
        var parts = new PartTableReader().ReadAll(rom);

        var asset = repository.ReadLargePartDisplay(rom, parts[375]);

        Assert.Equal(5, asset.Pieces.Count);
        Assert.Equal(new[] { 25, 26, 27, 28, 29 }, asset.Pieces.Select(piece => piece.DescriptorId).ToArray());
        Assert.All(asset.Pieces, piece => Assert.Equal(16, piece.LoadedTileCount));
        Assert.True(asset.Pieces[0].PaletteOffset > 0);
        Assert.All(asset.Pieces.Skip(1), piece => Assert.Equal(0, piece.PaletteOffset));
        Assert.All(asset.Pieces, piece => Assert.Equal(3, piece.PaletteBank));
        Assert.Equal("FB05D277618A9CCD25A81E3ED957CF42C6F8386BD3F1792A6D07C7BFAE3D59F7", LargePartDisplayTestHelpers.ComputeSignature(asset));
    }

    [Fact]
    public async Task ImageRepository_ReadLargePartDisplay_ExtractsRealPipoHammerLargeArm()
    {
        var rom = await RomFile.LoadAsync(TestRomLocator.FindWorkspaceRom());
        var repository = new ImageAssetRepository();
        var parts = new PartTableReader().ReadAll(rom);

        var asset = repository.ReadLargePartDisplay(rom, parts[170]);

        Assert.Equal(2, asset.Pieces.Count);
        Assert.Equal(new[] { 14, 15 }, asset.Pieces.Select(piece => piece.DescriptorId).ToArray());
        Assert.All(asset.Pieces, piece => Assert.Equal(16, piece.LoadedTileCount));
        Assert.True(asset.Pieces[0].PaletteOffset > 0);
        Assert.Equal(0, asset.Pieces[1].PaletteOffset);
        Assert.All(asset.Pieces, piece => Assert.Equal(2, piece.PaletteBank));
        Assert.Equal("0994425EFA39100232493D4DB065EFF6500952A2A53E432741D3331B165CF6FE", LargePartDisplayTestHelpers.ComputeSignature(asset));
    }

    [Fact]
    public async Task ImageRepository_ReadLargePartDisplay_ExtractsRealTatackerLegs()
    {
        var rom = await RomFile.LoadAsync(TestRomLocator.FindWorkspaceRom());
        var repository = new ImageAssetRepository();
        var parts = new PartTableReader().ReadAll(rom);

        var asset = repository.ReadLargePartDisplay(rom, parts[171]);

        Assert.Equal(5, asset.Pieces.Count);
        Assert.Equal(new[] { 31, 32, 33, 34, 35 }, asset.Pieces.Select(piece => piece.DescriptorId).ToArray());
        Assert.Equal("F8EF834E2B7F0865548A35C5B505C5BD22ABFEF57AB97240D694E82996A65475", LargePartDisplayTestHelpers.ComputeSignature(asset));
    }

    [Fact]
    public async Task ImageRepository_ReadLargePartDisplay_ExtractsRealDeathcrawlerLegs()
    {
        var rom = await RomFile.LoadAsync(TestRomLocator.FindWorkspaceRom());
        var repository = new ImageAssetRepository();
        var parts = new PartTableReader().ReadAll(rom);

        var asset = repository.ReadLargePartDisplay(rom, parts[131]);

        Assert.Equal(5, asset.Pieces.Count);
        Assert.Equal(new[] { 45, 46, 47, 48, 49 }, asset.Pieces.Select(piece => piece.DescriptorId).ToArray());
        Assert.Equal("2A0B4A66CF4B2FB87C8E0988950D4CDC0A97ABEC9254658EF7E90009947141FC", LargePartDisplayTestHelpers.ComputeSignature(asset));
    }

    [Fact]
    public async Task ImageRepository_ReadLargePartDisplay_RealArmVariantIdentityDistributionMatchesCurrentRom()
    {
        var rom = await RomFile.LoadAsync(TestRomLocator.FindWorkspaceRom());
        var repository = new ImageAssetRepository();
        var parts = new PartTableReader().ReadAll(rom);

        var armParts = parts.Where(part => part.Kind is PartKind.RightArm or PartKind.LeftArm).ToArray();
        var identicalCount = 0;
        var distinctCount = 0;
        foreach (var part in armParts)
        {
            var assetA = repository.ReadLargePartDisplay(rom, part, 0);
            var assetB = repository.ReadLargePartDisplay(rom, part, 1);
            if (LargePartDisplayTestHelpers.AreEquivalent(assetA, assetB))
            {
                identicalCount++;
            }
            else
            {
                distinctCount++;
            }
        }

        Assert.Equal(240, armParts.Length);
        Assert.Equal(204, identicalCount);
        Assert.Equal(36, distinctCount);
    }

    [Theory]
    [InlineData(449)]
    [InlineData(450)]
    public async Task ImageRepository_ReadLargePartDisplay_AvikingArmsHaveIdenticalLargeVariants(int partId)
    {
        var rom = await RomFile.LoadAsync(TestRomLocator.FindWorkspaceRom());
        var repository = new ImageAssetRepository();
        var parts = new PartTableReader().ReadAll(rom);

        var assetA = repository.ReadLargePartDisplay(rom, parts[partId], 0);
        var assetB = repository.ReadLargePartDisplay(rom, parts[partId], 1);

        Assert.True(LargePartDisplayTestHelpers.AreEquivalent(assetA, assetB));
    }

    [Theory]
    [InlineData(170)]
    [InlineData(171)]
    [InlineData(131)]
    [InlineData(375)]
    public async Task ImageRepository_ReadLargePartDisplay_RealPiecesExposeWritableImagePointers(int partId)
    {
        var rom = await RomFile.LoadAsync(TestRomLocator.FindWorkspaceRom());
        var repository = new ImageAssetRepository();
        var parts = new PartTableReader().ReadAll(rom);

        var asset = repository.ReadLargePartDisplay(rom, parts[partId]);

        Assert.All(asset.Pieces, piece => Assert.True(piece.ImagePointerOffset > 0));
    }

    [Fact]
    public async Task ImageRepository_ReadLargePartDisplay_PipoHammerArmVariantsShareOnePointerTarget()
    {
        var rom = await RomFile.LoadAsync(TestRomLocator.FindWorkspaceRom());
        var repository = new ImageAssetRepository();
        var parts = new PartTableReader().ReadAll(rom);

        var assetA = repository.ReadLargePartDisplay(rom, parts[170], 0);
        var assetB = repository.ReadLargePartDisplay(rom, parts[170], 1);

        Assert.Equal(2, assetA.Pieces.Count);
        Assert.Equal(2, assetB.Pieces.Count);
        Assert.Equal(2, assetA.Pieces.Select(piece => piece.ImagePointerOffset).Distinct().Count());
        Assert.Equal(2, assetB.Pieces.Select(piece => piece.ImagePointerOffset).Distinct().Count());
        Assert.Single(assetA.Pieces.Select(piece => piece.ImagePointerOffset).Intersect(assetB.Pieces.Select(piece => piece.ImagePointerOffset)));
    }

    [Fact]
    public async Task ImageRepository_ReadMedabotLargeDisplayFrame_ResolvesCombinedBattlePreviewPieces()
    {
        var rom = await RomFile.LoadAsync(TestRomLocator.FindWorkspaceRom());
        var repository = new ImageAssetRepository();
        var parts = new PartTableReader().ReadAll(rom);

        var right = repository.ReadMedabotLargeDisplayFrame(rom, parts, medabotId: 42, side: 0);
        var left = repository.ReadMedabotLargeDisplayFrame(rom, parts, medabotId: 42, side: 1);

        Assert.Equal(42, right.MedabotId);
        Assert.Equal(42, left.MedabotId);
        Assert.Equal(0, right.Side);
        Assert.Equal(1, left.Side);
        Assert.False(right.MirrorFinalImageHorizontally);
        Assert.False(left.MirrorFinalImageHorizontally);
        Assert.Equal(11, right.Pieces.Count);
        Assert.Equal(11, left.Pieces.Count);
        Assert.Contains(right.Pieces, piece => piece.DescriptorId == 31);
        Assert.Contains(left.Pieces, piece => piece.DescriptorId == 35);
        Assert.True(Math.Abs(
            right.Pieces.First(piece => piece.DescriptorId == 17).X -
            right.Pieces.First(piece => piece.DescriptorId == 14).X) >= 12);
        Assert.NotNull(right.InitialPaletteBanks);
        Assert.NotNull(left.InitialPaletteBanks);
    }

    [Theory]
    [InlineData(13)]
    [InlineData(23)]
    public async Task ImageRepository_ReadMedabotLargeDisplayFrame_MirroredSideKeepsOriginalArmTemplatePaletteBanks(int medabotId)
    {
        var rom = await RomFile.LoadAsync(TestRomLocator.FindWorkspaceRom());
        var repository = new ImageAssetRepository();
        var parts = new PartTableReader().ReadAll(rom);

        var left = repository.ReadMedabotLargeDisplayFrame(rom, parts, medabotId, side: 1);

        Assert.Equal(1, left.Pieces.Single(piece => piece.DescriptorId == 17).PaletteBank);
        Assert.Equal(1, left.Pieces.Single(piece => piece.DescriptorId == 18).PaletteBank);
        Assert.Equal(2, left.Pieces.Single(piece => piece.DescriptorId == 14).PaletteBank);
        Assert.Equal(2, left.Pieces.Single(piece => piece.DescriptorId == 15).PaletteBank);
    }

    [Theory]
    [InlineData(13, 0x5B47F0, 0x5BA0FC, 0x5A756C, 0x5AD1A4)]
    [InlineData(23, 0x5B4F64, 0x5BA7F4, 0x5A7D20, 0x5AD88C)]
    public async Task ImageRepository_ReadMedabotLargeDisplayFrame_MirroredSideUsesRomArmCopyPassSources(
        int medabotId,
        int rightTopImageOffset,
        int rightBottomImageOffset,
        int leftTopImageOffset,
        int leftBottomImageOffset)
    {
        var rom = await RomFile.LoadAsync(TestRomLocator.FindWorkspaceRom());
        var repository = new ImageAssetRepository();
        var parts = new PartTableReader().ReadAll(rom);

        var left = repository.ReadMedabotLargeDisplayFrame(rom, parts, medabotId, side: 1);

        Assert.Equal(rightTopImageOffset, left.Pieces.Single(piece => piece.DescriptorId == 17).ImageOffset);
        Assert.Equal(rightBottomImageOffset, left.Pieces.Single(piece => piece.DescriptorId == 18).ImageOffset);
        Assert.Equal(leftTopImageOffset, left.Pieces.Single(piece => piece.DescriptorId == 14).ImageOffset);
        Assert.Equal(leftBottomImageOffset, left.Pieces.Single(piece => piece.DescriptorId == 15).ImageOffset);
    }

    [Fact]
    public async Task ImageRepository_ReadMedabotLargeDisplayFrame_MirroredSideUsesOwnVariantWhenArmCopyPassSkipsNegativeEntries()
    {
        var rom = await RomFile.LoadAsync(TestRomLocator.FindWorkspaceRom());
        var repository = new ImageAssetRepository();
        var parts = new PartTableReader().ReadAll(rom);

        var left = repository.ReadMedabotLargeDisplayFrame(rom, parts, medabotId: 42, side: 1);

        Assert.Equal(0x5AEB14, left.Pieces.Single(piece => piece.DescriptorId == 18).ImageOffset);
        Assert.Equal(0x5BBB40, left.Pieces.Single(piece => piece.DescriptorId == 15).ImageOffset);
    }

    [Theory]
    [InlineData(0, new[] { 7, 8, 16, 19, 30 })]
    [InlineData(28, new[] { 13, 14, 15, 17, 18, 25, 26, 27, 28, 29 })]
    [InlineData(30, new[] { 7, 8, 14, 15, 17, 18, 41, 42, 43, 44 })]
    [InlineData(32, new[] { 7, 8, 14, 15, 17, 18, 45, 46, 47, 48, 49 })]
    [InlineData(43, new[] { 9, 10, 14, 15, 17, 18, 20, 21, 22, 23, 24 })]
    [InlineData(45, new[] { 7, 8, 14, 15, 17, 18, 36, 37, 38, 39, 40 })]
    public async Task ImageRepository_ReadMedabotLargeDisplayFrame_SupportsRepresentativeArmAndLegFamilies(
        int medabotId,
        int[] expectedDescriptorIds)
    {
        var rom = await RomFile.LoadAsync(TestRomLocator.FindWorkspaceRom());
        var repository = new ImageAssetRepository();
        var parts = new PartTableReader().ReadAll(rom);

        var right = repository.ReadMedabotLargeDisplayFrame(rom, parts, medabotId, side: 0);
        var left = repository.ReadMedabotLargeDisplayFrame(rom, parts, medabotId, side: 1);

        Assert.Equal(expectedDescriptorIds, right.Pieces.Select(piece => piece.DescriptorId).OrderBy(id => id).ToArray());
        Assert.Equal(expectedDescriptorIds, left.Pieces.Select(piece => piece.DescriptorId).OrderBy(id => id).ToArray());
    }
}
