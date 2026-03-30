using Medabots.Rom.Metadata;

namespace Medabots.Rom.Maps;

public sealed class MapOverlayPatcher
{
    private readonly Dictionary<int, (int Offset, int Length)> _entitySpawnAllocations = [];
    private readonly Dictionary<int, (int Offset, int Length)> _warpAllocations = [];
    private readonly Dictionary<int, (int Offset, int Length)> _collisionAllocations = [];
    private readonly Dictionary<int, (int Offset, int Length)> _eventObjectResourceAllocations = [];

    public void RewriteEntitySpawns(RomHackSession session, MapEntitySpawnPatch patch, string description)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(patch);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        var payload = SerializeEntitySpawnRecords(patch.Records);
        var destination = ReserveEntitySpawnSpace(session.RomFile, patch.MapId, payload.Length);
        session.ApplyPatch(RomPatchAction.Create(destination, payload, description));

        Span<byte> pointerBytes = stackalloc byte[sizeof(uint)];
        GbaPointer.WriteFileOffset(pointerBytes, 0, destination);
        var pointerOffset = MedabotsRomSchema.MapEntitySpawnPointerTableOffset + (patch.MapId * sizeof(uint));
        session.ApplyPatch(RomPatchAction.Create(pointerOffset, pointerBytes, $"Update map {patch.MapId} entity spawn pointer"));
    }

    public void RewriteWarps(RomHackSession session, MapWarpPatch patch, string description)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(patch);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        var payload = SerializeWarpRecords(patch.Records);
        var destination = ReserveWarpSpace(session.RomFile, patch.MapId, payload.Length);
        session.ApplyPatch(RomPatchAction.Create(destination, payload, description));

        Span<byte> pointerBytes = stackalloc byte[sizeof(uint)];
        GbaPointer.WriteFileOffset(pointerBytes, 0, destination);
        var pointerOffset = MedabotsRomSchema.MapWarpPointerTableOffset + (patch.MapId * sizeof(uint));
        session.ApplyPatch(RomPatchAction.Create(pointerOffset, pointerBytes, $"Update map {patch.MapId} warp pointer"));
    }

    public void RewriteCollisionAttributes(RomHackSession session, MapCollisionPatch patch, string description)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(patch);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        var payload = patch.ColorAttributeBytes;
        var destination = ReserveCollisionSpace(session.RomFile, patch.MapId, payload.Length);
        session.ApplyPatch(RomPatchAction.Create(destination, payload, description));

        Span<byte> pointerBytes = stackalloc byte[sizeof(uint)];
        GbaPointer.WriteFileOffset(pointerBytes, 0, destination);
        var pointerOffset = MedabotsRomSchema.MapCollisionPointerTableOffset + (patch.MapId * sizeof(uint));
        session.ApplyPatch(RomPatchAction.Create(pointerOffset, pointerBytes, $"Update map {patch.MapId} collision pointer"));
    }

    public void RewriteEventObjectResources(RomHackSession session, MapEventObjectResourcePatch patch, string description)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(patch);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        var payload = SerializeEventObjectResources(patch.ResourceIds);
        var destination = ReserveEventObjectResourceSpace(session.RomFile, patch.MapId, payload.Length);
        session.ApplyPatch(RomPatchAction.Create(destination, payload, description));

        Span<byte> pointerBytes = stackalloc byte[sizeof(uint)];
        GbaPointer.WriteFileOffset(pointerBytes, 0, destination);
        var pointerOffset = MedabotsRomSchema.MapEventObjectResourcePointerTableOffset + (patch.MapId * sizeof(uint));
        session.ApplyPatch(RomPatchAction.Create(pointerOffset, pointerBytes, $"Update map {patch.MapId} sprite slot pointer"));
    }

    internal static byte[] SerializeEntitySpawnRecords(IEnumerable<MapEntitySpawnRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        var bytes = new List<byte>();
        foreach (var record in records)
        {
            bytes.Add(record.TileX);
            bytes.Add(record.TileY);
            bytes.Add((byte)(record.RecordKindAndEventId & 0xFF));
            bytes.Add((byte)((record.RecordKindAndEventId >> 8) & 0xFF));
            bytes.Add(record.SpriteAndFacingPacked);
            bytes.Add(record.SpawnGroupIndex);
            bytes.Add((byte)(record.ChapterVisibilityMask & 0xFF));
            bytes.Add((byte)((record.ChapterVisibilityMask >> 8) & 0xFF));
        }

        bytes.Add(0xFF);
        bytes.AddRange([0, 0, 0, 0, 0, 0, 0]);
        return bytes.ToArray();
    }

    internal static byte[] SerializeWarpRecords(IEnumerable<MapWarpRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        var bytes = new List<byte>();
        foreach (var record in records)
        {
            bytes.Add(record.TileX);
            bytes.Add(record.TileY);
            bytes.Add(record.DestinationMapId);
            bytes.Add(record.ArrivalFacingAndTransitionKind);
            bytes.Add(record.Unknown4);
            bytes.Add(record.Unknown5);
            bytes.Add(record.DestinationTileX);
            bytes.Add(record.DestinationTileY);
        }

        bytes.Add(0xFF);
        bytes.AddRange([0, 0, 0, 0, 0, 0, 0]);
        return bytes.ToArray();
    }

    internal static byte[] SerializeEventObjectResources(IEnumerable<byte> resourceIds)
    {
        ArgumentNullException.ThrowIfNull(resourceIds);

        var bytes = resourceIds.Take(16).ToList();
        bytes.Add(0xFF);
        return bytes.ToArray();
    }

    private int ReserveEntitySpawnSpace(RomFile romFile, int mapId, int requiredLength)
    {
        if (_entitySpawnAllocations.TryGetValue(mapId, out var allocation) && requiredLength <= allocation.Length)
        {
            return allocation.Offset;
        }

        var nextOffset = AlignUp(Math.Max(romFile.Length, 0x800000), 4);
        _entitySpawnAllocations[mapId] = (nextOffset, requiredLength);
        return nextOffset;
    }

    private int ReserveWarpSpace(RomFile romFile, int mapId, int requiredLength)
    {
        if (_warpAllocations.TryGetValue(mapId, out var allocation) && requiredLength <= allocation.Length)
        {
            return allocation.Offset;
        }

        var nextOffset = AlignUp(Math.Max(romFile.Length, 0x800000), 4);
        _warpAllocations[mapId] = (nextOffset, requiredLength);
        return nextOffset;
    }

    private int ReserveCollisionSpace(RomFile romFile, int mapId, int requiredLength)
    {
        if (_collisionAllocations.TryGetValue(mapId, out var allocation) && requiredLength <= allocation.Length)
        {
            return allocation.Offset;
        }

        var nextOffset = AlignUp(Math.Max(romFile.Length, 0x800000), 4);
        _collisionAllocations[mapId] = (nextOffset, requiredLength);
        return nextOffset;
    }

    private int ReserveEventObjectResourceSpace(RomFile romFile, int mapId, int requiredLength)
    {
        if (_eventObjectResourceAllocations.TryGetValue(mapId, out var allocation) && requiredLength <= allocation.Length)
        {
            return allocation.Offset;
        }

        var nextOffset = AlignUp(Math.Max(romFile.Length, 0x800000), 4);
        _eventObjectResourceAllocations[mapId] = (nextOffset, requiredLength);
        return nextOffset;
    }

    private static int AlignUp(int value, int alignment)
    {
        var remainder = value % alignment;
        return remainder == 0 ? value : value + (alignment - remainder);
    }
}
