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

    public IList<Maps.MapDimensionPatch> MapDimensionPatches { get; } = new List<Maps.MapDimensionPatch>();

    public IList<Images.SpriteAsset> OverworldSpriteEdits { get; } = new List<Images.SpriteAsset>();

    public IList<Images.PortraitAsset> PortraitEdits { get; } = new List<Images.PortraitAsset>();

    public IList<Images.BattleCompositeSpriteComponentAsset> BattleCompositeSpriteEdits { get; } = new List<Images.BattleCompositeSpriteComponentAsset>();

    public IList<Images.LargePartDisplayAsset> LargePartDisplayEdits { get; } = new List<Images.LargePartDisplayAsset>();

    public IList<Battles.BattleDefinition> BattleEdits { get; } = new List<Battles.BattleDefinition>();

    public IList<Parts.PartDefinition> PartEdits { get; } = new List<Parts.PartDefinition>();

    public IList<Shops.ShopDefinition> ShopEdits { get; } = new List<Shops.ShopDefinition>();

    public IList<Starter.StarterDefinition> StarterEdits { get; } = new List<Starter.StarterDefinition>();

    public IList<Maps.MapLayerPatch> MapLayerPatches { get; } = new List<Maps.MapLayerPatch>();

    public IList<int> SplitLargeDisplayPartIds { get; } = new List<int>();
}
