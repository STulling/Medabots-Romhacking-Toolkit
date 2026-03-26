namespace Medabots.Rom.Images;

public sealed record PortraitAsset(
    int CharacterId,
    int PortraitIndex,
    int ImagePointerOffset,
    int PalettePointerOffset,
    int ImageOffset,
    int PaletteOffset,
    IndexedImage Image);
