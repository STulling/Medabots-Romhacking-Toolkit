using Medabots.Rom.Metadata;
using Medabots.Rom.Projects;

namespace Medabots.Rom.Text;

internal sealed class MessageProjectEditSystem : IProjectEditSystem
{
    private readonly MedabotsTextPatcher _textPatcher;

    public MessageProjectEditSystem(MedabotsTextPatcher textPatcher)
    {
        _textPatcher = textPatcher;
    }

    public string DisplayName => "Message";

    public IEnumerable<string> DescribeChanges(RomHackProject project) =>
        project.MessagePatches.Select(patch => $"Bank {patch.Id.Bank:D3}, Index {patch.Id.Index:D3}");

    public IEnumerable<ProjectChange> BuildChanges(RomHackProject project, ProjectBuildContext context)
    {
        if (project.MessagePatches.Count == 0)
        {
            return [];
        }

        var resolvedProfile = context.Layout.RequireTextAndEventProfile();
        var actions = _textPatcher.BuildPatchActions(context.SourceRom, resolvedProfile.TextPointerTableOffset, resolvedProfile.TextDumpOffset, project.MessagePatches);
        return [new ProjectChange(DisplayName, "Message patches", actions)];
    }
}
