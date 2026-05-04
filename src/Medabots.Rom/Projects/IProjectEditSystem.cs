using Medabots.Rom.Metadata;

namespace Medabots.Rom.Projects;

public interface IProjectEditSystem
{
    string DisplayName { get; }

    IEnumerable<string> DescribeChanges(RomHackProject project);

    IEnumerable<ProjectChange> BuildChanges(RomHackProject project, ProjectBuildContext context);
}
