using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Medabots.Rom.Events;
using Medabots.Rom.Metadata;

namespace Medabots.Rom.Editor;

public sealed class EventArgumentEditorItem : INotifyPropertyChanged
{
    private string _valueText = string.Empty;
    private int _selectedEnumIndex = -1;
    private int _selectedJumpTargetIndex = -1;
    private int _moveDirectionIndex;
    private string _moveDistanceText = "0";
    private bool _isMoveUnused;
    private string _trackedObjectSlotText = "0";
    private string _packedFlagsText = "0x00";

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name { get; init; } = string.Empty;

    public EventArgumentType Type { get; init; }

    public string TypeName { get; init; } = string.Empty;

    public string HelpText { get; set; } = string.Empty;

    public bool IsEnumEditor { get; set; }

    public bool IsMoveEditor { get; set; }

    public bool IsPackedTrackedObjectIdEditor { get; set; }

    public bool IsJumpTargetEditor { get; set; }

    public bool IsPlainValueEditor => !IsEnumEditor && !IsMoveEditor && !IsPackedTrackedObjectIdEditor && !IsJumpTargetEditor;

    public bool ShowStandardHelpText => !IsJumpTargetEditor;

    public ObservableCollection<string> EnumOptions { get; } = [];

    public ObservableCollection<string> JumpTargetOptions { get; } = [];

    public string ValueText
    {
        get => _valueText;
        set
        {
            if (string.Equals(_valueText, value, StringComparison.Ordinal))
            {
                return;
            }

            _valueText = value;
            OnPropertyChanged();
        }
    }

    public int SelectedEnumIndex
    {
        get => _selectedEnumIndex;
        set
        {
            if (_selectedEnumIndex == value)
            {
                return;
            }

            _selectedEnumIndex = value;
            OnPropertyChanged();
        }
    }

    public int SelectedJumpTargetIndex
    {
        get => _selectedJumpTargetIndex;
        set
        {
            if (_selectedJumpTargetIndex == value)
            {
                return;
            }

            _selectedJumpTargetIndex = value;
            OnPropertyChanged();

            if (value >= 0 && value < JumpTargetOptions.Count)
            {
                ValueText = JumpTargetOptions[value];
            }
        }
    }

    public int MoveDirectionIndex
    {
        get => _moveDirectionIndex;
        set
        {
            if (_moveDirectionIndex == value)
            {
                return;
            }

            _moveDirectionIndex = value;
            OnPropertyChanged();
        }
    }

    public string MoveDistanceText
    {
        get => _moveDistanceText;
        set
        {
            if (string.Equals(_moveDistanceText, value, StringComparison.Ordinal))
            {
                return;
            }

            _moveDistanceText = value;
            OnPropertyChanged();
        }
    }

    public bool IsMoveUnused
    {
        get => _isMoveUnused;
        set
        {
            if (_isMoveUnused == value)
            {
                return;
            }

            _isMoveUnused = value;
            OnPropertyChanged();
        }
    }

    public string TrackedObjectSlotText
    {
        get => _trackedObjectSlotText;
        set
        {
            if (string.Equals(_trackedObjectSlotText, value, StringComparison.Ordinal))
            {
                return;
            }

            _trackedObjectSlotText = value;
            OnPropertyChanged();
        }
    }

    public string PackedFlagsText
    {
        get => _packedFlagsText;
        set
        {
            if (string.Equals(_packedFlagsText, value, StringComparison.Ordinal))
            {
                return;
            }

            _packedFlagsText = value;
            OnPropertyChanged();
        }
    }

