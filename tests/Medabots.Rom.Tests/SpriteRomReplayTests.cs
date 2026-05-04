using Medabots.Rom.Images;
using Medabots.Rom.Parts;
using Xunit;

namespace Medabots.Rom.Tests;

public sealed class SpriteRomReplayTests
{
    [Fact]
    public async Task SpritePatcher_ReplaysEditedRealOverworldSpriteIntoFreshRom()
    {
        var sourceRom = await RomFile.LoadAsync(TestRomLocator.FindWorkspaceRom());
        var repository = new ImageAssetRepository();
        var patcher = new ImageAssetPatcher();
        var workingSession = RomHackSession.FromRomFile(new RomFile("working-overworld.gba", sourceRom.Data.ToArray()));
        var exportedSession = RomHackSession.FromRomFile(new RomFile("exported-overworld.gba", sourceRom.Data.ToArray()));

        var sprite = repository.ReadSprite(workingSession.RomFile, 0);
        sprite.Image.PixelIndices[0] = (byte)((sprite.Image.PixelIndices[0] + 1) & 0x0F);
        sprite.Image.PaletteBytes[2] ^= 0x1F;

        patcher.ApplySpriteSmart(workingSession, sprite, 0x800000);

        exportedSession.ApplyPatches(workingSession.AppliedActions);
        var reread = repository.ReadSprite(exportedSession.RomFile, 0);

        Assert.Equal(sprite.Image.PixelIndices, reread.Image.PixelIndices);
        Assert.Equal(sprite.Image.PaletteBytes, reread.Image.PaletteBytes);
    }

    [Fact]
    public async Task PortraitPatcher_ReplaysEditedRealPortraitIntoFreshRom()
    {
        var sourceRom = await RomFile.LoadAsync(TestRomLocator.FindWorkspaceRom());
        var repository = new ImageAssetRepository();
        var patcher = new ImageAssetPatcher();
        var workingSession = RomHackSession.FromRomFile(new RomFile("working-portrait.gba", sourceRom.Data.ToArray()));
        var exportedSession = RomHackSession.FromRomFile(new RomFile("exported-portrait.gba", sourceRom.Data.ToArray()));

        var portrait = repository.ReadPortrait(workingSession.RomFile, 0, 0);
        portrait.Image.PixelIndices[0] = (byte)((portrait.Image.PixelIndices[0] + 3) & 0x0F);
        portrait.Image.PaletteBytes[2] ^= 0x1F;

        patcher.ApplyPortraitSmart(workingSession, portrait, 0x800000);

        exportedSession.ApplyPatches(workingSession.AppliedActions);
        var reread = repository.ReadPortrait(exportedSession.RomFile, 0, 0);

        Assert.Equal(portrait.Image.PixelIndices, reread.Image.PixelIndices);
        Assert.Equal(portrait.Image.PaletteBytes, reread.Image.PaletteBytes);
    }

    [Fact]
    public async Task BattleCompositePatcher_ReplaysEditedRealBattleDisplayIntoFreshRom()
    {
        var sourceRom = await RomFile.LoadAsync(TestRomLocator.FindWorkspaceRom());
        var repository = new ImageAssetRepository();
        var patcher = new ImageAssetPatcher();
        var workingSession = RomHackSession.FromRomFile(new RomFile("working-battle-display.gba", sourceRom.Data.ToArray()));
        var exportedSession = RomHackSession.FromRomFile(new RomFile("exported-battle-display.gba", sourceRom.Data.ToArray()));

        var component = repository.ReadBattleCompositeSpriteComponent(workingSession.RomFile, 0, 0);
        component.Image.PixelIndices[0] = (byte)((component.Image.PixelIndices[0] + 5) & 0x0F);

        var nextFamily = (byte)((component.PaletteFamily + 1) % Medabots.Rom.Metadata.MedabotsRomSchema.CompositeBattleSpritePaletteCount);
        var nextPalette = repository.ReadBattleCompositePaletteBytesForFamily(workingSession.RomFile, nextFamily);
        component = component with
        {
            PaletteFamily = nextFamily,
            PaletteOffset = Medabots.Rom.Metadata.MedabotsRomSchema.PartSelectionComponentPaletteSetOffset + (nextFamily * ImageAssetRepository.PaletteSize),
            PaletteSelector = (byte)(nextFamily + 4),
            Image = component.Image with { PaletteBytes = nextPalette.ToArray() }
        };

        patcher.ApplyBattleCompositeSpriteComponentSmart(workingSession, component, 0x800000);

        exportedSession.ApplyPatches(workingSession.AppliedActions);
        var reread = repository.ReadBattleCompositeSpriteComponent(exportedSession.RomFile, 0, 0);

        Assert.Equal(component.Image.PixelIndices, reread.Image.PixelIndices);
        Assert.Equal(component.Image.PaletteBytes, reread.Image.PaletteBytes);
        Assert.Equal(component.PaletteFamily, reread.PaletteFamily);
    }

    [Fact]
    public async Task LargePartDisplayPatcher_ReplaysEditedRealLargeDisplayIntoFreshRom()
    {
        var sourceRom = await RomFile.LoadAsync(TestRomLocator.FindWorkspaceRom());
        var repository = new ImageAssetRepository();
        var patcher = new ImageAssetPatcher();
        var parts = new PartTableReader().ReadAll(sourceRom);
        var workingSession = RomHackSession.FromRomFile(new RomFile("working-large-display-real.gba", sourceRom.Data.ToArray()));
        var exportedSession = RomHackSession.FromRomFile(new RomFile("exported-large-display-real.gba", sourceRom.Data.ToArray()));

        var asset = repository.ReadLargePartDisplay(workingSession.RomFile, parts[375]);
        asset.Pieces[0].Image.PixelIndices[0] = (byte)((asset.Pieces[0].Image.PixelIndices[0] + 7) & 0x0F);
        if (asset.Pieces[0].PaletteBytes.Length >= 4)
        {
            asset.Pieces[0].PaletteBytes[2] ^= 0x1F;
            asset.Pieces[0].Image.PaletteBytes[2] ^= 0x1F;
        }

        patcher.ApplyLargePartDisplaySmart(workingSession, asset, 0x800000);

        exportedSession.ApplyPatches(workingSession.AppliedActions);
        var reread = repository.ReadLargePartDisplay(exportedSession.RomFile, parts[375]);

        Assert.Equal(asset.Pieces.Count, reread.Pieces.Count);
        Assert.Equal(asset.Pieces[0].Image.PixelIndices, reread.Pieces[0].Image.PixelIndices);
        Assert.Equal(asset.Pieces[0].PaletteBytes, reread.Pieces[0].PaletteBytes);
    }
}
