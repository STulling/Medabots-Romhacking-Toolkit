namespace Medabots.Rom;

public sealed class RomHackSession
{
    private readonly List<RomPatchAction> _appliedActions = new();
    private readonly List<AppliedPatchRecord> _appliedPatchRecords = new();
    private string? _currentPatchOwner;

    private RomHackSession(RomFile romFile)
    {
        RomFile = romFile;
    }

    public RomFile RomFile { get; }

    public IReadOnlyList<RomPatchAction> AppliedActions => _appliedActions;

    public static async Task<RomHackSession> OpenAsync(string romPath, CancellationToken cancellationToken = default)
    {
        var romFile = await RomFile.LoadAsync(romPath, cancellationToken).ConfigureAwait(false);
        return new RomHackSession(romFile);
    }

    public static RomHackSession FromRomFile(RomFile romFile)
    {
        ArgumentNullException.ThrowIfNull(romFile);
        return new RomHackSession(romFile);
    }

    public void ApplyPatch(RomPatchAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        EnsureNoConflictingOverwrite(action);
        RomFile.WriteBytes(action.Offset, action.Data);
        _appliedActions.Add(action);
        _appliedPatchRecords.Add(new AppliedPatchRecord(action, _currentPatchOwner));
    }

    public void ApplyPatches(IEnumerable<RomPatchAction> actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        foreach (var action in actions)
        {
            ApplyPatch(action);
        }
    }

    public async Task SaveAsAsync(string destinationPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        await RomFile.SaveAsync(destinationPath, cancellationToken).ConfigureAwait(false);
    }

    public IDisposable BeginPatchScope(string owner)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        return new PatchScope(this, owner);
    }

    private void EnsureNoConflictingOverwrite(RomPatchAction action)
    {
        var actionEnd = action.Offset + action.Data.Length;
        foreach (var existing in _appliedPatchRecords)
        {
            if (!string.IsNullOrEmpty(_currentPatchOwner) &&
                string.Equals(existing.Owner, _currentPatchOwner, StringComparison.Ordinal))
            {
                continue;
            }

            var existingEnd = existing.Action.Offset + existing.Action.Data.Length;
            var overlapStart = Math.Max(action.Offset, existing.Action.Offset);
            var overlapEnd = Math.Min(actionEnd, existingEnd);
            if (overlapStart >= overlapEnd)
            {
                continue;
            }

            for (var offset = overlapStart; offset < overlapEnd; offset++)
            {
                var newByte = action.Data[offset - action.Offset];
                var oldByte = existing.Action.Data[offset - existing.Action.Offset];
                if (newByte == oldByte)
                {
                    continue;
                }

                throw new InvalidOperationException(
                    $"Conflicting ROM patch overwrite at 0x{offset:X6} between '{existing.Action.Description}' and '{action.Description}'.");
            }
        }
    }

    private sealed record AppliedPatchRecord(RomPatchAction Action, string? Owner);

    private sealed class PatchScope : IDisposable
    {
        private readonly RomHackSession _session;
        private readonly string? _previousOwner;
        private bool _disposed;

        public PatchScope(RomHackSession session, string owner)
        {
            _session = session;
            _previousOwner = session._currentPatchOwner;
            session._currentPatchOwner = owner;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _session._currentPatchOwner = _previousOwner;
            _disposed = true;
        }
    }
}
