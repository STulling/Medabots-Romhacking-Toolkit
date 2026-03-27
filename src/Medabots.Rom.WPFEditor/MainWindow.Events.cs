using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Forms = System.Windows.Forms;
using WpfColor = System.Windows.Media.Color;
using WpfPoint = System.Windows.Point;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfButton = System.Windows.Controls.Button;
using WpfMenuItem = System.Windows.Controls.MenuItem;
using WpfCursors = System.Windows.Input.Cursors;
using WpfMessageBox = System.Windows.MessageBox;
using Win32OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using Win32SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using Medabots.Rom.Battles;
using Medabots.Rom.Editor;
using Medabots.Rom.Encounters;
using Medabots.Rom.Events;
using Medabots.Rom.Images;
using Medabots.Rom.Metadata;
using Medabots.Rom.Parts;
using Medabots.Rom.Projects;
using Medabots.Rom.Shops;
using Medabots.Rom.Starter;
using Medabots.Rom.Text;
using Medabots.Rom.WPFEditor.Dialogs;
using Medabots.Rom.WPFEditor.Models;
using Microsoft.Win32;

namespace Medabots.Rom.WPFEditor;

public partial class MainWindow : Window
{
    private void ClearEventPresentation()
    {
        _selectedEventInstruction = null;
        _selectedEventId = null;
        _visibleEventInstructions.Clear();
        ClearEventInstructionEditor();
        EventInstructionCollectionView.SelectedItem = null;
    }

    private void ClearEventInstructionEditor()
    {
        _selectedEventInstruction = null;
        _selectedEventOperationDefinition = null;
        _visibleEventArgumentEditors.Clear();
        EventActionTitleLabel.Text = "No action selected.";
        EventActionHintLabel.Text = "Select an editable event action to change its arguments.";
        EventKnownLabelsLabel.Text = string.Empty;
        EventPatchStatusLabel.Text = string.Empty;
        EventOperationPicker.SelectedItem = null;
    }

    private void SetLoadingState(bool isVisible, string status, double progress)
    {
        LoadingStatusLabel.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        LoadingProgressBar.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        LoadingStatusLabel.Text = status;
        LoadingProgressBar.Value = Math.Clamp(progress, 0d, 1d) * 100d;
    }

    private async Task PreloadEventsAsync(MedabotsRomTextProfile profile)
    {
        if (_session is null)
        {
            return;
        }

        var progress = new Progress<(int Completed, int Total, string Status)>(value =>
        {
            var progressValue = value.Total == 0
                ? 1d
                : 0.45d + (0.55d * value.Completed / value.Total);
            SetLoadingState(true, value.Status, progressValue);
        });

        var preloadResult = await Task.Run(() =>
        {
            var scripts = new EventScript[profile.EventCount];
            var visualStates = new EventVisualState[profile.EventCount];
            var summaries = new string[profile.EventCount];
            var completed = 0;

            Parallel.For(0, profile.EventCount, eventIndex =>
            {
                var eventId = (short)eventIndex;
                var script = ReadEventScriptForEditor(eventId, profile);
                scripts[eventIndex] = script;
                visualStates[eventIndex] = BuildEventVisualState((short)eventIndex, script);
                summaries[eventIndex] = BuildEventSummary(script);

                var finished = Interlocked.Increment(ref completed);
                if (finished % 8 == 0 || finished == profile.EventCount)
                {
                    ((IProgress<(int Completed, int Total, string Status)>)progress)
                        .Report((finished, profile.EventCount, $"Preloading events... {finished}/{profile.EventCount}"));
                }
            });

            return (scripts, visualStates, summaries);
        });

        _eventCache.Clear();
        for (short eventId = 0; eventId < profile.EventCount; eventId++)
        {
            _eventCache[eventId] = preloadResult.scripts[eventId];
        }

        _eventViewCache.Clear();
        for (short eventId = 0; eventId < profile.EventCount; eventId++)
        {
            _eventViewCache[eventId] = preloadResult.visualStates[eventId];
        }

        for (var index = 0; index < _allEventItems.Count; index++)
        {
            var item = _allEventItems[index];
            item.Summary = preloadResult.summaries[index];
            item.IsCached = true;
        }
    }

