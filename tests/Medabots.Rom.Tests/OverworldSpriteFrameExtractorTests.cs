using Medabots.Rom.Images;
using Xunit;

namespace Medabots.Rom.Tests;

public sealed class OverworldSpriteFrameExtractorTests
{
    [Fact]
    public async Task ExtractFacingFrame_Returns16x24FromRealOverworldSheet()
    {
        var rom = await RomFile.LoadAsync(TestRomLocator.FindWorkspaceRom());
        var repository = new ImageAssetRepository();

        var asset = repository.ReadSprite(rom, 0);
        var frame = OverworldSpriteFrameExtractor.ExtractFacingFrame(asset.Image, 0);

        Assert.Equal(16, frame.Width);
        Assert.Equal(24, frame.Height);
    }
}
