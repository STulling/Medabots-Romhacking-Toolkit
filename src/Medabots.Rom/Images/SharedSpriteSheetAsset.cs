namespace Medabots.Rom.Images;

public sealed record SharedSpriteSheetAsset(
    string Key,
    string Name,
    int ImageOffset,
    IndexedImage Image);
