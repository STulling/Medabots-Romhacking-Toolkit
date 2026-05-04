using Medabots.Rom.Metadata;
using Medabots.Rom.Projects;

namespace Medabots.Rom.Battles;

internal sealed class BattleProjectEditSystem : IProjectEditSystem
{
    private readonly BattlePatcher _patcher;

    public BattleProjectEditSystem(BattlePatcher patcher)
    {
        _patcher = patcher;
    }

    public string DisplayName => "Battle";

    public IEnumerable<string> DescribeChanges(RomHackProject project) =>
        project.BattleEdits.Select(edit => $"Battle {edit.Id:D3}");

    public IEnumerable<ProjectChange> BuildChanges(RomHackProject project, ProjectBuildContext context)
    {
        return project.BattleEdits
            .OrderBy(edit => edit.Id)
            .Select(battle => new ProjectChange(DisplayName, $"Battle {battle.Id} patch", [_patcher.BuildPatch(battle)]))
            .ToArray();
    }
}
