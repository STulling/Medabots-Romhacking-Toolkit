namespace Medabots.Rom.WPFEditor.Models;

public sealed record MapTilesetOption(
    int RepresentativeMapId,
    int GraphicsDataOffset,
    int PaletteDataOffset,
    int ColorAttributeDataOffset,
    string DisplayName);
