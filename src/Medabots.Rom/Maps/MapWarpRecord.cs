namespace Medabots.Rom.Maps;

public sealed record MapWarpRecord(
    byte TileX,
    byte TileY,
    byte DestinationMapId,
    byte ArrivalFacingAndTransitionKind,
    byte Unknown4,
    byte Unknown5,
    byte DestinationTileX,
    byte DestinationTileY)
{
    public int TransitionKind => (ArrivalFacingAndTransitionKind >> 4) & 0xF;
    public int ArrivalFacing => ArrivalFacingAndTransitionKind & 0x7;
}
