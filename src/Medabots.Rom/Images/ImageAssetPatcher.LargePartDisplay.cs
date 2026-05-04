using Medabots.Rom.Compression;

namespace Medabots.Rom.Images;

public sealed partial class ImageAssetPatcher
{
    public void ApplyLargePartDisplaySmart(RomHackSession session, LargePartDisplayAsset asset, int expansionStartOffset = DefaultExpansionStartOffset)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(asset);

        var nextExpansionOffset = expansionStartOffset;
        Span<byte> pointer = stackalloc byte[4];
        foreach (var piece in asset.Pieces)
        {
            if (piece.ImagePointerOffset <= 0)
            {
                continue;
            }

            var packed = TileImageCodec.Pack4BppTiles(piece.Image.PixelIndices);
            var compressed = GbaLz77.Compress(packed);
            var imageOffset = Align(Math.Max(nextExpansionOffset, expansionStartOffset), 4);
            nextExpansionOffset = Align(imageOffset + compressed.Length, 4);

            session.ApplyPatch(RomPatchAction.Create(imageOffset, compressed, $"Write large part display {asset.PartId} variant {asset.VariantSelector} descriptor {piece.DescriptorId} image"));
            BitConverter.TryWriteBytes(pointer, GbaPointer.ToRomAddress(imageOffset));
            session.ApplyPatch(RomPatchAction.Create(piece.ImagePointerOffset, pointer, $"Repoint large part display {asset.PartId} variant {asset.VariantSelector} descriptor {piece.DescriptorId} image"));
        }

        var paletteWrites = new Dictionary<int, (int CurrentOffset, byte[] PaletteBytes, int DescriptorId)>();
        foreach (var piece in asset.Pieces)
        {
            if (piece.PalettePointerOffset <= 0 || piece.PaletteBytes.Length == 0)
            {
                continue;
            }

            paletteWrites[piece.PalettePointerOffset] = (piece.PaletteOffset, piece.PaletteBytes, piece.DescriptorId);
        }

        foreach (var paletteWrite in paletteWrites)
        {
            var paletteOffset = Align(Math.Max(nextExpansionOffset, expansionStartOffset), 4);
            nextExpansionOffset = Align(paletteOffset + paletteWrite.Value.PaletteBytes.Length, 4);

            session.ApplyPatch(RomPatchAction.Create(paletteOffset, paletteWrite.Value.PaletteBytes, $"Write large part display {asset.PartId} variant {asset.VariantSelector} descriptor {paletteWrite.Value.DescriptorId} palette"));
            BitConverter.TryWriteBytes(pointer, GbaPointer.ToRomAddress(paletteOffset));
            session.ApplyPatch(RomPatchAction.Create(paletteWrite.Key, pointer, $"Repoint large part display {asset.PartId} variant {asset.VariantSelector} descriptor {paletteWrite.Value.DescriptorId} palette"));
        }
    }
}
