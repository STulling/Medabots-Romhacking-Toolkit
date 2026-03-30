using Medabots.Rom.Text;

namespace Medabots.Rom;

public sealed class RomHackProject
{
    public string Name { get; set; } = "New Medabots Hack";

    public string? ProjectFilePath { get; set; }

    public string? SourceRomPath { get; set; }

    public string? TextProfileId { get; set; }

    public IList<RomPatchAction> PendingActions { get; } = new List<RomPatchAction>();

    public IList<MessagePatch> MessagePatches { get; } = new List<MessagePatch>();

    public IList<EventLabelPatch> EventLabels { get; } = new List<EventLabelPatch>();

    public IList<EventScriptPatch> EventScriptPatches { get; } = new List<EventScriptPatch>();

    public IList<short> DeletedEventScriptIds { get; } = new List<short>();

    public IList<Maps.MapEntitySpawnPatch> MapEntitySpawnPatches { get; } = new List<Maps.MapEntitySpawnPatch>();

    public IList<Maps.MapWarpPatch> MapWarpPatches { get; } = new List<Maps.MapWarpPatch>();

    public IList<Maps.MapCollisionPatch> MapCollisionPatches { get; } = new List<Maps.MapCollisionPatch>();

    public IList<Maps.MapEncounterPatch> MapEncounterPatches { get; } = new List<Maps.MapEncounterPatch>();

    public IList<Maps.MapEncounterStatePatch> MapEncounterStatePatches { get; } = new List<Maps.MapEncounterStatePatch>();

    public IList<Maps.MapMusicPatch> MapMusicPatches { get; } = new List<Maps.MapMusicPatch>();

    public IList<Maps.MapEventObjectResourcePatch> MapEventObjectResourcePatches { get; } = new List<Maps.MapEventObjectResourcePatch>();

    public IList<int> SplitLargeDisplayPartIds { get; } = new List<int>();
}
