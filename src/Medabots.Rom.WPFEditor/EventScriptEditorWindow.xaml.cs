using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Medabots.Rom.Editor;
using Medabots.Rom.Events;
using Medabots.Rom.WPFEditor.Dialogs;

namespace Medabots.Rom.WPFEditor;

public partial class EventScriptEditorWindow : Window
{
    private readonly short _eventId;
    private readonly IReadOnlyList<EventOperationOption> _operationOptions;
    private readonly Func<EventVisualState> _refreshState;
    private readonly Func<string> _getPatchStatus;
    private readonly Action<int, string?> _applyLabelChange;
    private readonly Action<EventInstruction, EventOperationDefinition, Dictionary<string, int>> _applyActionChange;
    private readonly Action<int, EventOperationDefinition> _insertSelectedOperationBefore;
    private readonly Action<int, EventOperationDefinition> _insertSelectedOperationAfter;
    private readonly Action<int> _insertNopBefore;
    private readonly Action<int> _insertNopAfter;
    private readonly Action<int> _moveInstructionUp;
    private readonly Action<int> _moveInstructionDown;
    private readonly Action<int> _deleteInstruction;
    private readonly ObservableCollection<EventInstructionItem> _visibleInstructions = [];
    private readonly ObservableCollection<EventArgumentEditorItem> _visibleArguments = [];
    private EventVisualState _currentVisualState;
    private EventInstructionItem? _selectedInstruction;
    private EventOperationDefinition? _selectedOperationDefinition;
    private bool _isRefreshingUi;

    public EventScriptEditorWindow(
        short eventId,
        EventVisualState initialVisualState,
        IReadOnlyList<EventOperationOption> operationOptions,
        Func<EventVisualState> refreshState,
        Func<string> getPatchStatus,
        Action<int, string?> applyLabelChange,
        Action<EventInstruction, EventOperationDefinition, Dictionary<string, int>> applyActionChange,
        Action<int, EventOperationDefinition> insertSelectedOperationBefore,
        Action<int, EventOperationDefinition> insertSelectedOperationAfter,
        Action<int> insertNopBefore,
        Action<int> insertNopAfter,
        Action<int> moveInstructionUp,
        Action<int> moveInstructionDown,
        Action<int> deleteInstruction)
    {
        _eventId = eventId;
        _currentVisualState = initialVisualState;
        _operationOptions = operationOptions;
        _refreshState = refreshState;
        _getPatchStatus = getPatchStatus;
        _applyLabelChange = applyLabelChange;
        _applyActionChange = applyActionChange;
        _insertSelectedOperationBefore = insertSelectedOperationBefore;
        _insertSelectedOperationAfter = insertSelectedOperationAfter;
        _insertNopBefore = insertNopBefore;
        _insertNopAfter = insertNopAfter;
        _moveInstructionUp = moveInstructionUp;
        _moveInstructionDown = moveInstructionDown;
        _deleteInstruction = deleteInstruction;

        InitializeComponent();
        EventInstructionCollectionView.ItemsSource = _visibleInstructions;
        EventArgumentCollectionView.ItemsSource = _visibleArguments;
        EventOperationPicker.ItemsSource = _operationOptions;
        EventHeaderLabel.Text = $"Event {_eventId:D4}";
        ApplyVisualState(_currentVisualState);
        EventPatchStatusLabel.Text = _getPatchStatus();
    }

    private void ApplyVisualState(EventVisualState visualState)
    {
        _currentVisualState = visualState;
        RefreshCollection(_visibleInstructions, visualState.Instructions);
        _selectedInstruction = null;
        _selectedOperationDefinition = null;
        EventInstructionCollectionView.SelectedItem = null;
        _visibleArguments.Clear();
        EventActionTitleLabel.Text = "No action selected.";
        EventActionHintLabel.Text = "Select an editable event action to change its arguments.";
        EventOperationPicker.SelectedItem = null;
        EventPatchStatusLabel.Text = _getPatchStatus();
    }

