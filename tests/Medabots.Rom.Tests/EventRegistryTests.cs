using Medabots.Rom.Events;
using Xunit;

namespace Medabots.Rom.Tests;

public sealed class EventRegistryTests
{
    [Fact]
    public void Registry_LoadsKnownOperations()
    {
        var registry = EventOperationRegistry.LoadDefault();

        Assert.True(registry.TryGetDefinition(0x01, out var definition));
        Assert.Equal("Show_Message_A", definition.Name);
        Assert.Equal(2, definition.Arguments.Count);
        Assert.Equal(EventArgumentType.EventBank, definition.Arguments[0].Type);
        Assert.Equal(EventArgumentType.Short, definition.Arguments[1].Type);
    }

    [Theory]
    [InlineData(0x29, "Move_All_Tracked_Actors_A", 1)]
    [InlineData(0x2A, "Move_All_Tracked_Actors_B", 1)]
    [InlineData(0x2B, "Initiate_Free_Tracked_Actor", 5)]
    [InlineData(0x2C, "Set_Actor_Render_Priority", 1)]
    [InlineData(0x2D, "Move_Actor_C", 2)]
    [InlineData(0x2E, "Adjust_Value_If_No_Spare_Part", 3)]
    [InlineData(0x2F, "Set_Value_From_EventFlag_Nibble", 2)]
    [InlineData(0x35, "Set_All_Tracked_Actors_Facing_Variant", 1)]
    [InlineData(0x3E, "Increase_Capped_Event_Counter", 2)]
    [InlineData(0x3F, "Decrease_Capped_Event_Counter", 2)]
    [InlineData(0x40, "Pulse_Screen_Transition", 1)]
    [InlineData(0x41, "Flicker_All_Tracked_Actors", 1)]
    [InlineData(0x42, "Move_Player_One_Step", 0)]
    [InlineData(0x43, "Shake_Camera", 2)]
    [InlineData(0x4F, "Jump_If_Previous_Scene_Command_Failed", 1)]
    [InlineData(0x55, "Reserved_NoOp", 0)]
    [InlineData(0x57, "Reserved_NoOp", 0)]
    [InlineData(0x5A, "Set_Secondary_Marker_Facing_Variant", 1)]
    [InlineData(0x5B, "Jump_If_Fewer_Than_Three_Complete_Part_Sets", 1)]
    [InlineData(0x5C, "Jump_If_Missing_Complete_Monochrome_Part_Set", 2)]
    [InlineData(0x5D, "Update_Overworld_Event_Mode_Flags", 1)]
    [InlineData(0x5E, "Set_Overworld_Event_Mode_Bit0", 1)]
    [InlineData(0x5F, "Set_Random_Encounter_Mode_Flags", 1)]
    [InlineData(0x60, "Focus_Event_Object", 2)]
    [InlineData(0x61, "Restore_Focused_Event_Mode_Flags", 0)]
    [InlineData(0x62, "Equip_Starter_Parts", 0)]
    [InlineData(0x63, "Show_Effect", 3)]
    [InlineData(0x64, "Reset_Overworld_UI_Render_State", 0)]
    [InlineData(0x65, "Reserved_NoOp", 0)]
    [InlineData(0x66, "Animate_Tracked_Object_Transition", 2)]
    [InlineData(0x68, "Clear_Collision_For_Packed_Object", 1)]
    [InlineData(0x6B, "Set_Current_Chapter", 1)]
    [InlineData(0x6C, "Initiate_Moving_Actor_C", 5)]
    [InlineData(0x6D, "Move_Actor_D", 2)]
    [InlineData(0x6E, "Initiate_Moving_Actor_D", 5)]
    [InlineData(0x6F, "Move_Actor_E", 2)]
    [InlineData(0x70, "Run_Scene_Command_13", 0)]
    [InlineData(0x71, "Run_Scene_Command_00", 0)]
    [InlineData(0x72, "Run_Scene_Command_02", 0)]
    [InlineData(0x73, "Run_Scene_Command_08", 0)]
    [InlineData(0x74, "Reserved_NoOp", 0)]
    [InlineData(0x78, "Run_Scene_Command_10_If_At_Least_Three_Complete_Part_Sets", 2)]
    [InlineData(0x79, "Run_Scene_Command_12", 1)]
    [InlineData(0x7A, "Run_Scene_Command_11", 0)]
    [InlineData(0x7B, "Jump_If_No_Spare_Part", 3)]
    [InlineData(0x7C, "Reserved_NoOp", 0)]
    [InlineData(0x7E, "Move_All_Tracked_Actors_C", 1)]
    [InlineData(0x80, "Begin_Batched_Object_Command_Block", 0)]
    [InlineData(0x81, "Queue_Batched_Object_Command", 8)]
    [InlineData(0x82, "Execute_Batched_Object_Command_Block", 0)]
    [InlineData(0x83, "Set_Object_Render_Mode", 2)]
    [InlineData(0x84, "Set_Map_Scene_Variant_When_Player_Faces_Up", 1)]
    [InlineData(0x85, "Begin_Event_Object_Facing_Cycle_Block", 0)]
    [InlineData(0x86, "Queue_Event_Object_Facing_Cycle", 8)]
    [InlineData(0x87, "Execute_Event_Object_Facing_Cycle_Block", 0)]
    public void Registry_LoadsReverseEngineeredOperations(byte opcode, string expectedName, int expectedArgumentCount)
    {
        var registry = EventOperationRegistry.LoadDefault();

        Assert.True(registry.TryGetDefinition(opcode, out var definition));
        Assert.Equal(expectedName, definition.Name);
        Assert.Equal(expectedArgumentCount, definition.Arguments.Count);
    }

    [Fact]
    public void Registry_TypesFreeTrackedActorAndMoveActorCArgumentsConsistently()
    {
        var registry = EventOperationRegistry.LoadDefault();

        Assert.True(registry.TryGetDefinition(0x2B, out var freeActor));
        Assert.Equal(EventArgumentType.PackedTrackedObjectId, freeActor.Arguments[0].Type);
        Assert.Equal(EventArgumentType.Move, freeActor.Arguments[4].Type);

        Assert.True(registry.TryGetDefinition(0x2D, out var moveActorC));
        Assert.Equal(EventArgumentType.TrackedObjectSlot, moveActorC.Arguments[0].Type);
        Assert.Equal(EventArgumentType.Move, moveActorC.Arguments[1].Type);
    }
}
