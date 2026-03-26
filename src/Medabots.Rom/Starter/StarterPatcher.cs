namespace Medabots.Rom.Starter;

public sealed class StarterPatcher
{
    public void Apply(RomHackSession session, StarterDefinition starter)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(starter);

        var parts = new byte[17];
        for (var i = 0; i < 4; i++)
        {
            parts[i * 4] = starter.PartId;
        }

        parts[16] = starter.IsFemale ? (byte)1 : (byte)0;
        session.ApplyPatch(RomPatchAction.Create(starter.PartsOffset, parts, "Update starter loadout"));
        session.ApplyPatch(RomPatchAction.Create(starter.MedalOffset, [starter.MedalId], "Update starter medal"));
    }
}
