using Medabots.Rom.Metadata;
using Medabots.Rom.Projects;

namespace Medabots.Rom.Parts;

internal sealed class PartProjectEditSystem : IProjectEditSystem
{
    private readonly PartPatcher _patcher;

    public PartProjectEditSystem(PartPatcher patcher)
    {
        _patcher = patcher;
    }

    public string DisplayName => "Part";

    public IEnumerable<string> DescribeChanges(RomHackProject project) =>
        project.PartEdits.Select(edit => $"Part {edit.Id:D3}");

    public IEnumerable<ProjectChange> BuildChanges(RomHackProject project, ProjectBuildContext context)
    {
        return project.PartEdits
            .OrderBy(edit => edit.Id)
            .Select(part => new ProjectChange(DisplayName, $"Part {part.Id} patch", [_patcher.BuildPatch(part)]))
            .ToArray();
    }
}
