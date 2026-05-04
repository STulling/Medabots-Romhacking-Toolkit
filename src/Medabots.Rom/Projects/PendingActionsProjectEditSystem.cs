using Medabots.Rom.Metadata;

namespace Medabots.Rom.Projects;

internal sealed class PendingActionsProjectEditSystem : IProjectEditSystem
{
    public string DisplayName => "Pending Patch Action";

    public IEnumerable<string> DescribeChanges(RomHackProject project) =>
        project.PendingActions.Select(action => $"0x{action.Offset:X6} ({action.Data.Length} bytes) {action.Description}");

    public IEnumerable<ProjectChange> BuildChanges(RomHackProject project, ProjectBuildContext context)
    {
        if (project.PendingActions.Count == 0)
        {
            return [];
        }

        return [new ProjectChange(DisplayName, "Pending patch actions", project.PendingActions)];
    }
}
