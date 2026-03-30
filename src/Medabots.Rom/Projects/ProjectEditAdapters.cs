using Medabots.Rom.Battles;
using Medabots.Rom.Images;
using Medabots.Rom.Maps;
using Medabots.Rom.Parts;

namespace Medabots.Rom.Projects;

public static class ProjectEditAdapters
{
    public static IProjectEditCollectionAdapter<SpriteAsset, int> OverworldSprite { get; } = new OverworldSpriteAdapter();

    public static IProjectEditCollectionAdapter<PortraitAsset, (int CharacterId, int PortraitIndex)> Portrait { get; } = new PortraitAdapter();

    public static IProjectEditCollectionAdapter<BattleCompositeSpriteComponentAsset, (int MedabotId, int ComponentIndex)> BattleCompositeSprite { get; } = new BattleCompositeSpriteAdapter();

    public static IProjectEditCollectionAdapter<LargePartDisplayAsset, (int PartId, int VariantSelector)> LargeDisplaySprite { get; } = new LargeDisplaySpriteAdapter();

    public static IProjectEditCollectionAdapter<BattleDefinition, int> Battle { get; } = new BattleAdapter();

    public static IProjectEditCollectionAdapter<PartDefinition, int> Part { get; } = new PartAdapter();

    public static IProjectEditCollectionAdapter<MapLayerPatch, (int MapId, int LayerIndex)> MapLayer { get; } = new MapLayerAdapter();

    private sealed class OverworldSpriteAdapter : IProjectEditCollectionAdapter<SpriteAsset, int>
    {
        public IList<SpriteAsset> GetCollection(RomHackProject project) => project.OverworldSpriteEdits;
        public int GetKey(SpriteAsset edit) => edit.SpriteId;
    }

    private sealed class PortraitAdapter : IProjectEditCollectionAdapter<PortraitAsset, (int CharacterId, int PortraitIndex)>
    {
        public IList<PortraitAsset> GetCollection(RomHackProject project) => project.PortraitEdits;
        public (int CharacterId, int PortraitIndex) GetKey(PortraitAsset edit) => (edit.CharacterId, edit.PortraitIndex);
    }

    private sealed class BattleCompositeSpriteAdapter : IProjectEditCollectionAdapter<BattleCompositeSpriteComponentAsset, (int MedabotId, int ComponentIndex)>
    {
        public IList<BattleCompositeSpriteComponentAsset> GetCollection(RomHackProject project) => project.BattleCompositeSpriteEdits;
        public (int MedabotId, int ComponentIndex) GetKey(BattleCompositeSpriteComponentAsset edit) => (edit.MedabotId, edit.ComponentIndex);
    }

    private sealed class LargeDisplaySpriteAdapter : IProjectEditCollectionAdapter<LargePartDisplayAsset, (int PartId, int VariantSelector)>
    {
        public IList<LargePartDisplayAsset> GetCollection(RomHackProject project) => project.LargePartDisplayEdits;
        public (int PartId, int VariantSelector) GetKey(LargePartDisplayAsset edit) => (edit.PartId, edit.VariantSelector);
    }

    private sealed class BattleAdapter : IProjectEditCollectionAdapter<BattleDefinition, int>
    {
        public IList<BattleDefinition> GetCollection(RomHackProject project) => project.BattleEdits;
        public int GetKey(BattleDefinition edit) => edit.Id;
    }

    private sealed class PartAdapter : IProjectEditCollectionAdapter<PartDefinition, int>
    {
        public IList<PartDefinition> GetCollection(RomHackProject project) => project.PartEdits;
        public int GetKey(PartDefinition edit) => edit.Id;
    }

    private sealed class MapLayerAdapter : IProjectEditCollectionAdapter<MapLayerPatch, (int MapId, int LayerIndex)>
    {
        public IList<MapLayerPatch> GetCollection(RomHackProject project) => project.MapLayerPatches;
        public (int MapId, int LayerIndex) GetKey(MapLayerPatch edit) => (edit.MapId, edit.LayerIndex);
    }
}
