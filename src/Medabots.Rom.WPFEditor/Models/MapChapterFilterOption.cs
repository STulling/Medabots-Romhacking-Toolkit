namespace Medabots.Rom.WPFEditor.Models;

public sealed class MapChapterFilterOption
{
    public required string Key { get; init; }
    public required string DisplayName { get; init; }
    public int? ChapterIndex { get; init; }
}
