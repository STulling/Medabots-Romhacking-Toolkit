using System.Windows.Media.Imaging;
using Medabots.Rom.Images;

namespace Medabots.Rom.WPFEditor.Models;

public sealed record SpritePreviewState(
    int SpriteId,
    BitmapSource Bitmap,
    string Summary,
    string PaletteSummary,
    IReadOnlyList<PaletteSwatchItem> Swatches,
    IReadOnlyList<SpritePreviewPiece>? Pieces = null);

public sealed record SpritePreviewPiece(
    int PieceIndex,
    int X,
    int Y,
    IndexedImage Image);
