namespace Medabots.Rom.Maps;

public sealed record MapEntitySpawnRecord(
    byte TileX,
    byte TileY,
    ushort RecordKindAndEventId,
    byte SpriteAndFacingPacked,
    byte SpawnGroupIndex,
    ushort ChapterVisibilityMask)
{
    public int RecordKind => (RecordKindAndEventId >> 12) & 0xF;
    public int EventOrObjectId => RecordKindAndEventId & 0x0FFF;
    public bool IsWalkOverTrigger => RecordKind == 0x8;
    public bool IsFacingTriggerOrMarker => RecordKind is 0x0 or 0x4 or 0x6;
    public bool IsVisibleInChapter(int chapterIndex)
    {
        if (chapterIndex < 0 || chapterIndex >= 16)
        {
            return true;
        }

        return ((ChapterVisibilityMask >> chapterIndex) & 1) != 0;
    }
}
