namespace Medabots.Rom.Battles;

public sealed record BattleBot(
    byte Unknown,
    byte HeadPartId,
    byte RightArmPartId,
    byte LeftArmPartId,
    byte LegsPartId,
    byte MedalId,
    byte MedalLevel,
    byte Unknown1,
    byte Unknown2,
    byte Unknown3,
    byte Unknown4,
    byte Unknown5);
