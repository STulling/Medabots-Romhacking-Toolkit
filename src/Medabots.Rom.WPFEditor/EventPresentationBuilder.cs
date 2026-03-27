using System.Text;
using Medabots.Rom.Editor;
using Medabots.Rom.Events;
using Medabots.Rom.Metadata;
using Medabots.Rom.Text;

namespace Medabots.Rom.WPFEditor;

internal sealed class EventPresentationBuilder(
    MedabotsMetadata metadata,
    IReadOnlyDictionary<MessageId, string> loadedMessages,
    IReadOnlyDictionary<short, Dictionary<int, string>> eventCustomLabels)
{
    public EventVisualState BuildVisualState(short eventId, EventScript script)
    {
        var labelMap = BuildEventLabelMap(eventId, script);
        var instructions = new List<EventInstructionItem>();
        var order = 0;

        foreach (var instruction in script.Instructions.OrderBy(instruction => instruction.Offset))
        {
            instructions.Add(BuildEventInstructionItem(++order, instruction, labelMap));
        }

        return new EventVisualState
        {
            Instructions = instructions,
            LabelMap = labelMap,
            OrderedLabels = labelMap.OrderBy(pair => pair.Key).Select(pair => $"{pair.Value} @ 0x{pair.Key:X}").ToArray()
        };
    }

    public static string BuildJumpArgumentHelpText(EventInstructionItem instructionItem, EventArgumentValue argument, IReadOnlyDictionary<int, string>? labelMap)
    {
        var targetOffset = instructionItem.Offset + argument.RawValue + 1;
        var targetLabel = labelMap?.TryGetValue(targetOffset, out var label) == true
            ? label
            : $"Label_{targetOffset:X}";
        if (string.Equals(instructionItem.Instruction?.Name, "Conditional_Multijump", StringComparison.Ordinal))
        {
            var branchIndex = ParseConditionalBranchIndex(argument.Name);
            return branchIndex >= 0
                ? $"Value {branchIndex} branch: {targetLabel}"
                : $"Branch target: {targetLabel}";
        }

        return $"{DescribeJumpBehavior(instructionItem.Instruction!)}: {targetLabel}";
    }

    public static EventOperationDefinition? ResolveEditorOperationDefinition(EventInstruction? instruction)
    {
        if (instruction is null)
        {
            return null;
        }

        if (instruction.Definition is not null)
        {
            return instruction.Definition;
        }

        if (string.Equals(instruction.Name, "Conditional_Multijump", StringComparison.Ordinal))
        {
            var arguments = instruction.Arguments
                .Select((argument, index) => new EventArgumentDefinition($"jump{index + 1}", EventArgumentType.Jump))
                .ToArray();
            return new EventOperationDefinition(instruction.Opcode, "Conditional_Multijump", arguments);
        }

        return null;
    }

    public static int ResolveJumpArgumentValue(EventArgumentEditorItem argument, int sourceOffset, IReadOnlyDictionary<int, string>? labelMap)
    {
        var rawText = argument.ValueText?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(rawText) && !LooksLikeNumericValue(rawText))
        {
            if (labelMap is not null)
            {
                foreach (var pair in labelMap)
                {
                    if (!string.Equals(pair.Value, rawText, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    return Math.Max(0, pair.Key - sourceOffset - 1);
                }
            }

            throw new InvalidOperationException($"Unknown label '{rawText}'.");
        }

        return argument.GetEditedValue();
    }

    private IReadOnlyDictionary<int, string> BuildEventLabelMap(short eventId, EventScript script)
    {
        var labels = new Dictionary<int, string>
        {
            [script.StartOffset] = "Start"
        };

        var nextLabelNumber = 1;
        foreach (var instruction in script.Instructions.OrderBy(instruction => instruction.Offset))
        {
            if (string.Equals(instruction.Name, "Conditional_Multijump", StringComparison.Ordinal))
            {
                foreach (var jump in instruction.Arguments.Where(argument => argument.Type == EventArgumentType.Jump).Select((argument, index) => (argument, index)))
                {
                    var targetOffset = instruction.Offset + jump.argument.RawValue + 1;
                    if (!labels.ContainsKey(targetOffset))
                    {
                        labels[targetOffset] = $"Branch{jump.index + 1}_{nextLabelNumber++}";
                    }
                }

                continue;
            }

            var jumpArgument = instruction.Arguments.FirstOrDefault(argument => string.Equals(argument.Name, "jump", StringComparison.Ordinal));
            if (jumpArgument is null)
            {
                continue;
            }

            var target = instruction.Offset + jumpArgument.RawValue + 1;
            if (labels.ContainsKey(target))
            {
                continue;
            }

            labels[target] = BuildLabelName(instruction, nextLabelNumber++);
        }

        if (eventCustomLabels.TryGetValue(eventId, out var customLabels))
        {
            foreach (var pair in customLabels)
            {
                labels[pair.Key] = pair.Value;
            }
        }

        return labels;
    }

    private EventInstructionItem BuildEventInstructionItem(int order, EventInstruction instruction, IReadOnlyDictionary<int, string> labelMap)
    {
        labelMap.TryGetValue(instruction.Offset, out var labelDisplay);
        var presentation = BuildEventInstructionPresentation(instruction);

        if (TryFormatMessageInstruction(instruction, out var messageLine))
        {
            return new EventInstructionItem
            {
                Instruction = instruction,
                Order = order,
                Offset = instruction.Offset,
                OffsetDisplay = $"0x{instruction.Offset:X}",
                Opcode = instruction.Opcode,
                Name = instruction.Name,
                Kind = instruction.AstKind,
                LabelDisplay = labelDisplay ?? string.Empty,
                Category = presentation.Category,
                CategoryBackgroundColor = presentation.BackgroundColor,
                CategoryTextColor = presentation.TextColor,
                AccentColor = presentation.AccentColor,
                Summary = messageLine,
                Detail = BuildMessageInstructionDetail(instruction),
                IsEditable = true,
                Arguments = instruction.Arguments
            };
        }

        if (string.Equals(instruction.Name, "Conditional_Multijump", StringComparison.Ordinal))
        {
            var branches = instruction.Arguments
                .Where(argument => argument.Type == EventArgumentType.Jump)
                .Select((argument, index) =>
                {
                    var target = instruction.Offset + argument.RawValue + 1;
                    var label = labelMap.TryGetValue(target, out var labelName) ? labelName : $"Label_{target:X}";
                    return $"Value {index} -> {label}";
                });

            return new EventInstructionItem
            {
                Instruction = instruction,
                Order = order,
                Offset = instruction.Offset,
                OffsetDisplay = $"0x{instruction.Offset:X}",
                Opcode = instruction.Opcode,
                Name = instruction.Name,
                Kind = instruction.AstKind,
                LabelDisplay = labelDisplay ?? string.Empty,
                Category = presentation.Category,
                CategoryBackgroundColor = presentation.BackgroundColor,
                CategoryTextColor = presentation.TextColor,
                AccentColor = presentation.AccentColor,
                Summary = "Branch on current event variable",
                Detail = string.Join(Environment.NewLine, branches),
                IsEditable = instruction.Definition is not null || instruction.Arguments.Count > 0,
                Arguments = instruction.Arguments
            };
        }

        if (string.Equals(instruction.Name, "END", StringComparison.Ordinal))
        {
            return new EventInstructionItem
            {
                Instruction = instruction,
                Order = order,
                Offset = instruction.Offset,
                OffsetDisplay = $"0x{instruction.Offset:X}",
                Opcode = instruction.Opcode,
                Name = instruction.Name,
                Kind = instruction.AstKind,
                LabelDisplay = labelDisplay ?? string.Empty,
                Category = presentation.Category,
                CategoryBackgroundColor = presentation.BackgroundColor,
                CategoryTextColor = presentation.TextColor,
                AccentColor = presentation.AccentColor,
                Summary = "End of event path",
                Detail = string.Empty
            };
        }

        if (string.Equals(instruction.Name, "GOTO_EVENT", StringComparison.Ordinal))
        {
            var eventId = instruction.Arguments.FirstOrDefault(argument => string.Equals(argument.Name, "event_id", StringComparison.Ordinal))?.RawValue ?? -1;
            return new EventInstructionItem
            {
                Instruction = instruction,
                Order = order,
                Offset = instruction.Offset,
                OffsetDisplay = $"0x{instruction.Offset:X}",
                Opcode = instruction.Opcode,
                Name = instruction.Name,
                Kind = instruction.AstKind,
                LabelDisplay = labelDisplay ?? string.Empty,
                Category = presentation.Category,
                CategoryBackgroundColor = presentation.BackgroundColor,
                CategoryTextColor = presentation.TextColor,
                AccentColor = presentation.AccentColor,
                Summary = $"Go to event {eventId}",
                Detail = "Transfers control to another event"
            };
        }

        var jumpArgument = instruction.Arguments.FirstOrDefault(argument => string.Equals(argument.Name, "jump", StringComparison.Ordinal));
        if (jumpArgument is not null)
        {
            var targetOffset = instruction.Offset + jumpArgument.RawValue + 1;
            var targetLabel = labelMap.TryGetValue(targetOffset, out var label) ? label : $"Label_{targetOffset:X}";
            return new EventInstructionItem
            {
                Instruction = instruction,
                Order = order,
                Offset = instruction.Offset,
                OffsetDisplay = $"0x{instruction.Offset:X}",
                Opcode = instruction.Opcode,
                Name = instruction.Name,
                Kind = instruction.AstKind,
                LabelDisplay = labelDisplay ?? string.Empty,
                Category = presentation.Category,
                CategoryBackgroundColor = presentation.BackgroundColor,
                CategoryTextColor = presentation.TextColor,
                AccentColor = presentation.AccentColor,
                Summary = FormatFriendlyInstruction(instruction),
                Detail = $"{DescribeJumpBehavior(instruction)}: {targetLabel}",
                IsEditable = true,
                Arguments = instruction.Arguments
            };
        }

        return new EventInstructionItem
        {
            Instruction = instruction,
            Order = order,
            Offset = instruction.Offset,
            OffsetDisplay = $"0x{instruction.Offset:X}",
            Opcode = instruction.Opcode,
            Name = instruction.Name,
            Kind = instruction.AstKind,
            LabelDisplay = labelDisplay ?? string.Empty,
            Category = presentation.Category,
            CategoryBackgroundColor = presentation.BackgroundColor,
            CategoryTextColor = presentation.TextColor,
            AccentColor = presentation.AccentColor,
            Summary = FormatFriendlyInstruction(instruction),
            Detail = string.Empty,
            IsEditable = instruction.Definition is not null || instruction.Arguments.Count > 0,
            Arguments = instruction.Arguments
        };
    }

    private static (string Category, string BackgroundColor, string TextColor, string AccentColor) BuildEventInstructionPresentation(EventInstruction instruction)
    {
        if (string.Equals(instruction.Name, "Show_Message_A", StringComparison.Ordinal) ||
            string.Equals(instruction.Name, "Show_Message_B", StringComparison.Ordinal))
        {
            return ("Message", "#DBEAFE", "#1D4ED8", "#60A5FA");
        }

        if (instruction.Arguments.Count > 1 && instruction.Arguments.All(argument => argument.Type == EventArgumentType.Jump))
        {
            return ("Selector", "#F3E8FF", "#7C3AED", "#A78BFA");
        }

        if (string.Equals(instruction.Name, "Conditional_Multijump", StringComparison.Ordinal) ||
            instruction.Arguments.Any(argument => string.Equals(argument.Name, "jump", StringComparison.Ordinal)))
        {
            return ("Jump", "#FEF3C7", "#92400E", "#F59E0B");
        }

        if (string.Equals(instruction.Name, "GOTO_EVENT", StringComparison.Ordinal))
        {
            return ("Transfer", "#E0E7FF", "#4338CA", "#818CF8");
        }

        if (string.Equals(instruction.Name, "END", StringComparison.Ordinal))
        {
            return ("End", "#F3F4F6", "#374151", "#9CA3AF");
        }

        if (instruction.Name.Contains("Battle", StringComparison.OrdinalIgnoreCase))
        {
            return ("Battle", "#FCE7F3", "#9D174D", "#EC4899");
        }

        if (instruction.Name.Contains("Actor", StringComparison.OrdinalIgnoreCase) ||
            instruction.Name.Contains("Npc", StringComparison.OrdinalIgnoreCase) ||
            instruction.Name.Contains("Object", StringComparison.OrdinalIgnoreCase))
        {
            return ("Actor", "#DCFCE7", "#166534", "#4ADE80");
        }

        return ("Action", "#E5E7EB", "#374151", "#9CA3AF");
    }

    private bool TryFormatMessageInstruction(EventInstruction instruction, out string line)
    {
        if (!string.Equals(instruction.Name, "Show_Message_A", StringComparison.Ordinal) &&
            !string.Equals(instruction.Name, "Show_Message_B", StringComparison.Ordinal))
        {
            line = string.Empty;
            return false;
        }

        var bank = instruction.Arguments.FirstOrDefault(argument => string.Equals(argument.Name, "bank", StringComparison.Ordinal))?.RawValue ?? 0;
        var id = instruction.Arguments.FirstOrDefault(argument => string.Equals(argument.Name, "id", StringComparison.Ordinal))?.RawValue ?? 0;
        if (bank < 0 || id < 0)
        {
            line = $"{instruction.Name.Replace('_', ' ')} (invalid message reference: bank {bank}, id {id})";
            return true;
        }

        var messageId = new MessageId(bank, id);
        var messageText = loadedMessages.TryGetValue(messageId, out var text) ? text : "<missing message>";
        line = $"Message {id} ({instruction.Name.Replace('_', ' ')}): {SanitizeMessageText(messageText)}";
        return true;
    }

    private string BuildMessageInstructionDetail(EventInstruction instruction)
    {
        var bank = instruction.Arguments.FirstOrDefault(argument => string.Equals(argument.Name, "bank", StringComparison.Ordinal))?.RawValue ?? 0;
        var id = instruction.Arguments.FirstOrDefault(argument => string.Equals(argument.Name, "id", StringComparison.Ordinal))?.RawValue ?? 0;
        if (bank < 0 || id < 0)
        {
            return $"Invalid message reference (bank {bank}, message {id})";
        }

        return $"Bank {bank}, message {id}";
    }

    private string FormatFriendlyInstruction(EventInstruction instruction)
    {
        switch (instruction)
        {
            case StartBattleInstruction startBattle:
                return $"Start Battle (battle: {startBattle.Battle.Value}, mode: 0x{startBattle.BattleModeFlags.Value:X2}, post-battle: 0x{startBattle.PostBattleModeFlags.Value:X2})";
            case InitiateActorInstruction initiateActor:
                return $"Initiate Actor (slot: {initiateActor.PackedActorId.TrackedObjectSlot}, flags: 0x{initiateActor.PackedActorId.Flags:X2}, sprite: {initiateActor.SpriteId}, x: {initiateActor.X}, y: {initiateActor.Y})";
            case MoveActorInstruction moveActor:
                return $"Move Actor ({moveActor.TrackedObjectSlot.Value}: {moveActor.Move.Direction.Name}, {moveActor.Move.Distance})";
            case RotateActorInstruction rotateActor:
                return $"Rotate Actor ({rotateActor.TrackedObjectSlot.Value}: {rotateActor.Direction.Name})";
            case UnloadActorInstruction unloadActor:
                return $"Unload Actor (slot: {unloadActor.PackedActorId.TrackedObjectSlot}, flags: 0x{unloadActor.PackedActorId.Flags:X2})";
            case SetMapSceneVariantInstruction sceneVariant:
                return $"Set Map Scene Variant (variant: {sceneVariant.Variant}, skip reload: {sceneVariant.SkipFullReload})";
        }

        if (string.Equals(instruction.Name, "Wait_For_Button_Press", StringComparison.Ordinal))
        {
            return "Wait for button press";
        }

        if (string.Equals(instruction.Name, "Close_Message_Box", StringComparison.Ordinal))
        {
            return "Close message box";
        }

        var friendlyName = instruction.Name.Replace('_', ' ');
        if (instruction.Arguments.Count == 0)
        {
            return friendlyName;
        }

        var arguments = instruction.Arguments
            .Where(argument => argument.Type != EventArgumentType.Jump)
            .Select(argument => $"{argument.Name}: {FormatFriendlyArgument(argument)}");
        return $"{friendlyName} ({string.Join(", ", arguments)})";
    }

    private string FormatFriendlyArgument(EventArgumentValue argument)
    {
        return argument.Type switch
        {
            EventArgumentType.Bot => $"{metadata.GetBotName(argument.RawValue)} ({argument.RawValue})",
            EventArgumentType.Medal => $"{metadata.GetMedalName(argument.RawValue)} ({argument.RawValue})",
            EventArgumentType.Music => $"{metadata.GetSongName(argument.RawValue)} ({argument.RawValue})",
            EventArgumentType.PackedTrackedObjectId => new PackedTrackedObjectId((byte)argument.RawValue).Flags == 0
                ? $"slot {new PackedTrackedObjectId((byte)argument.RawValue).TrackedObjectSlot}"
                : $"slot {new PackedTrackedObjectId((byte)argument.RawValue).TrackedObjectSlot}, flags 0x{new PackedTrackedObjectId((byte)argument.RawValue).Flags:X2}",
            EventArgumentType.TrackedObjectSlot => $"slot {argument.RawValue}",
            EventArgumentType.BattleModeFlags => $"0x{argument.RawValue:X2}",
            EventArgumentType.PostBattleModeFlags => $"0x{argument.RawValue:X2}",
            _ => argument.DisplayValue
        };
    }

    private static string SanitizeMessageText(string text)
    {
        return text
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("<END:0>", string.Empty, StringComparison.Ordinal)
            .Trim();
    }

    private static bool LooksLikeNumericValue(string text)
    {
        var trimmed = text.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return false;
        }

        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return int.TryParse(trimmed[2..], System.Globalization.NumberStyles.HexNumber, null, out _);
        }

        return int.TryParse(trimmed, out _);
    }

    private static string DescribeJumpBehavior(EventInstruction instruction)
    {
        return instruction.Name switch
        {
            "Yes_or_No_Box" => "No branch",
            "No_or_Yes_Box" => "Yes branch",
            "Jump_If_Not_Has_Money" => "Not enough money",
            "Jump_If_Not_Has_Medal" => "Missing medal",
            "Jump_If_Not_Player_Direction" => "Wrong facing direction",
            _ when instruction.Name.StartsWith("Jump_If_", StringComparison.Ordinal) => $"If {instruction.Name["Jump_If_".Length..].Replace('_', ' ')}",
            _ => "Branch target"
        };
    }

    private static string BuildLabelName(EventInstruction instruction, int sequence)
    {
        return instruction.Name switch
        {
            "Yes_or_No_Box" => $"No_{sequence}",
            "No_or_Yes_Box" => $"Yes_{sequence}",
            "Jump_If_Not_Has_Money" => $"NotEnoughMoney_{sequence}",
            "Jump_If_Not_Has_Medal" => $"MissingMedal_{sequence}",
            "Relative_Long_Jump" => $"JumpTarget_{sequence}",
            _ when instruction.Name.StartsWith("Jump_If_", StringComparison.Ordinal) => $"Condition_{sequence}",
            _ => $"Label_{sequence}"
        };
    }

    private static int ParseConditionalBranchIndex(string argumentName)
    {
        if (!argumentName.StartsWith("jump", StringComparison.OrdinalIgnoreCase))
        {
            return -1;
        }

        return int.TryParse(argumentName["jump".Length..], out var oneBasedIndex) ? oneBasedIndex - 1 : -1;
    }
}