    private void PopulateEventInstructionEditor(EventInstructionItem? instructionItem)
    {
        _visibleEventArgumentEditors.Clear();
        if (instructionItem is null)
        {
            ClearEventInstructionEditor();
            return;
        }

        EventActionTitleLabel.Text = instructionItem.HasLabelDisplay
            ? $"{instructionItem.Name}  •  {instructionItem.LabelDisplay}"
            : instructionItem.Name;
        if (!instructionItem.IsEditable)
        {
            EventOperationPicker.SelectedItem = null;
            EventActionHintLabel.Text = "This row is descriptive only and cannot be edited directly.";
            return;
        }

        _selectedEventOperationDefinition = ResolveEditorOperationDefinition(instructionItem.Instruction);
        EventOperationPicker.SelectedItem = _eventOperationOptions.FirstOrDefault(option => option.Definition.Opcode == _selectedEventOperationDefinition?.Opcode);
        RebuildEventArgumentEditors();
    }

    private Dictionary<string, int> BuildEventArgumentUpdateMap()
    {
        var values = new Dictionary<string, int>(StringComparer.Ordinal);
        var labelMap = _selectedEventVisualState?.LabelMap;
        foreach (var argument in _visibleEventArgumentEditors)
        {
            if (_selectedEventInstruction is not null && IsJumpArgument(argument))
            {
                values[argument.Name] = ResolveJumpArgumentValue(argument, _selectedEventInstruction.Offset, labelMap);
                continue;
            }

            values[argument.Name] = argument.GetEditedValue();
        }

        return values;
    }

    private void RebuildEventArgumentEditors()
    {
        var currentValues = _visibleEventArgumentEditors.ToDictionary(argument => argument.Name, argument => argument.ValueText, StringComparer.Ordinal);
        _visibleEventArgumentEditors.Clear();
        if (_selectedEventInstruction?.Instruction is null || _selectedEventOperationDefinition is null)
        {
            return;
        }

        var sourceOffset = _selectedEventInstruction.Offset;
        var sourceArguments = _selectedEventInstruction.Instruction.Arguments.ToDictionary(argument => argument.Name, argument => argument, StringComparer.Ordinal);

        EventActionHintLabel.Text = string.Equals(_selectedEventInstruction.Instruction.Name, "Conditional_Multijump", StringComparison.Ordinal)
            ? "This instruction selects a branch from the current event variable/state value. Edit the forward branch targets below."
            : _selectedEventOperationDefinition.Arguments.Any(IsJumpDefinition)
                ? "Edit the argument values below and apply them back to the loaded ROM. Jump branches can be targeted by label."
                : "Edit the argument values below and apply them back to the loaded ROM.";

        foreach (var definition in _selectedEventOperationDefinition.Arguments)
        {
            var rawValue = sourceArguments.TryGetValue(definition.Name, out var sourceArgument) ? sourceArgument.RawValue : 0;
            var displayValue = sourceArguments.TryGetValue(definition.Name, out sourceArgument) ? sourceArgument.DisplayValue : rawValue.ToString();
            var editorItem = EventArgumentEditorItem.Create(
                new EventArgumentValue(definition.Name, definition.Type, rawValue, displayValue),
                _selectedEventVisualState?.LabelMap,
                sourceOffset);

            if (currentValues.TryGetValue(definition.Name, out var currentValue))
            {
                editorItem.ValueText = currentValue;
            }

            if (IsJumpDefinition(definition))
            {
                editorItem.HelpText = BuildJumpArgumentHelpText(_selectedEventInstruction, new EventArgumentValue(definition.Name, definition.Type, rawValue, displayValue));
            }

            _visibleEventArgumentEditors.Add(editorItem);
        }
    }

