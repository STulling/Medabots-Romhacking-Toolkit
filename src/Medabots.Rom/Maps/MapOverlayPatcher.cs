using Medabots.Rom.Metadata;
using Medabots.Rom.Projects;

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

        session.ApplyPatches(BuildEntitySpawnActions(session.RomFile, patch, description, new FreeSpaceAllocator(FreeSpaceAllocator.AlignUp(Math.Max(session.RomFile.Length, 0x800000), 4))));
    }

    public void RewriteWarps(RomHackSession session, MapWarpPatch patch, string description)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(patch);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        session.ApplyPatches(BuildWarpActions(session.RomFile, patch, description, new FreeSpaceAllocator(FreeSpaceAllocator.AlignUp(Math.Max(session.RomFile.Length, 0x800000), 4))));
    }

    public void RewriteCollisionAttributes(RomHackSession session, MapCollisionPatch patch, string description)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(patch);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        session.ApplyPatches(BuildCollisionActions(session.RomFile, patch, description, new FreeSpaceAllocator(FreeSpaceAllocator.AlignUp(Math.Max(session.RomFile.Length, 0x800000), 4))));
    }

    public void RewriteEventObjectResources(RomHackSession session, MapEventObjectResourcePatch patch, string description)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(patch);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        session.ApplyPatches(BuildEventObjectResourceActions(session.RomFile, patch, description, new FreeSpaceAllocator(FreeSpaceAllocator.AlignUp(Math.Max(session.RomFile.Length, 0x800000), 4))));
    }

    public IReadOnlyList<RomPatchAction> BuildEntitySpawnActions(RomFile romFile, MapEntitySpawnPatch patch, string description, FreeSpaceAllocator allocator)
    {
        var payload = SerializeEntitySpawnRecords(patch.Records);
        var destination = ReserveEntitySpawnSpace(romFile, patch.MapId, payload.Length, allocator);
        Span<byte> pointerBytes = stackalloc byte[sizeof(uint)];
        GbaPointer.WriteFileOffset(pointerBytes, 0, destination);
        var pointerOffset = MedabotsRomSchema.MapEntitySpawnPointerTableOffset + (patch.MapId * sizeof(uint));
        return
        [
            RomPatchAction.Create(destination, payload, description),
            RomPatchAction.Create(pointerOffset, pointerBytes, $"Update map {patch.MapId} entity spawn pointer")
        ];
    }

    public IReadOnlyList<RomPatchAction> BuildWarpActions(RomFile romFile, MapWarpPatch patch, string description, FreeSpaceAllocator allocator)
    {
        var payload = SerializeWarpRecords(patch.Records);
        var destination = ReserveWarpSpace(romFile, patch.MapId, payload.Length, allocator);
        Span<byte> pointerBytes = stackalloc byte[sizeof(uint)];
        GbaPointer.WriteFileOffset(pointerBytes, 0, destination);
        var pointerOffset = MedabotsRomSchema.MapWarpPointerTableOffset + (patch.MapId * sizeof(uint));
        return
        [
            RomPatchAction.Create(destination, payload, description),
            RomPatchAction.Create(pointerOffset, pointerBytes, $"Update map {patch.MapId} warp pointer")
        ];
    }

    public IReadOnlyList<RomPatchAction> BuildCollisionActions(RomFile romFile, MapCollisionPatch patch, string description, FreeSpaceAllocator allocator)
    {
        var payload = patch.ColorAttributeBytes;
        var destination = ReserveCollisionSpace(romFile, patch.MapId, payload.Length, allocator);
        Span<byte> pointerBytes = stackalloc byte[sizeof(uint)];
        GbaPointer.WriteFileOffset(pointerBytes, 0, destination);
        var pointerOffset = MedabotsRomSchema.MapCollisionPointerTableOffset + (patch.MapId * sizeof(uint));
        return
        [
            RomPatchAction.Create(destination, payload, description),
            RomPatchAction.Create(pointerOffset, pointerBytes, $"Update map {patch.MapId} collision pointer")
        ];
    }

    public IReadOnlyList<RomPatchAction> BuildEventObjectResourceActions(RomFile romFile, MapEventObjectResourcePatch patch, string description, FreeSpaceAllocator allocator)
    {
        var payload = SerializeEventObjectResources(patch.ResourceIds);
        var destination = ReserveEventObjectResourceSpace(romFile, patch.MapId, payload.Length, allocator);
        Span<byte> pointerBytes = stackalloc byte[sizeof(uint)];
        GbaPointer.WriteFileOffset(pointerBytes, 0, destination);
        var pointerOffset = MedabotsRomSchema.MapEventObjectResourcePointerTableOffset + (patch.MapId * sizeof(uint));
        return
        [
            RomPatchAction.Create(destination, payload, description),
            RomPatchAction.Create(pointerOffset, pointerBytes, $"Update map {patch.MapId} sprite slot pointer")
        ];
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

    private int ReserveEntitySpawnSpace(RomFile romFile, int mapId, int requiredLength, FreeSpaceAllocator allocator)
    {
        if (_entitySpawnAllocations.TryGetValue(mapId, out var allocation) && requiredLength <= allocation.Length)
        {
            return allocation.Offset;
        }

        var nextOffset = allocator.Reserve(requiredLength, 4);
        _entitySpawnAllocations[mapId] = (nextOffset, requiredLength);
        return nextOffset;
    }

    private int ReserveWarpSpace(RomFile romFile, int mapId, int requiredLength, FreeSpaceAllocator allocator)
    {
        if (_warpAllocations.TryGetValue(mapId, out var allocation) && requiredLength <= allocation.Length)
        {
            return allocation.Offset;
        }

        var nextOffset = allocator.Reserve(requiredLength, 4);
        _warpAllocations[mapId] = (nextOffset, requiredLength);
        return nextOffset;
    }

    private int ReserveCollisionSpace(RomFile romFile, int mapId, int requiredLength, FreeSpaceAllocator allocator)
    {
        if (_collisionAllocations.TryGetValue(mapId, out var allocation) && requiredLength <= allocation.Length)
        {
            return allocation.Offset;
        }

        var nextOffset = allocator.Reserve(requiredLength, 4);
        _collisionAllocations[mapId] = (nextOffset, requiredLength);
        return nextOffset;
    }

    private int ReserveEventObjectResourceSpace(RomFile romFile, int mapId, int requiredLength, FreeSpaceAllocator allocator)
    {
        if (_eventObjectResourceAllocations.TryGetValue(mapId, out var allocation) && requiredLength <= allocation.Length)
        {
            return allocation.Offset;
        }

        var nextOffset = allocator.Reserve(requiredLength, 4);
        _eventObjectResourceAllocations[mapId] = (nextOffset, requiredLength);
        return nextOffset;
    }
}
