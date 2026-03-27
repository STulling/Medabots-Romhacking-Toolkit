using Medabots.Rom.Parts;

namespace Medabots.Rom.Images;

public sealed record LargePartDisplayAsset(
    int PartId,
    int PartOrdinal,
    PartKind Kind,
    int VariantSelector,
    int RootDescriptorId,
    int RootRecordOffset,
    IReadOnlyDictionary<int, byte[]> InitialPaletteBanks,
    IReadOnlyList<LargePartDisplayPieceAsset> Pieces);

public sealed record LargePartDisplayPieceAsset(
    int DescriptorId,
    int RecordOffset,
    int ImagePointerOffset,
    int PalettePointerOffset,
    int ImageOffset,
    int PaletteOffset,
    byte[] PaletteBytes,
    int PaletteBank,
    int X,
    int Y,
    int LoadedTileCount,
    IndexedImage Image);
