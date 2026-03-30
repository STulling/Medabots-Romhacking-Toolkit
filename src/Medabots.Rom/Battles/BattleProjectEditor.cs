using Medabots.Rom.Projects;

namespace Medabots.Rom.Battles;

public sealed class BattleProjectEditor
{
    public BattleDefinition? StageBattle(RomHackProject project, BattleDefinition sourceBattle, BattleDefinition editedBattle)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(sourceBattle);
        ArgumentNullException.ThrowIfNull(editedBattle);

        if (BattleTableReader.Serialize(sourceBattle).SequenceEqual(BattleTableReader.Serialize(editedBattle)))
        {
            ProjectEditCollection.Remove(project, ProjectEditAdapters.Battle, sourceBattle.Id);
            return null;
        }

        ProjectEditCollection.Upsert(project, ProjectEditAdapters.Battle, editedBattle);
        return editedBattle;
    }
}
