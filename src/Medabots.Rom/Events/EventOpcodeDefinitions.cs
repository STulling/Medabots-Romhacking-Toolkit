namespace Medabots.Rom.Events;

internal static class EventOpcodeDefinitions
{
    public static IReadOnlyDictionary<byte, EventOperationDefinition> Create()
    {
        return new Dictionary<byte, EventOperationDefinition>
        {
            [0x00] = new EventOperationDefinition(
                0x00,
                "Nop",
                Array.Empty<EventArgumentDefinition>()),
            [0x01] = new EventOperationDefinition(
                0x01,
                "Show_Message_A",
                new EventArgumentDefinition[]
                {
                    new("bank", EventArgumentType.EventBank),
                    new("id", EventArgumentType.Short),
                }),
            [0x02] = new EventOperationDefinition(
                0x02,
                "Show_Message_B",
                new EventArgumentDefinition[]
                {
                    new("bank", EventArgumentType.EventBank),
                    new("id", EventArgumentType.Short),
                }),
            [0x03] = new EventOperationDefinition(
                0x03,
                "Close_Message_Box",
                Array.Empty<EventArgumentDefinition>()),
            [0x04] = new EventOperationDefinition(
                0x04,
                "Wait_X_Frames",
                new EventArgumentDefinition[]
                {
                    new("frames", EventArgumentType.Byte),
                }),
            [0x05] = new EventOperationDefinition(
                0x05,
                "Wait_For_Button_Press",
                Array.Empty<EventArgumentDefinition>()),
            [0x07] = new EventOperationDefinition(
                0x07,
                "Fade_To_Black",
                new EventArgumentDefinition[]
                {
                    new("frames", EventArgumentType.Byte),
                }),
            [0x08] = new EventOperationDefinition(
                0x08,
                "Fade_From_Black",
                new EventArgumentDefinition[]
                {
                    new("frames", EventArgumentType.Byte),
                }),
            [0x09] = new EventOperationDefinition(
                0x09,
                "Fade_To_White",
                new EventArgumentDefinition[]
                {
                    new("frames", EventArgumentType.Byte),
                }),
            [0x0A] = new EventOperationDefinition(
                0x0A,
                "Fade_From_White",
                new EventArgumentDefinition[]
                {
                    new("frames", EventArgumentType.Byte),
                }),
            [0x0B] = new EventOperationDefinition(
                0x0B,
                "Warp_A",
                new EventArgumentDefinition[]
                {
                    new("map_id", EventArgumentType.Byte),
                    new("unk1", EventArgumentType.Byte),
                    new("unk2", EventArgumentType.Byte),
                    new("x", EventArgumentType.Byte),
                    new("y", EventArgumentType.Byte),
                }),
            [0x0C] = new EventOperationDefinition(
                0x0C,
                "Warp_B",
                new EventArgumentDefinition[]
                {
                    new("map_id", EventArgumentType.Byte),
                    new("unk1", EventArgumentType.Byte),
                    new("unk2", EventArgumentType.Byte),
                    new("x", EventArgumentType.Byte),
                    new("y", EventArgumentType.Byte),
                }),
            [0x0D] = new EventOperationDefinition(
                0x0D,
                "Player_Face_Direction",
                new EventArgumentDefinition[]
                {
                    new("dir", EventArgumentType.Direction),
                }),
            [0x0E] = new EventOperationDefinition(
                0x0E,
                "Player_Hop_In_Place",
                Array.Empty<EventArgumentDefinition>()),
            [0x0F] = new EventOperationDefinition(
                0x0F,
                "Player_Hop_In_Direction",
                new EventArgumentDefinition[]
                {
                    new("dir", EventArgumentType.Direction),
                }),
            [0x10] = new EventOperationDefinition(
                0x10,
                "Move_Player",
                new EventArgumentDefinition[]
                {
                    new("step1", EventArgumentType.Move),
                    new("step2", EventArgumentType.Move),
                    new("step3", EventArgumentType.Move),
                    new("step4", EventArgumentType.Move),
                }),
            [0x11] = new EventOperationDefinition(
                0x11,
                "Change_Player_Costume",
                new EventArgumentDefinition[]
                {
                    new("costume_id", EventArgumentType.Byte),
                }),
            [0x12] = new EventOperationDefinition(
                0x12,
                "Add_Event_Permanently",
                new EventArgumentDefinition[]
                {
                    new("event_id", EventArgumentType.Short),
                }),
            [0x13] = new EventOperationDefinition(
                0x13,
                "Remove_Event_Permanently",
                new EventArgumentDefinition[]
                {
                    new("event_id", EventArgumentType.Short),
                }),
            [0x14] = new EventOperationDefinition(
                0x14,
                "Remove_Current_Event_Now",
                Array.Empty<EventArgumentDefinition>()),
            [0x15] = new EventOperationDefinition(
                0x15,
                "Remove_Current_Event_Now_Keep_Collision",
                Array.Empty<EventArgumentDefinition>()),
            [0x16] = new EventOperationDefinition(
                0x16,
                "Remove_Event_Now",
                new EventArgumentDefinition[]
                {
                    new("event_id", EventArgumentType.Short),
                }),
            [0x17] = new EventOperationDefinition(
                0x17,
                "Set_Event_Variable",
                new EventArgumentDefinition[]
                {
                    new("event_id", EventArgumentType.Short),
                    new("value", EventArgumentType.Byte),
                }),
            [0x18] = new EventOperationDefinition(
                0x18,
                "Increment_Event_Variable",
                new EventArgumentDefinition[]
                {
                    new("event_id", EventArgumentType.Short),
                }),
            [0x1A] = new EventOperationDefinition(
                0x1A,
                "NPC_Do_Animation",
                new EventArgumentDefinition[]
                {
                    new("animation_id", EventArgumentType.Byte),
                    new("frames", EventArgumentType.Byte),
                }),
            [0x1B] = new EventOperationDefinition(
                0x1B,
                "NPC_Reset_Animation",
                Array.Empty<EventArgumentDefinition>()),
            [0x1C] = new EventOperationDefinition(
                0x1C,
                "Rotate_Event_NPC",
                new EventArgumentDefinition[]
                {
                    new("dir", EventArgumentType.Direction),
                }),
            [0x1D] = new EventOperationDefinition(
                0x1D,
                "Hop_Event_NPC",
                Array.Empty<EventArgumentDefinition>()),
            [0x1E] = new EventOperationDefinition(
                0x1E,
                "Flicker_Event_NPC",
                new EventArgumentDefinition[]
                {
                    new("frames", EventArgumentType.Byte),
                }),
            [0x1F] = new EventOperationDefinition(
                0x1F,
                "Initiate_Moving_Actor_A",
                new EventArgumentDefinition[]
                {
                    new("packed_actor_id", EventArgumentType.PackedTrackedObjectId),
                    new("sprite_id", EventArgumentType.Byte),
                    new("x", EventArgumentType.Byte),
                    new("y", EventArgumentType.Byte),
                    new("move", EventArgumentType.Move),
                }),
            [0x20] = new EventOperationDefinition(
                0x20,
                "Initiate_Moving_Actor_B",
                new EventArgumentDefinition[]
                {
                    new("packed_actor_id", EventArgumentType.PackedTrackedObjectId),
                    new("sprite_id", EventArgumentType.Byte),
                    new("x", EventArgumentType.Byte),
                    new("y", EventArgumentType.Byte),
                    new("move", EventArgumentType.Move),
                }),
            [0x21] = new EventOperationDefinition(
                0x21,
                "Initiate_Actor",
                new EventArgumentDefinition[]
                {
                    new("packed_actor_id", EventArgumentType.PackedTrackedObjectId),
                    new("sprite_id", EventArgumentType.Byte),
                    new("x", EventArgumentType.Byte),
                    new("y", EventArgumentType.Byte),
                }),
            [0x22] = new EventOperationDefinition(
                0x22,
                "Rotate_Actor",
                new EventArgumentDefinition[]
                {
                    new("tracked_object_slot", EventArgumentType.TrackedObjectSlot),
                    new("dir", EventArgumentType.Direction),
                }),
            [0x23] = new EventOperationDefinition(
                0x23,
                "Move_Actor_A",
                new EventArgumentDefinition[]
                {
                    new("tracked_object_slot", EventArgumentType.TrackedObjectSlot),
                    new("move", EventArgumentType.Move),
                }),
            [0x24] = new EventOperationDefinition(
                0x24,
                "Move_Actor_B",
                new EventArgumentDefinition[]
                {
                    new("tracked_object_slot", EventArgumentType.TrackedObjectSlot),
                    new("move", EventArgumentType.Move),
                }),
            [0x25] = new EventOperationDefinition(
                0x25,
                "Unload_Actor",
                new EventArgumentDefinition[]
                {
                    new("packed_actor_id", EventArgumentType.PackedTrackedObjectId),
                }),
            [0x26] = new EventOperationDefinition(
                0x26,
                "Flicker_Actor",
                new EventArgumentDefinition[]
                {
                    new("tracked_object_slot", EventArgumentType.TrackedObjectSlot),
                    new("frames", EventArgumentType.Byte),
                }),
            [0x27] = new EventOperationDefinition(
                0x27,
                "Actor_Do_Animation",
                new EventArgumentDefinition[]
                {
                    new("tracked_object_slot", EventArgumentType.TrackedObjectSlot),
                    new("animation_id", EventArgumentType.Byte),
                    new("frames", EventArgumentType.Byte),
                }),
            [0x28] = new EventOperationDefinition(
                0x28,
                "Hop_Actor",
                new EventArgumentDefinition[]
                {
                    new("tracked_object_slot", EventArgumentType.TrackedObjectSlot),
                }),
            [0x30] = new EventOperationDefinition(
                0x30,
                "Relative_Long_Jump",
                new EventArgumentDefinition[]
                {
                    new("jump", EventArgumentType.Short),
                }),
            [0x31] = new EventOperationDefinition(
                0x31,
                "Yes_or_No_Box",
                new EventArgumentDefinition[]
                {
                    new("jump", EventArgumentType.Byte),
                }),
            [0x32] = new EventOperationDefinition(
                0x32,
                "No_or_Yes_Box",
                new EventArgumentDefinition[]
                {
                    new("jump", EventArgumentType.Byte),
                }),
            [0x33] = new EventOperationDefinition(
                0x33,
                "Start_Battle",
                new EventArgumentDefinition[]
                {
                    new("battle", EventArgumentType.BattleId),
                    new("battle_mode_flags", EventArgumentType.BattleModeFlags),
                    new("post_battle_mode_flags", EventArgumentType.PostBattleModeFlags),
                }),
            [0x34] = new EventOperationDefinition(
                0x34,
                "Rotate_NPC",
                new EventArgumentDefinition[]
                {
                    new("event_id", EventArgumentType.Short),
                    new("dir", EventArgumentType.Direction),
                }),
            [0x36] = new EventOperationDefinition(
                0x36,
                "Get_Item",
                new EventArgumentDefinition[]
                {
                    new("item_id", EventArgumentType.Byte),
                    new("amount", EventArgumentType.Byte),
                }),
            [0x37] = new EventOperationDefinition(
                0x37,
                "Lose_Item",
                new EventArgumentDefinition[]
                {
                    new("item_id", EventArgumentType.Byte),
                    new("amount", EventArgumentType.Byte),
                }),
            [0x38] = new EventOperationDefinition(
                0x38,
                "Get_Part",
                new EventArgumentDefinition[]
                {
                    new("bot", EventArgumentType.Bot),
                    new("part", EventArgumentType.Part),
                    new("amount", EventArgumentType.Byte),
                }),
            [0x39] = new EventOperationDefinition(
                0x39,
                "Lose_Part",
                new EventArgumentDefinition[]
                {
                    new("bot", EventArgumentType.Bot),
                    new("part", EventArgumentType.Part),
                    new("amount", EventArgumentType.Byte),
                }),
            [0x3A] = new EventOperationDefinition(
                0x3A,
                "Get_Money",
                new EventArgumentDefinition[]
                {
                    new("amount", EventArgumentType.Short),
                }),
            [0x3B] = new EventOperationDefinition(
                0x3B,
                "Lose_Money",
                new EventArgumentDefinition[]
                {
                    new("amount", EventArgumentType.Short),
                }),
            [0x3C] = new EventOperationDefinition(
                0x3C,
                "Get_Medal",
                new EventArgumentDefinition[]
                {
                    new("medal", EventArgumentType.Medal),
                }),
            [0x3D] = new EventOperationDefinition(
                0x3D,
                "Get_Tinpet",
                new EventArgumentDefinition[]
                {
                    new("tinpet", EventArgumentType.Byte),
                }),
            [0x44] = new EventOperationDefinition(
                0x44,
                "Play_Sound",
                new EventArgumentDefinition[]
                {
                    new("sound_id", EventArgumentType.Sound),
                }),
            [0x45] = new EventOperationDefinition(
                0x45,
                "Play_Music",
                new EventArgumentDefinition[]
                {
                    new("sound_id", EventArgumentType.Byte),
                }),
            [0x46] = new EventOperationDefinition(
                0x46,
                "Stop_Music",
                Array.Empty<EventArgumentDefinition>()),
            [0x47] = new EventOperationDefinition(
                0x47,
                "Fade_Out_Music",
                Array.Empty<EventArgumentDefinition>()),
            [0x48] = new EventOperationDefinition(
                0x48,
                "Play_Persistent_Music",
                new EventArgumentDefinition[]
                {
                    new("music_id", EventArgumentType.Music),
                }),
            [0x49] = new EventOperationDefinition(
                0x49,
                "Play_Map_Music",
                Array.Empty<EventArgumentDefinition>()),
            [0x4A] = new EventOperationDefinition(
                0x4A,
                "Jump_If_Not_Player_Direction",
                new EventArgumentDefinition[]
                {
                    new("dir", EventArgumentType.Direction),
                    new("jump", EventArgumentType.Byte),
                }),
            [0x4B] = new EventOperationDefinition(
                0x4B,
                "Jump_If_Item_Count_Below_Amount",
                new EventArgumentDefinition[]
                {
                    new("item_id", EventArgumentType.Byte),
                    new("amount", EventArgumentType.Byte),
                    new("jump", EventArgumentType.Byte),
                }),
            [0x4C] = new EventOperationDefinition(
                0x4C,
                "Jump_If_Missing_Part",
                new EventArgumentDefinition[]
                {
                    new("bot", EventArgumentType.Bot),
                    new("part", EventArgumentType.Part),
                    new("jump", EventArgumentType.Byte),
                }),
            [0x4D] = new EventOperationDefinition(
                0x4D,
                "Jump_If_Not_Has_Medal",
                new EventArgumentDefinition[]
                {
                    new("medal_id", EventArgumentType.Medal),
                    new("jump", EventArgumentType.Byte),
                }),
            [0x4E] = new EventOperationDefinition(
                0x4E,
                "Jump_If_Not_Has_Money",
                new EventArgumentDefinition[]
                {
                    new("money", EventArgumentType.Short),
                    new("jump", EventArgumentType.Byte),
                }),
            [0x4F] = new EventOperationDefinition(
                0x4F,
                "Jump_If_Lost_Robattle",
                new EventArgumentDefinition[]
                {
                    new("jump", EventArgumentType.Byte),
                }),
            [0x50] = new EventOperationDefinition(
                0x50,
                "Jump_If_Current_Active_Object_Index_Not_Equal",
                new EventArgumentDefinition[]
                {
                    new("active_object_index", EventArgumentType.Byte),
                    new("jump", EventArgumentType.Byte),
                }),
            [0x51] = new EventOperationDefinition(
                0x51,
                "Random_Jump",
                new EventArgumentDefinition[]
                {
                    new("reserved", EventArgumentType.Byte),
                    new("jump", EventArgumentType.Byte),
                }),
            [0x53] = new EventOperationDefinition(
                0x53,
                "Set_Object_Render_Mode",
                new EventArgumentDefinition[]
                {
                    new("target_flags", EventArgumentType.Byte),
                    new("render_mode", EventArgumentType.Byte),
                }),
            [0x56] = new EventOperationDefinition(
                0x56,
                "Load_Entities",
                Array.Empty<EventArgumentDefinition>()),
            [0x59] = new EventOperationDefinition(
                0x59,
                "Open_Shop",
                new EventArgumentDefinition[]
                {
                    new("shop_id", EventArgumentType.Byte),
                }),
            [0x5A] = new EventOperationDefinition(
                0x5A,
                "Jump_If_Fewer_Than_Three_Complete_Part_Sets",
                new EventArgumentDefinition[]
                {
                    new("jump", EventArgumentType.Byte),
                }),
            [0x5B] = new EventOperationDefinition(
                0x5B,
                "Jump_If_Missing_Complete_Monochrome_Part_Set",
                new EventArgumentDefinition[]
                {
                    new("part_id", EventArgumentType.Byte),
                    new("jump", EventArgumentType.Byte),
                }),
            [0x5D] = new EventOperationDefinition(
                0x5D,
                "Set_Overworld_Event_Mode_Bit0",
                new EventArgumentDefinition[]
                {
                    new("enabled", EventArgumentType.Byte),
                }),
            [0x5E] = new EventOperationDefinition(
                0x5E,
                "Set_Random_Encounter_Mode_Flags",
                new EventArgumentDefinition[]
                {
                    new("flags", EventArgumentType.Byte),
                }),
            [0x5F] = new EventOperationDefinition(
                0x5F,
                "Focus_Event_Object",
                new EventArgumentDefinition[]
                {
                    new("target_mode", EventArgumentType.Byte),
                    new("target_packed_object_id", EventArgumentType.Short),
                }),
            [0x62] = new EventOperationDefinition(
                0x62,
                "Equip_Starter_Parts",
                Array.Empty<EventArgumentDefinition>()),
            [0x63] = new EventOperationDefinition(
                0x63,
                "Show_Effect",
                new EventArgumentDefinition[]
                {
                    new("effect_id", EventArgumentType.Byte),
                    new("x", EventArgumentType.Byte),
                    new("y", EventArgumentType.Byte),
                }),
            [0x66] = new EventOperationDefinition(
                0x66,
                "Animate_Tracked_Object_Transition",
                new EventArgumentDefinition[]
                {
                    new("tracked_object_index", EventArgumentType.TrackedObjectSlot),
                }),
            [0x69] = new EventOperationDefinition(
                0x69,
                "Set_Map_Scene_Variant",
                new EventArgumentDefinition[]
                {
                    new("variant", EventArgumentType.MapSceneVariant),
                    new("skip_full_reload", EventArgumentType.Byte),
                }),
            [0x76] = new EventOperationDefinition(
                0x76,
                "Start_Medabot_Link",
                Array.Empty<EventArgumentDefinition>()),
        };
    }
}