    private void RebuildMessageItems()
    {
        _allPatchItems.Clear();

        foreach (var message in _loadedMessages.OrderBy(pair => pair.Key.Bank).ThenBy(pair => pair.Key.Index))
        {
            _allPatchItems.Add(new MessagePatchItem
            {
                Bank = message.Key.Bank,
                Index = message.Key.Index,
                Text = message.Value,
                OriginalText = _originalMessages.TryGetValue(message.Key, out var originalText) ? originalText : message.Value
            });
        }

        foreach (var patch in _project.MessagePatches)
        {
            var existing = _allPatchItems.FirstOrDefault(item => item.Bank == patch.Id.Bank && item.Index == patch.Id.Index);
            if (existing is not null)
            {
                existing.Text = patch.Text;
                continue;
            }

            _allPatchItems.Add(new MessagePatchItem
            {
                Bank = patch.Id.Bank,
                Index = patch.Id.Index,
                Text = patch.Text,
                OriginalText = _originalMessages.TryGetValue(patch.Id, out var originalText) ? originalText : string.Empty
            });
        }

        _allPatchItems.Sort(static (left, right) =>
        {
            var bankComparison = left.Bank.CompareTo(right.Bank);
            return bankComparison != 0 ? bankComparison : left.Index.CompareTo(right.Index);
        });

        RefreshMessageFilter();
        PatchCollectionView.SelectedItem = null;
        _selectedPatch = null;
        ClearPatchEditor();
        ClearEventPresentation();
    }

    private void SetActiveSection(string section)
    {
        MessagesSection.IsSelected = string.Equals(section, "Messages", StringComparison.Ordinal);
        EventsSection.IsSelected = string.Equals(section, "Events", StringComparison.Ordinal);
        BattlesSection.IsSelected = string.Equals(section, "Battles", StringComparison.Ordinal);
        PartsSection.IsSelected = string.Equals(section, "Parts", StringComparison.Ordinal);
        SpritesSection.IsSelected = string.Equals(section, "Sprites", StringComparison.Ordinal);
        EncountersSection.IsSelected = string.Equals(section, "Encounters", StringComparison.Ordinal);
        ShopsSection.IsSelected = string.Equals(section, "Shops", StringComparison.Ordinal);
        StarterSection.IsSelected = string.Equals(section, "Starter", StringComparison.Ordinal);
    }

