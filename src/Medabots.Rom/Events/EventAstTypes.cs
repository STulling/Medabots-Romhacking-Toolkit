using Medabots.Rom.Metadata;

namespace Medabots.Rom.Events;

public readonly record struct PackedTrackedObjectId(byte RawValue)
{
    public byte TrackedObjectSlot => (byte)(RawValue & 0x0F);

    public byte Flags => (byte)(RawValue & 0xF0);

    public bool HasTransitionFlags => (RawValue & 0xF0) != 0;

    public bool AdjustSpawnYOffset => (RawValue & 0x40) != 0;
}

public readonly record struct TrackedObjectSlot(byte Value);

public readonly record struct EventDirection(byte RawValue)
{
    public string Name => RawValue switch
    {
        0 => "north",
        1 => "south",
        2 => "west",
        3 => "east",
        _ => RawValue.ToString()
    };
}

public readonly record struct EventMove(byte RawValue)
{
    public int Distance => RawValue & MedabotsRomSchema.EventMoveDistanceMask;

    public EventDirection Direction => new((byte)(RawValue & MedabotsRomSchema.EventMoveMask));
}

public readonly record struct BattleId(byte Value);

public readonly record struct BattleModeFlags(byte Value);

public readonly record struct PostBattleModeFlags(byte Value);

public sealed record class ConditionalMultiJumpInstruction(
    int Offset,
    byte Opcode,
    IReadOnlyList<EventArgumentValue> Arguments,
    string DisplayText,
    bool IsTerminal)
    : EventInstruction(Offset, Opcode, "Conditional_Multijump", Arguments, DisplayText, IsTerminal)
{
    public override string AstKind => nameof(ConditionalMultiJumpInstruction);
}

public sealed record class EndInstruction(int Offset, byte Opcode)
    : EventInstruction(Offset, Opcode, "END", [], "<END>", true)
{
    public override string AstKind => nameof(EndInstruction);
}

public sealed record class GotoEventInstruction(int Offset, byte Opcode, short EventId)
    : EventInstruction(
        Offset,
        Opcode,
        "GOTO_EVENT",
        [new EventArgumentValue("event_id", EventArgumentType.Short, EventId, EventId.ToString())],
        $"<GOTO_EVENT: {EventId}>",
        true)
{
    public override string AstKind => nameof(GotoEventInstruction);
}

public sealed record class UnknownOpcodeInstruction(
    int Offset,
    byte Opcode,
    IReadOnlyList<EventArgumentValue> Arguments,
    string DisplayText,
    bool IsTerminal)
    : EventInstruction(Offset, Opcode, $"UNKNOWN_{Opcode:X2}", Arguments, DisplayText, IsTerminal)
{
    public override string AstKind => nameof(UnknownOpcodeInstruction);
}

public sealed record class InvalidOpcodeInstruction(int Offset, byte Opcode)
    : EventInstruction(Offset, Opcode, $"INVALID_{Opcode:X2}", [], $"<INVALID_{Opcode:X2}>", true)
{
    public override string AstKind => nameof(InvalidOpcodeInstruction);
}

public sealed record class ShowMessageInstruction(
    int Offset,
    byte Opcode,
    string Name,
    IReadOnlyList<EventArgumentValue> Arguments,
    string DisplayText,
    bool IsTerminal,
    int Bank,
    int MessageId)
    : EventInstruction(Offset, Opcode, Name, Arguments, DisplayText, IsTerminal)
{
    public override string AstKind => nameof(ShowMessageInstruction);
}

public sealed record class StartBattleInstruction(
    int Offset,
    byte Opcode,
    IReadOnlyList<EventArgumentValue> Arguments,
    string DisplayText,
    bool IsTerminal,
    BattleId Battle,
    BattleModeFlags BattleModeFlags,
    PostBattleModeFlags PostBattleModeFlags)
    : EventInstruction(Offset, Opcode, "Start_Battle", Arguments, DisplayText, IsTerminal)
{
    public override string AstKind => nameof(StartBattleInstruction);
}