    public static EventArgumentEditorItem Create(EventArgumentValue argument, IReadOnlyDictionary<int, string>? labelMap = null, int sourceOffset = 0)
    {
        var item = new EventArgumentEditorItem
        {
            Name = argument.Name,
            Type = argument.Type,
            TypeName = argument.Type.ToString(),
            ValueText = argument.RawValue.ToString(),
            HelpText = argument.DisplayValue
        };

        switch (argument.Type)
        {
            case EventArgumentType.Jump:
                item.IsJumpTargetEditor = true;
                if (labelMap is not null)
                {
                    var orderedLabels = labelMap.Where(pair => pair.Key > sourceOffset).OrderBy(pair => pair.Key).ToArray();
                    foreach (var label in orderedLabels)
                    {
                        item.JumpTargetOptions.Add(label.Value);
                    }

                    var targetOffset = sourceOffset + argument.RawValue + 1;
                    var selectedIndex = Array.FindIndex(orderedLabels, pair => pair.Key == targetOffset);
                    item.SelectedJumpTargetIndex = selectedIndex;
                }
                break;

            case EventArgumentType.Direction:
                item.IsEnumEditor = true;
                item.EnumOptions.Add("North");
                item.EnumOptions.Add("South");
                item.EnumOptions.Add("West");
                item.EnumOptions.Add("East");
                item.SelectedEnumIndex = Math.Clamp(argument.RawValue, 0, item.EnumOptions.Count - 1);
                break;

            case EventArgumentType.Part:
                item.IsEnumEditor = true;
                item.EnumOptions.Add("Head");
                item.EnumOptions.Add("Right Arm");
                item.EnumOptions.Add("Left Arm");
                item.EnumOptions.Add("Legs");
                item.SelectedEnumIndex = Math.Clamp(argument.RawValue, 0, item.EnumOptions.Count - 1);
                break;

            case EventArgumentType.Bot:
                item.IsEnumEditor = true;
                PopulateEnumOptions(item.EnumOptions, MedabotsMetadata.Default.Catalog.Bots);
                item.SelectedEnumIndex = Math.Clamp(argument.RawValue, 0, item.EnumOptions.Count - 1);
                break;

            case EventArgumentType.Medal:
                item.IsEnumEditor = true;
                PopulateEnumOptions(item.EnumOptions, MedabotsMetadata.Default.Catalog.Medals);
                item.SelectedEnumIndex = Math.Clamp(argument.RawValue, 0, item.EnumOptions.Count - 1);
                break;

            case EventArgumentType.Move:
                item.IsMoveEditor = true;
                item.EnumOptions.Add("North");
                item.EnumOptions.Add("South");
                item.EnumOptions.Add("West");
                item.EnumOptions.Add("East");
                item.IsMoveUnused = argument.RawValue == MedabotsRomSchema.EventMoveNone;
                item.MoveDirectionIndex = (argument.RawValue & MedabotsRomSchema.EventMoveMask) switch
                {
                    MedabotsRomSchema.EventMoveNorth => 0,
                    MedabotsRomSchema.EventMoveSouth => 1,
                    MedabotsRomSchema.EventMoveWest => 2,
                    MedabotsRomSchema.EventMoveEast => 3,
                    _ => 0
                };
                item.MoveDistanceText = (argument.RawValue & MedabotsRomSchema.EventMoveDistanceMask).ToString();
                break;

            case EventArgumentType.PackedTrackedObjectId:
                item.IsPackedTrackedObjectIdEditor = true;
                var packed = new PackedTrackedObjectId((byte)argument.RawValue);
                item.TrackedObjectSlotText = packed.TrackedObjectSlot.ToString();
                item.PackedFlagsText = $"0x{packed.Flags:X2}";
                break;

            case EventArgumentType.BattleModeFlags:
            case EventArgumentType.PostBattleModeFlags:
                item.ValueText = $"0x{argument.RawValue:X2}";
                break;
        }

        if (string.Equals(argument.Name, "jump", StringComparison.Ordinal) && !item.IsJumpTargetEditor)
        {
            item.IsJumpTargetEditor = true;
            if (labelMap is not null)
            {
                var orderedLabels = labelMap.Where(pair => pair.Key > sourceOffset).OrderBy(pair => pair.Key).ToArray();
                foreach (var label in orderedLabels)
                {
                    item.JumpTargetOptions.Add(label.Value);
                }

                var targetOffset = sourceOffset + argument.RawValue + 1;
                var selectedIndex = Array.FindIndex(orderedLabels, pair => pair.Key == targetOffset);
                item.SelectedJumpTargetIndex = selectedIndex;
            }
        }

        return item;
    }

    public int GetEditedValue()
    {
        if (IsEnumEditor)
        {
            return SelectedEnumIndex < 0 ? 0 : SelectedEnumIndex;
        }

        if (IsMoveEditor)
        {
            if (IsMoveUnused)
            {
                return MedabotsRomSchema.EventMoveNone;
            }

            if (!int.TryParse(MoveDistanceText, out var distance) || distance < 0 || distance > MedabotsRomSchema.EventMoveDistanceMask)
            {
                throw new InvalidOperationException($"{Name} distance must be between 0 and {MedabotsRomSchema.EventMoveDistanceMask}.");
            }

            var directionBits = MoveDirectionIndex switch
            {
                0 => MedabotsRomSchema.EventMoveNorth,
                1 => MedabotsRomSchema.EventMoveSouth,
                2 => MedabotsRomSchema.EventMoveWest,
                3 => MedabotsRomSchema.EventMoveEast,
                _ => MedabotsRomSchema.EventMoveNorth
            };

            return directionBits | distance;
        }

        if (IsPackedTrackedObjectIdEditor)
        {
            if (!int.TryParse(TrackedObjectSlotText, out var slot) || slot < 0 || slot > 0x0F)
            {
                throw new InvalidOperationException($"{Name} slot must be between 0 and 15.");
            }

            var flags = ParseFlexibleInt(PackedFlagsText, $"{Name} flags");
            if (flags < 0 || flags > 0xF0 || (flags & 0x0F) != 0)
            {
                throw new InvalidOperationException($"{Name} flags must be a high-nibble mask like 0x00, 0x40, 0x80, or 0xF0.");
            }

            return flags | slot;
        }

        return ParseFlexibleInt(ValueText, Name);
    }

    private static int ParseFlexibleInt(string text, string fieldName)
    {
        var trimmed = (text ?? string.Empty).Trim();
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(trimmed[2..], System.Globalization.NumberStyles.HexNumber, null, out var hexValue))
            {
                return hexValue;
            }
        }

        if (int.TryParse(trimmed, out var value) && value >= 0)
        {
            return value;
        }

        throw new InvalidOperationException($"{fieldName} must be a non-negative integer or 0x-prefixed hex value.");
    }

    private static void PopulateEnumOptions(ObservableCollection<string> target, IReadOnlyList<string> values)
    {
        for (var index = 0; index < values.Count; index++)
        {
            var name = string.IsNullOrWhiteSpace(values[index]) ? $"Unknown #{index}" : values[index];
            target.Add($"{index:D3}  {name}");
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
