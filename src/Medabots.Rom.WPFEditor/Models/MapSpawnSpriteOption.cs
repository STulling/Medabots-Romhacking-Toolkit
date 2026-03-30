using System.Windows.Media.Imaging;

namespace Medabots.Rom.WPFEditor.Models;

public sealed record MapSpawnSpriteOption(
    int ResourceIndex,
    int SpriteId,
    string Title,
    string Subtitle,
    BitmapSource Thumbnail,
    bool IsHidden);
