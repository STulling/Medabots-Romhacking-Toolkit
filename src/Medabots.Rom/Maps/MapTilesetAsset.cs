using Medabots.Rom.Images;

namespace Medabots.Rom.Maps;

public sealed record MapTilesetAsset(
    int MapId,
    string Name,
    int WidthInTiles,
    int HeightInTiles,
    int EncounterSettingsDataOffset,
    byte EncounterEnabledByte,
    int MusicDataOffset,
    byte MusicId,
    int GraphicsPointerOffset,
    int GraphicsDataOffset,
    int PalettePointerOffset,
    int PaletteDataOffset,
    int CollisionPointerOffset,
    int CollisionDataOffset,
    int ColorAttributePointerOffset,
    int ColorAttributeDataOffset,
    int EventObjectResourcePointerOffset,
    int EventObjectResourceDataOffset,
    byte[] PaletteBytes,
    byte[] CollisionBytes,
    byte[]? ColorAttributeBytes,
    IReadOnlyList<byte> EventObjectResourceIds,
    byte[] RawTilesetPixelIndices,
    IndexedImage TilesetSheet,
    IReadOnlyList<int> TilePaletteBanks,
    IReadOnlyList<MapLayerAsset> Layers)
{
    public int WidthInMetaTiles => WidthInTiles / 2;
    public int HeightInMetaTiles => HeightInTiles / 2;
    public bool HasEncounters => EncounterEnabledByte != 0;
}
