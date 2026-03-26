using Medabots.Rom.Metadata;

namespace Medabots.Rom.Projects;

public sealed class RomHackProjectApplicator
{
    private readonly Text.MedabotsTextPatcher _textPatcher;
    private readonly Events.EventInstructionPatcher _eventInstructionPatcher;

    public RomHackProjectApplicator(Text.MedabotsTextPatcher? textPatcher = null, Events.EventInstructionPatcher? eventInstructionPatcher = null)
    {
        _textPatcher = textPatcher ?? new Text.MedabotsTextPatcher();
        _eventInstructionPatcher = eventInstructionPatcher ?? new Events.EventInstructionPatcher();
    }

    public void Apply(RomHackProject project, RomHackSession session)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(session);

        session.ApplyPatches(project.PendingActions);

        if (project.MessagePatches.Count == 0)
        {
            return;
        }

        var profile = MedabotsRomTextProfiles.FindById(project.TextProfileId)
            ?? throw new InvalidOperationException("The project does not define a known text profile.");

        if (project.MessagePatches.Count > 0)
        {
            _textPatcher.Apply(session, profile.TextPointerTableOffset, profile.TextDumpOffset, project.MessagePatches);
        }

        foreach (var patch in project.EventScriptPatches)
        {
            _eventInstructionPatcher.RewriteEvent(session, profile, patch.EventId, patch.ScriptBytes, $"Apply project event patch {patch.EventId}");
        }
    }
}
