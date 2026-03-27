namespace Medabots.Rom.WPFEditor.Models;

public sealed class SpritePaletteFamilyOption
{
    public byte Value { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public IReadOnlyList<PaletteSwatchItem> PreviewSwatches { get; init; } = [];
}
