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

public sealed record MedabotLargeDisplayFrame(
    int MedabotId,
    int Side,
    bool MirrorFinalImageHorizontally,
    IReadOnlyDictionary<int, byte[]> InitialPaletteBanks,
    IReadOnlyList<LargePartDisplayPieceAsset> Pieces);

public sealed record LargePartDisplayPieceAsset(
    int DescriptorId,
    int AppearanceEntryOffset,
    int RecordOffset,
    byte[] DescriptorRecordBytes,
    int ImagePointerOffset,
    int PalettePointerOffset,
    int ImageOffset,
    int PaletteOffset,
    byte[] PaletteBytes,
    int PaletteBank,
    int X,
    int Y,
    byte SiblingDescriptorId,
    byte ChildDescriptorId,
    byte RawWidth,
    byte RawHeight,
    byte SizeDivisors,
    int LoadedTileCount,
    bool MirrorDisplayHorizontally,
    bool ForceIndependentSource,
    IndexedImage Image);

public sealed record LargePartDisplayDescriptorVariantResolution(
    int VariantSelector,
    uint AppearanceEntryRaw,
    int AppearanceSelectorBase,
    int AppearanceVariantBit,
    int AppearanceSignedByte1,
    int TableIndex,
    int ImagePointerOffset,
    int PalettePointerOffset,
    int ImageOffset,
    int PaletteOffset,
    bool HasImage);

public sealed record LargePartDisplayDescriptorRecord(
    int DescriptorId,
    int AppearanceEntryOffset,
    uint AppearanceEntryRaw,
    int DescriptorPointerOffset,
    int RecordOffset,
    int BlobPointerTableOffset,
    byte[] DescriptorRecordBytes,
    int ImagePointerOffset,
    int PalettePointerOffset,
    int ImageOffset,
    int PaletteOffset,
    int RawX,
    int RawY,
    int X,
    int Y,
    byte SiblingDescriptorId,
    byte ChildDescriptorId,
    byte PaletteBank,
    byte RawWidth,
    byte RawHeight,
    byte SizeDivisors,
    byte RawByte0C,
    byte RawByte0D,
    byte RawByte0E,
    byte RawByte15,
    byte RawByte16,
    byte RawByte17,
    int WidthDivisor,
    int HeightDivisor,
    int EffectiveWidth,
    int EffectiveHeight,
    int TableIndex,
    bool HasImage,
    int SelectedVariantSelector,
    IReadOnlyList<LargePartDisplayDescriptorVariantResolution> VariantResolutions);
