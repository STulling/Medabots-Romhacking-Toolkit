namespace Medabots.Rom.Metadata;

public static class MedabotsRomSchema
{
    public const int HeaderOffset = 0xA0;
    public const int HeaderLength = 18;
    public const int MessageBankCount = 16;

    public const int PortraitsPerCharacter = 9;
    public const int PaletteSize = 0x20;
    public const int BattleSize = 0x28;
    public const int BattleBotCount = 3;
    public const int BattleBotOffset = 3;
    public const int BattleBotSize = 12;
    public const int BattleFooterOffset = 0x27;
    public const int BattleActionScriptTableOffset = 0x3C6CC8;
    public const int BattleActionScriptCount = 0x78;
    public const int BattleActionOpcodeHandlerTableOffset = 0x3AF1A0;
    public const int BattleActionOpcodeCount = 0x40;
    public const int PartSize = 0x10;
    public const int PartCount = 481;
    public const int EncounterSize = 4;
    public const int EncounterCount = 192;
    public const int MapCount = 192;
    public const int MapLayerCount = 3;
    public const int MapPaletteSize = 0x200;
    public const int MapTilemapHeaderSize = 0x10;
    public const int MapTilesetSheetTileWidth = 16;
    public const int MapDimensionsInMetaTilesTableOffset = 0x3F8920;
    public const int MapTilesetGraphicsPointerTableOffset = 0x3F8AA0;
    public const int MapColorAttributePointerTableOffset = 0x3F8DA0;
    public const int MapLayerTilemapPointerTableOffset = 0x3F90A0;
    public const int MapTilesetPalettePointerTableOffset = 0x3F99A0;

    public const byte EventConditionalMultiJumpOpcode = 0x2F;
    public const byte EventEndOpcode = 0x06;
    public const byte EventGotoEventOpcode = 0x19;
    public const int EventMultiJumpMaxEntries = 8;
    public const int EventBankTableOffset = 0x11E0;
    public const int EventBankSize = 0x4000;
    public const int EventBankBaseAddress = 0x4000;
    public const int EventBankBias = 2;
    public const int EventMoveMask = 0xF0;
    public const int EventMoveDistanceMask = 0x0F;
    public const int EventMoveNorth = 0x00;
    public const int EventMoveSouth = 0x10;
    public const int EventMoveWest = 0x20;
    public const int EventMoveEast = 0x30;
    public const int EventMoveNone = 0xFF;

    public const int PortraitPointerTableOffset = 0x3AFEA8;
    public const int PortraitPaletteTableOffset = 0x3B1768;
    public const int PortraitCharacterCount = 176;
    public const int PortraitTileWidth = 8;
    public const int CompositeBattleSpritePointerTableOffset = 0x3B1A28;
    public const int CompositeBattleSpritePointersPerPart = 6;
    public const int CompositeBattleSpritePartCount = 0x78;
    public const int CompositeBattleSpritePaletteFamilyTableOffset = 0x3BEBB4;
    public const int CompositeBattleSpritePaletteDataOffset = 0x50F31C;
    public const int CompositeBattleSpritePaletteCount = 6;
    public const int PartSelectionComponentPaletteSetOffset = 0x50F4BC;
    public const int CompositePreviewDescriptorPointerTableOffset = 0x3D1D98;
    public const int CompositePreviewHeadAppearanceTableOffset = 0x07EE90;
    public const int CompositePreviewRightArmAppearanceTableOffset = 0x07F630;
    public const int CompositePreviewLeftArmAppearanceTableOffset = 0x07FDD0;
    public const int CompositePreviewLegsAppearanceTableOffset = 0x080570;
    public const int PartDetailObjPaletteBlockAOffset = 0x50F33C;
    public const int PartDetailObjPaletteBlockBOffset = 0x50F39C;
    public const int PartDetailObjPaletteBlockCOffset = 0x4E441C;
    public const int SharedPartScreenTileSheetOffset = 0x785FD8;
    public const int SharedPartScreenTileSheetTileWidth = 8;
    public const int SharedPartDetailTileSheetAOffset = 0x4C4C00;
    public const int SharedPartDetailTileSheetBOffset = 0x4C4FE0;
    public const int SharedPartDetailTileSheetCOffset = 0x4C4B70;
    public const int SharedPartDetailTileSheetDOffset = 0x4C5020;
    public const int SharedPartDetailTileSheetEOffset = 0x4C53EC;
    public const int SharedPartDetailTileSheetFOffset = 0x4C3E88;
    public const int SharedPartDetailTileSheetGOffset = 0x4C88DC;
    public const int SpritePointerTableOffset = 0x3F7EA0;
    public const int SpritePaletteTableOffset = 0x3F83E0;
    public const int SpriteCount = 336;

    public static RomPatternLocator PartTable { get; } = new(
        "the part table",
        [0x0F, 0x22, 0x02, 0x00, 0x23, 0x15, 0x08, 0x01, 0x08, 0x00]);

    public static RomPatternLocator EncounterTable { get; } = new(
        "the encounter table",
        [0x00, 0x00, 0x00, 0x00, 0x9B, 0xF5, 0x9B, 0xA7, 0x9B, 0xF5]);

    public static RomPatternLocator ShopTable { get; } = new(
        "the shop table",
        [0x13, 0x00, 0xFF, 0xFF, 0x13, 0x00, 0x42, 0xFF, 0x13, 0x00]);

    public static RomPatternLocator StarterMedalSlot { get; } = new(
        "the starter medal slot",
        [0x01, 0x02, 0x00, 0x56, 0x5D, 0x01, 0x62, 0x17, 0x01],
        -1);
}
