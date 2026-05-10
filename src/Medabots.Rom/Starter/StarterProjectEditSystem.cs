using Medabots.Rom.Projects;

namespace Medabots.Rom.Starter;

internal sealed class StarterProjectEditSystem : IProjectEditSystem
{
    private readonly StarterPatcher _patcher;

    public StarterProjectEditSystem(StarterPatcher patcher)
    {
        _patcher = patcher;
    }

    public string DisplayName => "Starter";

    public IEnumerable<string> DescribeChanges(RomHackProject project) =>
        project.StarterEdits.Select(_ => "Starter loadout");

    public IEnumerable<ProjectChange> BuildChanges(RomHackProject project, ProjectBuildContext context)
    {
        return project.StarterEdits
            .OrderBy(edit => edit.PartsOffset)
            .Select(starter => new ProjectChange(DisplayName, "Starter loadout patch", _patcher.BuildPatches(starter)))
            .ToArray();
    }
}
