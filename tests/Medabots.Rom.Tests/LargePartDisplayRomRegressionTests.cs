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
}
