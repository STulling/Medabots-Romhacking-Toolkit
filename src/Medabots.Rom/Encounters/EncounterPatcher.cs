namespace Medabots.Rom.Encounters;

public sealed class EncounterPatcher
{
    public void Apply(RomHackSession session, EncounterDefinition encounter)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.ApplyPatch(RomPatchAction.Create(encounter.DataOffset, EncounterTableReader.Serialize(encounter), $"Update encounter {encounter.Id}"));
    }
}
