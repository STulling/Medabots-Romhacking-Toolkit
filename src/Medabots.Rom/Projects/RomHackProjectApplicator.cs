using Medabots.Rom.Metadata;

namespace Medabots.Rom.Projects;

public sealed partial class RomHackProjectApplicator
{
    private readonly Text.MedabotsTextPatcher _textPatcher;
    private readonly Events.EventInstructionPatcher _eventInstructionPatcher;
    private readonly Encounters.EncounterTableReader _encounterTableReader;
    private readonly Maps.MapOverlayPatcher _mapOverlayPatcher;

    public RomHackProjectApplicator(Text.MedabotsTextPatcher? textPatcher = null, Events.EventInstructionPatcher? eventInstructionPatcher = null, Maps.MapOverlayPatcher? mapOverlayPatcher = null)
    {
        _textPatcher = textPatcher ?? new Text.MedabotsTextPatcher();
        _eventInstructionPatcher = eventInstructionPatcher ?? new Events.EventInstructionPatcher();
        _encounterTableReader = new Encounters.EncounterTableReader();
        _mapOverlayPatcher = mapOverlayPatcher ?? new Maps.MapOverlayPatcher();
    }

    public void Apply(RomHackProject project, RomHackSession session)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(session);

        ApplyPendingActions(project, session);
        var profile = ResolveTextProfile(project);
        ApplyMessagePatches(project, session, profile);
        ApplyEventScriptPatches(project, session, profile);
        ApplyMapEncounterStatePatches(project, session);
        ApplyMapEncounterPatches(project, session);
        ApplyMapMusicPatches(project, session);
        ApplyMapEventObjectResourcePatches(project, session);
        ApplyMapEntitySpawnPatches(project, session);
        ApplyMapWarpPatches(project, session);
        ApplyMapCollisionPatches(project, session);
    }

    private MedabotsRomTextProfile? ResolveTextProfile(RomHackProject project)
    {
        if (string.IsNullOrWhiteSpace(project.TextProfileId))
        {
            return null;
        }

        return MedabotsRomTextProfiles.FindById(project.TextProfileId)
            ?? throw new InvalidOperationException("The project does not define a known text profile.");
    }
}
