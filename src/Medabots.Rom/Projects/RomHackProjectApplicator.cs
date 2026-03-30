using Medabots.Rom.Metadata;

namespace Medabots.Rom.Projects;

public sealed partial class RomHackProjectApplicator
{
    private readonly Text.MedabotsTextPatcher _textPatcher;
    private readonly Events.EventInstructionPatcher _eventInstructionPatcher;
    private readonly Encounters.EncounterTableReader _encounterTableReader;
    private readonly Maps.MapOverlayPatcher _mapOverlayPatcher;
    private readonly Maps.MapLayerPatcher _mapLayerPatcher;
    private readonly Images.ImageAssetPatcher _imageAssetPatcher;
    private readonly Battles.BattlePatcher _battlePatcher;
    private readonly Parts.PartPatcher _partPatcher;

    public RomHackProjectApplicator(Text.MedabotsTextPatcher? textPatcher = null, Events.EventInstructionPatcher? eventInstructionPatcher = null, Maps.MapOverlayPatcher? mapOverlayPatcher = null, Maps.MapLayerPatcher? mapLayerPatcher = null, Images.ImageAssetPatcher? imageAssetPatcher = null, Battles.BattlePatcher? battlePatcher = null, Parts.PartPatcher? partPatcher = null)
    {
        _textPatcher = textPatcher ?? new Text.MedabotsTextPatcher();
        _eventInstructionPatcher = eventInstructionPatcher ?? new Events.EventInstructionPatcher();
        _encounterTableReader = new Encounters.EncounterTableReader();
        _mapOverlayPatcher = mapOverlayPatcher ?? new Maps.MapOverlayPatcher();
        _mapLayerPatcher = mapLayerPatcher ?? new Maps.MapLayerPatcher();
        _imageAssetPatcher = imageAssetPatcher ?? new Images.ImageAssetPatcher();
        _battlePatcher = battlePatcher ?? new Battles.BattlePatcher();
        _partPatcher = partPatcher ?? new Parts.PartPatcher();
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
        ApplyMapLayerPatches(project, session);
        ApplyBattleEdits(project, session);
        ApplyPartEdits(project, session);
        ApplySpriteEdits(project, session);
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
