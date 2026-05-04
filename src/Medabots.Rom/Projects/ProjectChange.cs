namespace Medabots.Rom.Projects;

public sealed class ProjectChange
{
    public ProjectChange(string owner, string description, IEnumerable<RomPatchAction> actions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(actions);

        Owner = owner;
        Description = description;
        Actions = actions.ToArray();
    }

    public string Owner { get; }

    public string Description { get; }

    public IReadOnlyList<RomPatchAction> Actions { get; }

    public void Apply(RomHackSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.ApplyPatches(Actions);
    }
}
