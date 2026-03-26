using System.Windows.Media.Imaging;

namespace Medabots.Rom.WPFEditor.Models;

public sealed record SpritePreviewState(
    int SpriteId,
    BitmapSource Bitmap,
    string Summary,
    string PaletteSummary,
    IReadOnlyList<PaletteSwatchItem> Swatches);
