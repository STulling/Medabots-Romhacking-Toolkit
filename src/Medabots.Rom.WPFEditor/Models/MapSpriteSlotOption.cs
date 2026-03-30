using System.Windows.Media.Imaging;

namespace Medabots.Rom.WPFEditor.Models;

public sealed class MapSpriteSlotOption
{
    public int RawValue { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Subtitle { get; init; } = string.Empty;

    public BitmapSource Thumbnail { get; init; } = null!;
}
