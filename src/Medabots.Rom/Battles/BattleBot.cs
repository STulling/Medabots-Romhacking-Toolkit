namespace Medabots.Rom.Battles;

public sealed record BattleBot(
    byte HeadPartId,
    byte RightArmPartId,
    byte LeftArmPartId,
    byte LegsPartId,
    byte MedalId,
    byte MedalLevel,
    byte PackedSpecialitySeedByte0,
    byte PackedSpecialitySeedByte1,
    byte PackedSpecialitySeedByte2,
    byte PackedSpecialitySeedByte3,
    byte SpecialityCycleResetValue,
    byte ReservedZeroByte);
