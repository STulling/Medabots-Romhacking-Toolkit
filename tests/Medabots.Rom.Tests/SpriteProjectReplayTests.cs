using Medabots.Rom.Images;
using Medabots.Rom.Projects;
using Medabots.Rom.Parts;
using Xunit;

namespace Medabots.Rom.Tests;

public sealed class SpriteProjectReplayTests
{
    [Fact]
    public async Task ProjectSerializerAndApplicator_ReplayEditedOverworldSpriteIntoFreshRom()
    {
        var sourceRom = await RomFile.LoadAsync(TestRomLocator.FindWorkspaceRom());
        var repository = new ImageAssetRepository();
        var sprite = repository.ReadSprite(sourceRom, 0);
        sprite.Image.PixelIndices[0] = (byte)((sprite.Image.PixelIndices[0] + 1) & 0x0F);
        sprite.Image.PaletteBytes[2] ^= 0x1F;

        var project = new RomHackProject
        {
            Name = "Sprite Project Replay"
        };
        project.OverworldSpriteEdits.Add(sprite);
        var loadedProject = await RoundTripProjectAsync(project);
        var targetSession = RomHackSession.FromRomFile(new RomFile("target-project-overworld.gba", sourceRom.Data.ToArray()));
        new RomHackProjectApplicator().Apply(loadedProject, targetSession);

        var reread = repository.ReadSprite(targetSession.RomFile, 0);
        Assert.Equal(sprite.Image.PixelIndices, reread.Image.PixelIndices);
        Assert.Equal(sprite.Image.PaletteBytes, reread.Image.PaletteBytes);
    }

    [Fact]
    public async Task ProjectSerializerAndApplicator_ReplayEditedPortraitIntoFreshRom()
    {
        var sourceRom = await RomFile.LoadAsync(TestRomLocator.FindWorkspaceRom());
        var repository = new ImageAssetRepository();
        var portrait = repository.ReadPortrait(sourceRom, 0, 0);
        portrait.Image.PixelIndices[0] = (byte)((portrait.Image.PixelIndices[0] + 3) & 0x0F);
        portrait.Image.PaletteBytes[2] ^= 0x1F;

        var project = new RomHackProject
        {
            Name = "Sprite Project Replay"
        };
        project.PortraitEdits.Add(portrait);
        var loadedProject = await RoundTripProjectAsync(project);
        var targetSession = RomHackSession.FromRomFile(new RomFile("target-project-portrait.gba", sourceRom.Data.ToArray()));
        new RomHackProjectApplicator().Apply(loadedProject, targetSession);

        var reread = repository.ReadPortrait(targetSession.RomFile, 0, 0);
        Assert.Equal(portrait.Image.PixelIndices, reread.Image.PixelIndices);
        Assert.Equal(portrait.Image.PaletteBytes, reread.Image.PaletteBytes);
    }

    [Fact]
    public async Task ProjectSerializerAndApplicator_ReplayEditedBattleDisplayIntoFreshRom()
    {
        var sourceRom = await RomFile.LoadAsync(TestRomLocator.FindWorkspaceRom());
        var repository = new ImageAssetRepository();
        var component = repository.ReadBattleCompositeSpriteComponent(sourceRom, 0, 0);
        component.Image.PixelIndices[0] = (byte)((component.Image.PixelIndices[0] + 5) & 0x0F);
        var nextFamily = (byte)((component.PaletteFamily + 1) % Medabots.Rom.Metadata.MedabotsRomSchema.CompositeBattleSpritePaletteCount);
        var nextPalette = repository.ReadBattleCompositePaletteBytesForFamily(sourceRom, nextFamily);
        component = component with
        {
            PaletteFamily = nextFamily,
            PaletteOffset = Medabots.Rom.Metadata.MedabotsRomSchema.PartSelectionComponentPaletteSetOffset + (nextFamily * ImageAssetRepository.PaletteSize),
            PaletteSelector = (byte)(nextFamily + 4),
            Image = component.Image with { PaletteBytes = nextPalette.ToArray() }
        };

        var project = new RomHackProject
        {
            Name = "Sprite Project Replay"
        };
        project.BattleCompositeSpriteEdits.Add(component);
        var loadedProject = await RoundTripProjectAsync(project);
        var targetSession = RomHackSession.FromRomFile(new RomFile("target-project-battle.gba", sourceRom.Data.ToArray()));
        new RomHackProjectApplicator().Apply(loadedProject, targetSession);

        var reread = repository.ReadBattleCompositeSpriteComponent(targetSession.RomFile, 0, 0);
        Assert.Equal(component.Image.PixelIndices, reread.Image.PixelIndices);
        Assert.Equal(component.Image.PaletteBytes, reread.Image.PaletteBytes);
        Assert.Equal(component.PaletteFamily, reread.PaletteFamily);
    }

    [Fact]
    public async Task ProjectSerializerAndApplicator_ReplayEditedLargeDisplayIntoFreshRom()
    {
        var sourceRom = await RomFile.LoadAsync(TestRomLocator.FindWorkspaceRom());
        var repository = new ImageAssetRepository();
        var parts = new PartTableReader().ReadAll(sourceRom);
        var asset = repository.ReadLargePartDisplay(sourceRom, parts[375]);
        asset.Pieces[0].Image.PixelIndices[0] = (byte)((asset.Pieces[0].Image.PixelIndices[0] + 7) & 0x0F);
        if (asset.Pieces[0].PaletteBytes.Length >= 4)
        {
            asset.Pieces[0].PaletteBytes[2] ^= 0x1F;
            asset.Pieces[0].Image.PaletteBytes[2] ^= 0x1F;
        }

        var project = new RomHackProject
        {
            Name = "Sprite Project Replay"
        };
        project.LargePartDisplayEdits.Add(asset);
        var loadedProject = await RoundTripProjectAsync(project);
        var targetSession = RomHackSession.FromRomFile(new RomFile("target-project-large.gba", sourceRom.Data.ToArray()));
        new RomHackProjectApplicator().Apply(loadedProject, targetSession);

        var reread = repository.ReadLargePartDisplay(targetSession.RomFile, parts[375]);
        Assert.Equal(asset.Pieces.Count, reread.Pieces.Count);
        for (var index = 0; index < asset.Pieces.Count; index++)
        {
            Assert.Equal(asset.Pieces[index].Image.PixelIndices, reread.Pieces[index].Image.PixelIndices);
            Assert.Equal(asset.Pieces[index].PaletteBytes, reread.Pieces[index].PaletteBytes);
        }
    }

    private static async Task<RomHackProject> RoundTripProjectAsync(RomHackProject project)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.medahack.json");
        try
        {
            await RomHackProjectSerializer.SaveAsync(project, tempFile);
            return await RomHackProjectSerializer.LoadAsync(tempFile);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
