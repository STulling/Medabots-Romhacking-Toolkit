namespace Medabots.Rom.Images;

public sealed record BattleCompositeSpriteComponentAsset(
    int MedabotId,
    int ComponentIndex,
    int ImagePointerOffset,
    int ImageOffset,
    int PalettePointerOffset,
    int PaletteOffset,
    byte PaletteFamily,
    byte AppearanceId,
    byte PaletteSelector,
    IndexedImage Image);
