namespace Medabots.Rom.Images;

public sealed record SpriteAsset(
    int SpriteId,
    int ImagePointerOffset,
    int PalettePointerOffset,
    int ImageOffset,
    int PaletteOffset,
    IndexedImage Image);
