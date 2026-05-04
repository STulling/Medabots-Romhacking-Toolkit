using Medabots.Rom.Metadata;

namespace Medabots.Rom.Battles;

public sealed class BattlePatcher
{
    public RomPatchAction BuildPatch(BattleDefinition battle)
    {
        ArgumentNullException.ThrowIfNull(battle);
        return RomPatchAction.Create(battle.DataOffset, BattleTableReader.Serialize(battle), $"Update battle {battle.Id}");
    }

    public void Apply(RomHackSession session, BattleDefinition battle)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.ApplyPatch(BuildPatch(battle));
    }
}