public sealed record class InitiateActorInstruction(
    int Offset,
    byte Opcode,
    string Name,
    IReadOnlyList<EventArgumentValue> Arguments,
    string DisplayText,
    bool IsTerminal,
    PackedTrackedObjectId PackedActorId,
    int SpriteId,
    int X,
    int Y)
    : EventInstruction(Offset, Opcode, Name, Arguments, DisplayText, IsTerminal)
{
    public override string AstKind => nameof(InitiateActorInstruction);
}

public sealed record class MoveActorInstruction(
    int Offset,
    byte Opcode,
    string Name,
    IReadOnlyList<EventArgumentValue> Arguments,
    string DisplayText,
    bool IsTerminal,
    TrackedObjectSlot TrackedObjectSlot,
    EventMove Move)
    : EventInstruction(Offset, Opcode, Name, Arguments, DisplayText, IsTerminal)
{
    public override string AstKind => nameof(MoveActorInstruction);
}

public sealed record class RotateActorInstruction(
    int Offset,
    byte Opcode,
    IReadOnlyList<EventArgumentValue> Arguments,
    string DisplayText,
    bool IsTerminal,
    TrackedObjectSlot TrackedObjectSlot,
    EventDirection Direction)
    : EventInstruction(Offset, Opcode, "Rotate_Actor", Arguments, DisplayText, IsTerminal)
{
    public override string AstKind => nameof(RotateActorInstruction);
}

public sealed record class UnloadActorInstruction(
    int Offset,
    byte Opcode,
    IReadOnlyList<EventArgumentValue> Arguments,
    string DisplayText,
    bool IsTerminal,
    PackedTrackedObjectId PackedActorId)
    : EventInstruction(Offset, Opcode, "Unload_Actor", Arguments, DisplayText, IsTerminal)
{
    public override string AstKind => nameof(UnloadActorInstruction);
}

public sealed record class FlickerActorInstruction(
    int Offset,
    byte Opcode,
    IReadOnlyList<EventArgumentValue> Arguments,
    string DisplayText,
    bool IsTerminal,
    TrackedObjectSlot TrackedObjectSlot,
    int Frames)
    : EventInstruction(Offset, Opcode, "Flicker_Actor", Arguments, DisplayText, IsTerminal)
{
    public override string AstKind => nameof(FlickerActorInstruction);
}

public sealed record class ActorAnimationInstruction(
    int Offset,
    byte Opcode,
    IReadOnlyList<EventArgumentValue> Arguments,
    string DisplayText,
    bool IsTerminal,
    TrackedObjectSlot TrackedObjectSlot,
    int AnimationId,
    int Frames)
    : EventInstruction(Offset, Opcode, "Actor_Do_Animation", Arguments, DisplayText, IsTerminal)
{
    public override string AstKind => nameof(ActorAnimationInstruction);
}

public sealed record class HopActorInstruction(
    int Offset,
    byte Opcode,
    IReadOnlyList<EventArgumentValue> Arguments,
    string DisplayText,
    bool IsTerminal,
    TrackedObjectSlot TrackedObjectSlot)
    : EventInstruction(Offset, Opcode, "Hop_Actor", Arguments, DisplayText, IsTerminal)
{
    public override string AstKind => nameof(HopActorInstruction);
}

public sealed record class SetMapSceneVariantInstruction(
    int Offset,
    byte Opcode,
    IReadOnlyList<EventArgumentValue> Arguments,
    string DisplayText,
    bool IsTerminal,
    int Variant,
    int SkipFullReload)
    : EventInstruction(Offset, Opcode, "Set_Map_Scene_Variant", Arguments, DisplayText, IsTerminal)
{
    public override string AstKind => nameof(SetMapSceneVariantInstruction);
}
