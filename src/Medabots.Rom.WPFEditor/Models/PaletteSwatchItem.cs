using System.Windows.Media;

namespace Medabots.Rom.WPFEditor.Models;

public sealed class PaletteSwatchItem
{
    public int Index { get; init; }
    public Color Color { get; init; }
    public string Hex { get; init; } = string.Empty;
    public bool IsSelected { get; set; }
}
