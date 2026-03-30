namespace Medabots.Rom.Maps;

public sealed record MapEventObjectResourcePatch(
    int MapId,
    IReadOnlyList<byte> ResourceIds);
