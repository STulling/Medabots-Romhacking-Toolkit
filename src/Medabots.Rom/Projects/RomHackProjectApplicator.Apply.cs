using Medabots.Rom.Metadata;

namespace Medabots.Rom.Projects;

public sealed partial class RomHackProjectApplicator
{
    private static void ApplyPendingActions(RomHackProject project, RomHackSession session)
    {
        session.ApplyPatches(project.PendingActions);
    }

    private void ApplyMessagePatches(RomHackProject project, RomHackSession session, MedabotsRomTextProfile? profile)
    {
        if (project.MessagePatches.Count == 0)
        {
            return;
        }

        var resolvedProfile = profile ?? throw new InvalidOperationException("The project does not define a known text profile.");
        _textPatcher.Apply(session, resolvedProfile.TextPointerTableOffset, resolvedProfile.TextDumpOffset, project.MessagePatches);
    }

    private void ApplyEventScriptPatches(RomHackProject project, RomHackSession session, MedabotsRomTextProfile? profile)
    {
        if (project.EventScriptPatches.Count == 0)
        {
            return;
        }

        var resolvedProfile = profile ?? throw new InvalidOperationException("The project does not define a known text profile.");
        foreach (var patch in project.EventScriptPatches)
        {
            _eventInstructionPatcher.RewriteEvent(session, resolvedProfile, patch.EventId, patch.ScriptBytes, $"Apply project event patch {patch.EventId}");
        }
    }
}
