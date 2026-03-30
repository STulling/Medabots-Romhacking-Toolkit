using System.Windows.Media.Imaging;

namespace Medabots.Rom.WPFEditor.Models;

public sealed record MapSpawnFacingOption(
    int FacingVariant,
    string Title,
    BitmapSource Thumbnail);
