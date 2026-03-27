namespace Medabots.Rom.WPFEditor.Models;

public sealed class MapLayerOption
{
    public required string Key { get; init; }
    public required string DisplayName { get; init; }
    public bool IsEditable { get; init; }
    public bool HasRuntimeData { get; init; }
}
