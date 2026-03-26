namespace Medabots.Rom.Parts;

public sealed class PartPatcher
{
    public RomPatchAction BuildPatch(PartDefinition part)
    {
        ArgumentNullException.ThrowIfNull(part);
        return RomPatchAction.Create(part.DataOffset, PartTableReader.Serialize(part), $"Update part {part.Id}");
    }

    public void Apply(RomHackSession session, PartDefinition part)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.ApplyPatch(BuildPatch(part));
    }
}
