namespace Medabots.Rom;

public sealed class RomHackSession
{
    private readonly List<RomPatchAction> _appliedActions = new();

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

        RomFile.WriteBytes(action.Offset, action.Data);
        _appliedActions.Add(action);
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
}