    private async void OnEventSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_session is null)
        {
            return;
        }

        if (EventCollectionView.SelectedItem is not EventBrowserItem item)
        {
            return;
        }

        var profile = RequireProfile();
        if (profile is null)
        {
            return;
        }

        try
        {
            var eventId = (short)item.Id;
            _selectedEventId = eventId;
            if (!_eventCache.TryGetValue(eventId, out var script))
            {
                script = ReadEventScriptForEditor(eventId, profile);
                _eventCache[eventId] = script;
                item.IsCached = true;
                item.Summary = BuildEventSummary(script);
            }

            if (!_eventViewCache.TryGetValue(eventId, out var visualState))
            {
                visualState = BuildEventVisualState(eventId, script);
                _eventViewCache[eventId] = visualState;
            }

            ApplyEventVisualState(visualState);
            ClearEventInstructionEditor();
            UpdateSelectedEventPatchStatus(eventId);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Event Load Failed", ex.Message, "OK");
        }
    }

    private async void OnBattleSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (BattleCollectionView.SelectedItem is not BrowserItem item || item.Id >= _loadedBattles.Count)
        {
            return;
        }

        _loadedBattle = _loadedBattles[item.Id];
        PopulateBattleEditor(_loadedBattle);
        BattleEditor.Text = FormatBattle(_loadedBattle, _metadata);
        await Task.CompletedTask;
    }

    private async void OnPartSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (PartCollectionView.SelectedItem is not BrowserItem item || item.Id >= _loadedParts.Count)
        {
            return;
        }

        _loadedPart = _loadedParts[item.Id];
        PopulatePartEditor(_loadedPart);
        PartEditor.Text = FormatPart(_loadedPart, _metadata);
        PartActionAnalysisEditor.Text = FormatPartActionAnalysis(_loadedPart);
        await Task.CompletedTask;
    }

    private async void OnEncounterSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (EncounterCollectionView.SelectedItem is not BrowserItem item || item.Id >= _loadedEncounters.Count)
        {
            return;
        }

        _loadedEncounter = _loadedEncounters[item.Id];
        PopulateEncounterEditor(_loadedEncounter);
        EncounterEditor.Text = FormatEncounter(_loadedEncounter);
        await Task.CompletedTask;
    }

    private void OnEventInstructionSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _selectedEventInstruction = EventInstructionCollectionView.SelectedItem as EventInstructionItem;
        PopulateEventInstructionEditor(_selectedEventInstruction);
    }

    private async void OnRenameEventLabelMenuClicked(object? sender, RoutedEventArgs e)
    {
        if (!TryGetContextInstructionItem(sender, out var instructionItem))
        {
            return;
        }

        SelectContextInstruction(instructionItem);
        await PromptForEventLabelAsync(instructionItem.Offset);
    }

    private async void OnEditSelectedEventLabelClicked(object? sender, RoutedEventArgs e)
    {
        if (_selectedEventInstruction is null)
        {
            await DisplayAlertAsync("No Action Selected", "Select an event action first.", "OK");
            return;
        }

        await PromptForEventLabelAsync(_selectedEventInstruction.Offset);
    }

    private async void OnRevertEventPatchClicked(object? sender, RoutedEventArgs e)
    {
        if (_selectedEventId is not short eventId)
        {
            await DisplayAlertAsync("No Event Selected", "Select an event first.", "OK");
            return;
        }

        await RevertEventPatchAsync(eventId);
    }

    private async void OnRevertEventPatchMenuClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not WpfMenuItem menuItem || menuItem.CommandParameter is not EventBrowserItem item)
        {
            return;
        }

        EventCollectionView.SelectedItem = item;
        _selectedEventId = (short)item.Id;
        await RevertEventPatchAsync((short)item.Id);
    }

    private async void OnInsertEventNopBeforeMenuClicked(object? sender, RoutedEventArgs e)
    {
        if (!TryGetContextInstructionItem(sender, out var instructionItem))
        {
            return;
        }

        SelectContextInstruction(instructionItem);
        await ApplyStructuralEventEditAsync(instructionItem, "Insert Nop Before", script => _eventScriptRewriter.InsertNopBefore(_session!.RomFile, script, _selectedEventVisualState!.LabelMap, instructionItem.Offset));
    }

    private async void OnInsertSelectedOperationBeforeMenuClicked(object? sender, RoutedEventArgs e)
    {
        if (!TryGetContextInstructionItem(sender, out var instructionItem))
        {
            return;
        }

        SelectContextInstruction(instructionItem);
        if (_selectedEventOperationDefinition is null)
        {
            await DisplayAlertAsync("No Operation Selected", "Choose an event operation in the action editor first.", "OK");
            return;
        }

        await ApplyStructuralEventEditAsync(
            instructionItem,
            $"Insert {_selectedEventOperationDefinition.Name} Before",
            script => _eventScriptRewriter.InsertInstructionBefore(_session!.RomFile, script, _selectedEventVisualState!.LabelMap, instructionItem.Offset, _selectedEventOperationDefinition));
    }

    private async void OnInsertEventNopAfterMenuClicked(object? sender, RoutedEventArgs e)
    {
        if (!TryGetContextInstructionItem(sender, out var instructionItem))
        {
            return;
        }

        SelectContextInstruction(instructionItem);
        await ApplyStructuralEventEditAsync(instructionItem, "Insert Nop After", script => _eventScriptRewriter.InsertNopAfter(_session!.RomFile, script, _selectedEventVisualState!.LabelMap, instructionItem.Offset));
    }

    private async void OnInsertSelectedOperationAfterMenuClicked(object? sender, RoutedEventArgs e)
    {
        if (!TryGetContextInstructionItem(sender, out var instructionItem))
        {
            return;
        }

        SelectContextInstruction(instructionItem);
        if (_selectedEventOperationDefinition is null)
        {
            await DisplayAlertAsync("No Operation Selected", "Choose an event operation in the action editor first.", "OK");
            return;
        }

        await ApplyStructuralEventEditAsync(
            instructionItem,
            $"Insert {_selectedEventOperationDefinition.Name} After",
            script => _eventScriptRewriter.InsertInstructionAfter(_session!.RomFile, script, _selectedEventVisualState!.LabelMap, instructionItem.Offset, _selectedEventOperationDefinition));
    }

    private async void OnDeleteEventInstructionMenuClicked(object? sender, RoutedEventArgs e)
    {
        if (!TryGetContextInstructionItem(sender, out var instructionItem))
        {
            return;
        }

        SelectContextInstruction(instructionItem);
        await ApplyStructuralEventEditAsync(instructionItem, "Delete Instruction", script => _eventScriptRewriter.DeleteInstruction(_session!.RomFile, script, _selectedEventVisualState!.LabelMap, instructionItem.Offset));
    }

    private void OnEventOperationSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (EventOperationPicker.SelectedItem is EventOperationOption option)
        {
            _selectedEventOperationDefinition = option.Definition;
            RebuildEventArgumentEditors();
        }
    }

    private async Task ApplyStructuralEventEditAsync(EventInstructionItem instructionItem, string operationName, Func<EventScript, byte[]> rewriteOperation)
    {
        if (_session is null || _selectedEventId is null)
        {
            return;
        }

        var profile = RequireProfile();
        if (profile is null)
        {
            return;
        }

        try
        {
            var script = _eventCache.TryGetValue(_selectedEventId.Value, out var cachedScript)
                ? cachedScript
                : ReadEventScriptForEditor(_selectedEventId.Value, profile);
            var rewrittenBytes = rewriteOperation(script);
            StoreEventScriptPatch(_selectedEventId.Value, rewrittenBytes);
            RefreshEditedEventView(_selectedEventId.Value, profile);
            UpdateStatus();
            await DisplayAlertAsync("Event Updated", $"{operationName} completed and stored in the project.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Event Edit Failed", ex.Message, "OK");
        }
    }

    private async Task PromptForEventLabelAsync(int offset)
    {
        var existingLabel = ResolveEditableLabelText(offset);
        var response = await DisplayPromptAsync(
            "Event Label",
            "Add, rename, or clear the label for this instruction.",
            accept: "Save",
            cancel: "Cancel",
            placeholder: "Label name",
            initialValue: existingLabel);

        if (response is null)
        {
            return;
        }

        await ApplyEventLabelChangeAsync(offset, response);
    }

    private async Task ApplyEventLabelChangeAsync(int offset, string? labelTextRaw)
    {
        if (_selectedEventId is not short eventId || _selectedEventInstruction is null)
        {
            await DisplayAlertAsync("No Action Selected", "Select an event action to add or update a label.", "OK");
            return;
        }

        var labelText = (labelTextRaw ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(labelText) && !IsValidEventLabel(labelText))
        {
            await DisplayAlertAsync("Invalid Label", "Labels must start with a letter or underscore and use only letters, digits, or underscores.", "OK");
            return;
        }

        if (!_eventCustomLabels.TryGetValue(eventId, out var customLabels))
        {
            customLabels = [];
            _eventCustomLabels[eventId] = customLabels;
        }

        if (!string.IsNullOrWhiteSpace(labelText) &&
            customLabels.Any(pair => pair.Key != _selectedEventInstruction.Offset && string.Equals(pair.Value, labelText, StringComparison.OrdinalIgnoreCase)))
        {
            await DisplayAlertAsync("Duplicate Label", $"The label '{labelText}' is already used in this event.", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(labelText))
        {
            customLabels.Remove(offset);
        }
        else
        {
            customLabels[offset] = labelText;
        }

        _eventViewCache.Remove(eventId);

        var profile = RequireProfile();
        if (profile is null || !_eventCache.TryGetValue(eventId, out var script))
        {
            return;
        }

        var refreshedVisualState = BuildEventVisualState(eventId, script);
        _eventViewCache[eventId] = refreshedVisualState;
        ApplyEventVisualState(refreshedVisualState);
        UpdateSelectedEventPatchStatus(eventId);
        ReselectEventInstruction(offset);
    }

    private async void OnApplyEventActionClicked(object? sender, RoutedEventArgs e)
    {
        if (_session is null || _selectedEventInstruction is null || !_selectedEventInstruction.IsEditable || _selectedEventId is null)
        {
            await DisplayAlertAsync("No Editable Action", "Select an editable event action first.", "OK");
            return;
        }

        try
        {
            var updatedArguments = BuildEventArgumentUpdateMap();
            var profile = RequireProfile();
            if (profile is null)
            {
                return;
            }

            var instruction = _selectedEventInstruction.Instruction;
            if (instruction is null)
            {
                throw new InvalidOperationException("The selected row does not have an editable event instruction.");
            }

            var currentScript = _eventCache.TryGetValue(_selectedEventId.Value, out var cachedScript)
                ? cachedScript
                : ReadEventScriptForEditor(_selectedEventId.Value, profile);
            var targetDefinition = _selectedEventOperationDefinition ?? instruction.Definition
                ?? throw new InvalidOperationException("No target event operation is selected.");
            var previewSession = CreatePreviewSession();
            _eventInstructionPatcher.Apply(previewSession, profile, currentScript, instruction, targetDefinition, updatedArguments);
            StoreEventScriptPatch(_selectedEventId.Value, ResolvePatchedEventBytes(previewSession.RomFile, profile, _selectedEventId.Value));
            RefreshEditedEventView(_selectedEventId.Value, profile);
            UpdateEventBrowserPatchStatus(_selectedEventId.Value);
            ReselectEventInstruction(instruction.Offset);
            UpdateStatus();
            await DisplayAlertAsync("Action Updated", "Stored the selected event action changes in the project.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Event Update Failed", ex.Message, "OK");
        }
    }

    private void OnLoadShopListClicked(object? sender, RoutedEventArgs e) => LoadShopList();

    private EventPresentationBuilder CreateEventPresentationBuilder() => new(_metadata, _loadedMessages, _eventCustomLabels);

    private EventVisualState BuildEventVisualState(short eventId, EventScript script) => CreateEventPresentationBuilder().BuildVisualState(eventId, script);

    private void ApplyEventVisualState(EventVisualState visualState)
    {
        _selectedEventVisualState = visualState;
        RefreshCollection(_visibleEventInstructions, visualState.Instructions);
        EventKnownLabelsLabel.Text = visualState.OrderedLabels.Count == 0
            ? "Labels: none"
            : $"Labels: {string.Join(", ", visualState.OrderedLabels.Take(8))}{(visualState.OrderedLabels.Count > 8 ? ", ..." : string.Empty)}";
    }

    private string ResolveEditableLabelText(int offset)
    {
        if (_selectedEventId is not short eventId)
        {
            return string.Empty;
        }

        if (_eventCustomLabels.TryGetValue(eventId, out var customLabels) && customLabels.TryGetValue(offset, out var customLabel))
        {
            return customLabel;
        }

        if (_selectedEventVisualState?.LabelMap.TryGetValue(offset, out var label) == true)
        {
            return label;
        }

        return string.Empty;
    }

    private string BuildJumpArgumentHelpText(EventInstructionItem instructionItem, EventArgumentValue argument) =>
        EventPresentationBuilder.BuildJumpArgumentHelpText(instructionItem, argument, _selectedEventVisualState?.LabelMap);

    private static EventOperationDefinition? ResolveEditorOperationDefinition(EventInstruction? instruction) =>
        EventPresentationBuilder.ResolveEditorOperationDefinition(instruction);

    private static int ResolveJumpArgumentValue(EventArgumentEditorItem argument, int sourceOffset, IReadOnlyDictionary<int, string>? labelMap) =>
        EventPresentationBuilder.ResolveJumpArgumentValue(argument, sourceOffset, labelMap);

    private void LoadEventLabelsFromProject()
    {
        _eventCustomLabels.Clear();

        foreach (var label in _project.EventLabels)
        {
            if (!_eventCustomLabels.TryGetValue(label.EventId, out var eventLabels))
            {
                eventLabels = [];
                _eventCustomLabels[label.EventId] = eventLabels;
            }

            eventLabels[label.Offset] = label.Label;
        }
    }

    private void LoadEventScriptPatchesFromProject()
    {
        _eventProjectScriptPatches.Clear();

        foreach (var patch in _project.EventScriptPatches)
        {
            _eventProjectScriptPatches[patch.EventId] = patch.ScriptBytes.ToArray();
        }

        UpdateEventBrowserPatchStatuses();
    }

    private EventScript ReadEventScriptForEditor(short eventId, MedabotsRomTextProfile profile)
    {
        if (_session is null)
        {
            throw new InvalidOperationException("No ROM session is open.");
        }

        if (!_eventProjectScriptPatches.TryGetValue(eventId, out var patchBytes))
        {
            return _eventScriptReader.ReadById(_session.RomFile, profile, eventId);
        }

        var previewSession = CreatePreviewSession();
        _eventInstructionPatcher.RewriteEvent(previewSession, profile, eventId, patchBytes, $"Preview event patch {eventId}");
        return _eventScriptReader.ReadById(previewSession.RomFile, profile, eventId);
    }

    private void StoreEventScriptPatch(short eventId, byte[] scriptBytes)
    {
        _eventProjectScriptPatches[eventId] = scriptBytes.ToArray();
        UpdateEventBrowserPatchStatus(eventId);
    }

    private void RefreshEditedEventView(short eventId, MedabotsRomTextProfile profile)
    {
        var refreshedScript = ReadEventScriptForEditor(eventId, profile);
        _eventCache[eventId] = refreshedScript;
        var refreshedVisualState = BuildEventVisualState(eventId, refreshedScript);
        _eventViewCache[eventId] = refreshedVisualState;
        ApplyEventVisualState(refreshedVisualState);
        UpdateSelectedEventPatchStatus(eventId);
    }

    private void UpdateEventBrowserPatchStatuses()
    {
        foreach (var item in _allEventItems)
        {
            item.IsPatched = _eventProjectScriptPatches.ContainsKey((short)item.Id);
        }
    }

    private void UpdateEventBrowserPatchStatus(short eventId)
    {
        var item = _allEventItems.FirstOrDefault(entry => entry.Id == eventId);
        if (item is not null)
        {
            item.IsPatched = _eventProjectScriptPatches.ContainsKey(eventId);
        }
    }

    private void UpdateSelectedEventPatchStatus(short eventId)
    {
        EventPatchStatusLabel.Text = _eventProjectScriptPatches.ContainsKey(eventId)
            ? "Patch status: this event is overridden in the project and will be relocated on ROM export."
            : "Patch status: using the original ROM event script.";
    }

    private async Task RevertEventPatchAsync(short eventId)
    {
        if (!_eventProjectScriptPatches.Remove(eventId))
        {
            await DisplayAlertAsync("No Event Patch", $"Event {eventId:D4} does not currently have a stored project patch.", "OK");
            return;
        }

        _eventCache.Remove(eventId);
        _eventViewCache.Remove(eventId);
        UpdateEventBrowserPatchStatus(eventId);

        var profile = RequireProfile();
        if (profile is not null)
        {
            var originalScript = ReadEventScriptForEditor(eventId, profile);
            _eventCache[eventId] = originalScript;
            var visualState = BuildEventVisualState(eventId, originalScript);
            _eventViewCache[eventId] = visualState;

            var item = _allEventItems.FirstOrDefault(entry => entry.Id == eventId);
            if (item is not null)
            {
                item.Summary = BuildEventSummary(originalScript);
                item.IsCached = true;
            }

            if (_selectedEventId == eventId)
            {
                ApplyEventVisualState(visualState);
                ClearEventInstructionEditor();
                UpdateSelectedEventPatchStatus(eventId);
            }
        }

        UpdateStatus();
    }

    private RomHackSession CreatePreviewSession()
    {
        if (_session is null)
        {
            throw new InvalidOperationException("No ROM session is open.");
        }

        var clonedRomFile = new RomFile(_session.RomFile.FilePath, _session.RomFile.Data.ToArray());
        return RomHackSession.FromRomFile(clonedRomFile);
    }

    private byte[] ResolvePatchedEventBytes(RomFile patchedRomFile, MedabotsRomTextProfile profile, short eventId)
    {
        var patchedScript = _eventScriptReader.ReadById(patchedRomFile, profile, eventId);
        var serializer = new EventScriptSerializer(_eventOperationRegistry);
        return serializer.Serialize(patchedRomFile, patchedScript);
    }

    private void ReselectEventInstruction(int offset)
    {
        var refreshedSelection = _visibleEventInstructions.FirstOrDefault(item => item.Offset == offset && item.Instruction is not null);
        if (refreshedSelection is null)
        {
            ClearEventInstructionEditor();
            return;
        }

        EventInstructionCollectionView.SelectedItem = refreshedSelection;
        _selectedEventInstruction = refreshedSelection;
        PopulateEventInstructionEditor(refreshedSelection);
    }

    private static bool IsValidEventLabel(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return false;
        }

        if (!(char.IsLetter(label[0]) || label[0] == '_'))
        {
            return false;
        }

        return label.All(character => char.IsLetterOrDigit(character) || character == '_');
    }

    private static bool LooksLikeNumericValue(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return int.TryParse(trimmed, out _);
    }

    private static bool IsJumpArgument(EventArgumentValue argument)
    {
        return argument.Type == EventArgumentType.Jump || string.Equals(argument.Name, "jump", StringComparison.Ordinal);
    }

    private static bool IsJumpArgument(EventArgumentEditorItem argument)
    {
        return argument.Type == EventArgumentType.Jump || string.Equals(argument.Name, "jump", StringComparison.Ordinal);
    }

    private static bool IsJumpDefinition(EventArgumentDefinition definition)
    {
        return definition.Type == EventArgumentType.Jump || string.Equals(definition.Name, "jump", StringComparison.Ordinal);
    }

    private bool TryGetContextInstructionItem(object? sender, out EventInstructionItem instructionItem)
    {
        if (sender is WpfMenuItem menuItem && menuItem.CommandParameter is EventInstructionItem item && item.Instruction is not null)
        {
            instructionItem = item;
            return true;
        }

        instructionItem = null!;
        return false;
    }

    private void SelectContextInstruction(EventInstructionItem instructionItem)
    {
        _selectedEventInstruction = instructionItem;
        EventInstructionCollectionView.SelectedItem = instructionItem;
        PopulateEventInstructionEditor(instructionItem);
    }

}