    private void OnEventInstructionSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isRefreshingUi)
        {
            return;
        }

        _selectedInstruction = EventInstructionCollectionView.SelectedItem as EventInstructionItem;
        PopulateEventInstructionEditor(_selectedInstruction);
    }

    private void PopulateEventInstructionEditor(EventInstructionItem? instructionItem)
    {
        _visibleArguments.Clear();
        if (instructionItem is null)
        {
            EventActionTitleLabel.Text = "No action selected.";
            EventActionHintLabel.Text = "Select an editable event action to change its arguments.";
            _selectedOperationDefinition = null;
            EventOperationPicker.SelectedItem = null;
            return;
        }

        EventActionTitleLabel.Text = instructionItem.HasLabelDisplay
            ? $"{instructionItem.Name}  •  {instructionItem.LabelDisplay}"
            : instructionItem.Name;

        if (!instructionItem.IsEditable || instructionItem.Instruction is null)
        {
            EventActionHintLabel.Text = "This row is descriptive only and cannot be edited directly.";
            _selectedOperationDefinition = null;
            EventOperationPicker.SelectedItem = null;
            return;
        }

        _selectedOperationDefinition = EventPresentationBuilder.ResolveEditorOperationDefinition(instructionItem.Instruction);
        _isRefreshingUi = true;
        EventOperationPicker.SelectedItem = _operationOptions.FirstOrDefault(option => option.Definition.Opcode == _selectedOperationDefinition?.Opcode);
        _isRefreshingUi = false;
        RebuildArgumentEditors();
    }

    private void OnEventOperationSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isRefreshingUi)
        {
            return;
        }

        if (EventOperationPicker.SelectedItem is EventOperationOption option)
        {
            _selectedOperationDefinition = option.Definition;
            RebuildArgumentEditors();
        }
    }

    private void RebuildArgumentEditors()
    {
        var currentValues = _visibleArguments.ToDictionary(argument => argument.Name, argument => argument.ValueText, StringComparer.Ordinal);
        _visibleArguments.Clear();
        if (_selectedInstruction?.Instruction is null || _selectedOperationDefinition is null)
        {
            return;
        }

        var sourceOffset = _selectedInstruction.Offset;
        var labelMap = _currentVisualState.LabelMap;
        var sourceArguments = _selectedInstruction.Instruction.Arguments.ToDictionary(argument => argument.Name, argument => argument, StringComparer.Ordinal);

        EventActionHintLabel.Text = string.Equals(_selectedInstruction.Instruction.Name, "Conditional_Multijump", StringComparison.Ordinal)
            ? "This instruction selects a branch from the current event variable/state value. Edit the forward branch targets below."
            : _selectedOperationDefinition.Arguments.Any(IsJumpDefinition)
                ? "Edit the argument values below. Jump branches can be targeted by label."
                : "Edit the argument values below.";

        foreach (var definition in _selectedOperationDefinition.Arguments)
        {
            var rawValue = sourceArguments.TryGetValue(definition.Name, out var sourceArgument) ? sourceArgument.RawValue : 0;
            var displayValue = sourceArguments.TryGetValue(definition.Name, out sourceArgument) ? sourceArgument.DisplayValue : rawValue.ToString();
            var editorItem = EventArgumentEditorItem.Create(
                new EventArgumentValue(definition.Name, definition.Type, rawValue, displayValue),
                labelMap,
                sourceOffset);

            if (currentValues.TryGetValue(definition.Name, out var currentValue))
            {
                editorItem.ValueText = currentValue;
            }

            if (IsJumpDefinition(definition) && _selectedInstruction.Instruction is not null)
            {
                editorItem.HelpText = EventPresentationBuilder.BuildJumpArgumentHelpText(_selectedInstruction, new EventArgumentValue(definition.Name, definition.Type, rawValue, displayValue), labelMap);
            }

            _visibleArguments.Add(editorItem);
        }
    }

    private async void OnEditEventLabelClicked(object sender, RoutedEventArgs e)
    {
        if (_selectedInstruction is null)
        {
            System.Windows.MessageBox.Show(this, "Select an event action first.", "No Action Selected", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var initialValue = _currentVisualState.LabelMap.TryGetValue(_selectedInstruction.Offset, out var label) ? label : string.Empty;
        var dialog = new InputDialog(
            "Event Label",
            "Add, rename, or clear the label for this instruction.",
            "Save",
            "Cancel",
            "Label name",
            initialValue)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            _applyLabelChange(_selectedInstruction.Offset, dialog.ResponseText);
            RefreshAfterUpdate(_selectedInstruction.Offset);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, ex.Message, "Label Update Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnApplyEventActionClicked(object sender, RoutedEventArgs e)
    {
        if (_selectedInstruction?.Instruction is null || !_selectedInstruction.IsEditable || _selectedOperationDefinition is null)
        {
            System.Windows.MessageBox.Show(this, "Select an editable event action first.", "No Editable Action", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var updatedArguments = BuildArgumentUpdateMap();
            _applyActionChange(_selectedInstruction.Instruction, _selectedOperationDefinition, updatedArguments);
            RefreshAfterUpdate(_selectedInstruction.Offset);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, ex.Message, "Event Update Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private Dictionary<string, int> BuildArgumentUpdateMap()
    {
        var values = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var argument in _visibleArguments)
        {
            if (IsJumpArgument(argument))
            {
                values[argument.Name] = EventPresentationBuilder.ResolveJumpArgumentValue(argument, _selectedInstruction!.Offset, _currentVisualState.LabelMap);
                continue;
            }

            values[argument.Name] = argument.GetEditedValue();
        }

        return values;
    }

    private void RefreshAfterUpdate(int preferredOffset)
    {
        var visualState = _refreshState();
        ApplyVisualState(visualState);
        var refreshed = _visibleInstructions.FirstOrDefault(item => item.Offset == preferredOffset);
        if (refreshed is not null)
        {
            EventInstructionCollectionView.SelectedItem = refreshed;
            EventInstructionCollectionView.ScrollIntoView(refreshed);
        }
    }

    private void OnInsertSelectedOperationBeforeClicked(object sender, RoutedEventArgs e)
    {
        if (!TryGetStructuralSelection(out var instructionItem))
        {
            return;
        }

        if (_selectedOperationDefinition is null)
        {
            System.Windows.MessageBox.Show(this, "Choose an event operation first.", "No Operation Selected", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        TryApplyStructuralChange(
            () => _insertSelectedOperationBefore(instructionItem.Offset, _selectedOperationDefinition),
            instructionItem.Offset,
            "Insert operation before");
    }

    private void OnInsertSelectedOperationAfterClicked(object sender, RoutedEventArgs e)
    {
        if (!TryGetStructuralSelection(out var instructionItem))
        {
            return;
        }

        if (_selectedOperationDefinition is null)
        {
            System.Windows.MessageBox.Show(this, "Choose an event operation first.", "No Operation Selected", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        TryApplyStructuralChange(
            () => _insertSelectedOperationAfter(instructionItem.Offset, _selectedOperationDefinition),
            instructionItem.Offset,
            "Insert operation after");
    }

    private void OnInsertNopBeforeClicked(object sender, RoutedEventArgs e)
    {
        if (!TryGetStructuralSelection(out var instructionItem))
        {
            return;
        }

        TryApplyStructuralChange(() => _insertNopBefore(instructionItem.Offset), instructionItem.Offset, "Insert nop before");
    }

    private void OnInsertNopAfterClicked(object sender, RoutedEventArgs e)
    {
        if (!TryGetStructuralSelection(out var instructionItem))
        {
            return;
        }

        TryApplyStructuralChange(() => _insertNopAfter(instructionItem.Offset), instructionItem.Offset, "Insert nop after");
    }

    private void OnMoveInstructionUpClicked(object sender, RoutedEventArgs e)
    {
        if (!TryGetStructuralSelection(out var instructionItem))
        {
            return;
        }

        TryApplyStructuralChange(() => _moveInstructionUp(instructionItem.Offset), instructionItem.Offset, "Move instruction up");
    }

    private void OnMoveInstructionDownClicked(object sender, RoutedEventArgs e)
    {
        if (!TryGetStructuralSelection(out var instructionItem))
        {
            return;
        }

        TryApplyStructuralChange(() => _moveInstructionDown(instructionItem.Offset), instructionItem.Offset, "Move instruction down");
    }

    private void OnDeleteInstructionClicked(object sender, RoutedEventArgs e)
    {
        if (!TryGetStructuralSelection(out var instructionItem))
        {
            return;
        }

        TryApplyStructuralChange(() => _deleteInstruction(instructionItem.Offset), instructionItem.Offset, "Delete instruction");
    }

    private bool TryGetStructuralSelection(out EventInstructionItem instructionItem)
    {
        instructionItem = _selectedInstruction!;
        if (_selectedInstruction is not null)
        {
            return true;
        }

        System.Windows.MessageBox.Show(this, "Select an event row first.", "No Action Selected", MessageBoxButton.OK, MessageBoxImage.Information);
        return false;
    }

    private void TryApplyStructuralChange(Action updateAction, int preferredOffset, string operationName)
    {
        try
        {
            updateAction();
            RefreshAfterUpdate(preferredOffset);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, ex.Message, $"{operationName} Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static bool IsJumpDefinition(EventArgumentDefinition definition)
    {
        return definition.Type == EventArgumentType.Jump || string.Equals(definition.Name, "jump", StringComparison.Ordinal);
    }

    private static bool IsJumpArgument(EventArgumentEditorItem argument)
    {
        return argument.Type == EventArgumentType.Jump || string.Equals(argument.Name, "jump", StringComparison.Ordinal);
    }

    private static void RefreshCollection<T>(ObservableCollection<T> collection, IEnumerable<T> items)
    {
        collection.Clear();
        foreach (var item in items)
        {
            collection.Add(item);
        }
    }
}
