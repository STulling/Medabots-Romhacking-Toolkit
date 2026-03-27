using Medabots.Rom.Images;
using System.Security.Cryptography;

namespace Medabots.Rom.Tests;

internal static class LargePartDisplayTestHelpers
{
    public static string ComputeSignature(LargePartDisplayAsset asset)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(asset.Pieces.Count);
        foreach (var piece in asset.Pieces)
        {
            writer.Write(piece.DescriptorId);
            writer.Write(piece.PaletteBank);
            writer.Write(piece.LoadedTileCount);
            writer.Write(piece.Image.TileWidth);
            writer.Write(piece.Image.TileHeight);
            writer.Write(piece.Image.PixelIndices.Length);
            writer.Write(piece.Image.PixelIndices);
        }

        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    public static bool AreEquivalent(LargePartDisplayAsset left, LargePartDisplayAsset right)
    {
        if (left.RootDescriptorId != right.RootDescriptorId || left.Pieces.Count != right.Pieces.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Pieces.Count; index++)
        {
            var leftPiece = left.Pieces[index];
            var rightPiece = right.Pieces[index];
            if (leftPiece.DescriptorId != rightPiece.DescriptorId ||
                leftPiece.ImageOffset != rightPiece.ImageOffset ||
                leftPiece.PaletteOffset != rightPiece.PaletteOffset ||
                leftPiece.PaletteBank != rightPiece.PaletteBank ||
                leftPiece.LoadedTileCount != rightPiece.LoadedTileCount)
            {
                return false;
            }
        }

        return true;
    }

    public static void WritePointer(byte[] romBytes, int offset, int fileOffset)
    {
        var pointer = BitConverter.GetBytes(GbaPointer.ToRomAddress(fileOffset));
        Array.Copy(pointer, 0, romBytes, offset, pointer.Length);
    }
}
