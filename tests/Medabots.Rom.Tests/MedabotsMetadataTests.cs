using Medabots.Rom.Metadata;
using Xunit;

namespace Medabots.Rom.Tests;

public sealed class MedabotsMetadataTests
{
    [Fact]
    public void DefaultCatalog_LoadsExpectedNames()
    {
        var metadata = MedabotsMetadata.Default;

        Assert.Equal("Ikki", metadata.GetCharacterName(0));
        Assert.Equal("Riverview City", metadata.GetMapName(0));
        Assert.Equal("Metabee", metadata.GetBotName(43));
        Assert.Equal("Psycho Missile", metadata.GetPartName(0));
        Assert.Equal("Strike", metadata.GetSpecialityName(0));
        Assert.Equal("Sword", metadata.GetTechniqueName(0));
        Assert.Equal("Kuwagata", metadata.GetMedalName(0));
        Assert.Equal("Grapple", metadata.GetPartAttributeName(0));
        Assert.Equal("Nothing", metadata.GetPartAttributeName(26));
        Assert.Equal("Flying", metadata.GetLegTypeName(100));
        Assert.Equal("Aquatic", metadata.GetLegTypeName(106));
    }

    [Fact]
    public void DefaultCatalog_ExposesBotMetadata()
    {
        var metadata = MedabotsMetadata.Default;

        Assert.True(metadata.TryGetBestMedalId(43, out var medalId));
        Assert.Equal(1, medalId);
        Assert.True(metadata.IsFemaleBot(1));
        Assert.False(metadata.IsFemaleBot(0));
    }

    [Fact]
    public void DefaultCatalog_UsesSafeFallbackForUnknownIds()
    {
        var metadata = MedabotsMetadata.Default;

        Assert.Equal("Unknown part #999", metadata.GetPartName(999));
        Assert.Equal("Unknown character #-1", metadata.GetCharacterName(-1));
    }
}
