
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
    private readonly ObservableCollection<MessagePatchItem> _visiblePatchItems = [];
    private readonly ObservableCollection<EventBrowserItem> _visibleEventItems = [];
    private readonly ObservableCollection<EventInstructionItem> _visibleEventInstructions = [];
    private readonly ObservableCollection<EventArgumentEditorItem> _visibleEventArgumentEditors = [];
    private readonly ObservableCollection<BrowserItem> _visibleBattleItems = [];
    private readonly ObservableCollection<BrowserItem> _visiblePartItems = [];
    private readonly ObservableCollection<SpriteBrowserNode> _visibleSpriteNodes = [];
    private readonly ObservableCollection<BrowserItem> _visibleEncounterItems = [];
    private readonly ObservableCollection<BrowserItem> _visibleShopItems = [];

    private readonly List<MessagePatchItem> _allPatchItems = [];
    private readonly List<EventBrowserItem> _allEventItems = [];
    private readonly List<BrowserItem> _allBattleItems = [];
    private readonly List<BrowserItem> _allPartItems = [];
    private readonly List<SpriteBrowserNode> _allSpriteNodes = [];
    private readonly List<BrowserItem> _allEncounterItems = [];
    private readonly List<BrowserItem> _allShopItems = [];

    private readonly RomHackProjectApplicator _projectApplicator = new();
    private readonly MedabotsMessageTableReader _messageTableReader = new();
    private readonly EventScriptReader _eventScriptReader = new();
    private readonly EventInstructionPatcher _eventInstructionPatcher = new();
    private readonly EventScriptRewriter _eventScriptRewriter = new();
    private readonly BattleTableReader _battleTableReader = new();
    private readonly BattleActionOpcodeTableReader _battleActionOpcodeTableReader = new();
    private readonly BattleActionScriptTableReader _battleActionScriptTableReader = new();
    private readonly BattlePatcher _battlePatcher = new();
    private readonly BattleActionRegistry _battleActionRegistry = BattleActionRegistry.LoadDefault();
    private readonly PartTableReader _partTableReader = new();
    private readonly PartPatcher _partPatcher = new();
    private readonly ImageAssetRepository _imageAssetRepository = new();
    private readonly ImageAssetPatcher _imageAssetPatcher = new();
    private readonly EncounterTableReader _encounterTableReader = new();
    private readonly EncounterPatcher _encounterPatcher = new();
    private readonly ShopTableReader _shopTableReader = new();
    private readonly ShopPatcher _shopPatcher = new();
    private readonly StarterReader _starterReader = new();
    private readonly StarterPatcher _starterPatcher = new();
    private readonly EventOperationRegistry _eventOperationRegistry = EventOperationRegistry.LoadDefault();
    private readonly MedabotsMetadata _metadata = MedabotsMetadata.Default;
    private readonly Dictionary<MessageId, string> _originalMessages = [];
    private readonly Dictionary<short, EventScript> _eventCache = [];
    private readonly Dictionary<short, EventVisualState> _eventViewCache = [];
    private readonly Dictionary<short, Dictionary<int, string>> _eventCustomLabels = [];
    private readonly Dictionary<short, byte[]> _eventProjectScriptPatches = [];
    private readonly Dictionary<string, SpritePreviewState> _spritePreviewCache = [];
    private readonly Dictionary<int, SpriteAsset> _editedOverworldSpriteAssets = [];
    private readonly Dictionary<(int CharacterId, int PortraitIndex), PortraitAsset> _editedPortraitAssets = [];
    private readonly Dictionary<string, SpriteEditHistory> _spriteEditHistories = [];
    private readonly Dictionary<(int EntryLength, int ShopId), ShopDefinition> _shopCache = [];
    private readonly List<EventOperationOption> _eventOperationOptions = [];

    private RomHackSession? _session;
    private RomHackProject _project = new();
    private MessagePatchItem? _selectedPatch;
    private EventInstructionItem? _selectedEventInstruction;
    private EventOperationDefinition? _selectedEventOperationDefinition;
    private SpriteBrowserNode? _selectedSpriteNode;
    private short? _selectedEventId;
    private EventVisualState? _selectedEventVisualState;
    private SpriteEditorTool _selectedSpriteEditorTool = SpriteEditorTool.Pencil;
    private int _selectedPaletteIndex = 1;
    private bool _isPaintingSprite;
    private bool _hasCapturedUndoForCurrentStroke;
    private int _spriteEditorZoom = 4;
    private IReadOnlyDictionary<MessageId, string> _loadedMessages = new Dictionary<MessageId, string>();
    private IReadOnlyList<BattleDefinition> _loadedBattles = [];
    private IReadOnlyList<BattleActionOpcodeEntry> _loadedBattleActionOpcodes = [];
    private IReadOnlyList<BattleActionScriptEntry> _loadedBattleActionScripts = [];
    private IReadOnlyList<PartDefinition> _loadedParts = [];
    private IReadOnlyList<EncounterDefinition> _loadedEncounters = [];
    private BattleDefinition? _loadedBattle;
    private PartDefinition? _loadedPart;
    private EncounterDefinition? _loadedEncounter;
    private ShopDefinition? _loadedShop;
    private StarterDefinition? _loadedStarter;

    public MainWindow()
    {
        InitializeComponent();
        PatchCollectionView.ItemsSource = _visiblePatchItems;
        EventCollectionView.ItemsSource = _visibleEventItems;
        EventInstructionCollectionView.ItemsSource = _visibleEventInstructions;
        EventArgumentCollectionView.ItemsSource = _visibleEventArgumentEditors;
        BattleCollectionView.ItemsSource = _visibleBattleItems;
        PartCollectionView.ItemsSource = _visiblePartItems;
        SpriteTreeView.ItemsSource = _visibleSpriteNodes;
        EncounterCollectionView.ItemsSource = _visibleEncounterItems;
        ShopCollectionView.ItemsSource = _visibleShopItems;
        _eventOperationOptions.AddRange(_eventOperationRegistry.Definitions
            .OrderBy(definition => definition.Opcode)
            .Select(definition => new EventOperationOption
            {
                Definition = definition,
                DisplayName = $"0x{definition.Opcode:X2}  {definition.Name}"
            }));
        EventOperationPicker.ItemsSource = _eventOperationOptions;
        ResetPartEditorLabels(PartKind.Head);
        ClearPartEditor();
        SetActiveSection("Messages");
        UpdateStatus();
    }

    private void OnExitClicked(object? sender, RoutedEventArgs e) => Close();

    private void OnMessagesTabClicked(object? sender, EventArgs e) => SetActiveSection("Messages");
    private void OnEventsTabClicked(object? sender, EventArgs e) => SetActiveSection("Events");
    private void OnBattlesTabClicked(object? sender, EventArgs e) => SetActiveSection("Battles");
    private void OnPartsTabClicked(object? sender, EventArgs e) => SetActiveSection("Parts");
    private void OnSpritesTabClicked(object? sender, EventArgs e) => SetActiveSection("Sprites");
    private void OnEncountersTabClicked(object? sender, EventArgs e) => SetActiveSection("Encounters");
    private void OnShopsTabClicked(object? sender, EventArgs e) => SetActiveSection("Shops");
    private void OnStarterTabClicked(object? sender, EventArgs e) => SetActiveSection("Starter");

    private async void OnLoadProjectClicked(object? sender, RoutedEventArgs e)
    {
        var projectPath = PickOpenFilePath("Select a Medabots project file", "Medabots project (*.json)|*.json|All files (*.*)|*.*");
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return;
        }

        try
        {
            var loadedProject = await RomHackProjectSerializer.LoadAsync(projectPath);
            var romPath = await ResolveProjectRomPathAsync(loadedProject.SourceRomPath);
            if (string.IsNullOrWhiteSpace(romPath))
            {
                return;
            }

            SetLoadingState(true, "Opening ROM...", 0.02);
            var session = await RomHackSession.OpenAsync(romPath);
            loadedProject.ProjectFilePath = projectPath;
            loadedProject.SourceRomPath = romPath;

            _session = session;
            _project = loadedProject;
            PrepareProjectForEditing();
            SetLoadingState(true, "Detecting profile...", 0.08);
            TryDetectTextProfile();
            await LoadBrowsableDataAsync();
            SetLoadingState(false, string.Empty, 0);
            UpdateStatus();
        }
        catch (Exception ex)
        {
            SetLoadingState(false, string.Empty, 0);
            await DisplayAlertAsync("Load Failed", ex.Message, "OK");
        }
    }

    private async void OnSaveProjectClicked(object? sender, RoutedEventArgs e)
    {
        if (!TrySyncPatchEditorIntoSelection(out var errorMessage))
        {
            await DisplayAlertAsync("Project Invalid", errorMessage, "OK");
            return;
        }

        var projectPath = await EnsureProjectSavePathAsync();
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return;
        }

        try
        {
            PopulateProjectFromEditor(projectPath);
            await RomHackProjectSerializer.SaveAsync(_project, projectPath);
            ProjectPathEntry.Text = projectPath;
            UpdateStatus();
            await DisplayAlertAsync("Project Saved", $"Saved project to:{Environment.NewLine}{projectPath}", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Save Failed", ex.Message, "OK");
        }
    }

    private async void OnOpenRomClicked(object? sender, RoutedEventArgs e)
    {
        var romPath = PickOpenFilePath("Select a Medabots GBA ROM", "Game Boy Advance ROM (*.gba)|*.gba|All files (*.*)|*.*");
        if (string.IsNullOrWhiteSpace(romPath))
        {
            return;
        }

        try
        {
            SetLoadingState(true, "Opening ROM...", 0.02);
            var session = await RomHackSession.OpenAsync(romPath);
            _session = session;
            _project = CreateInMemoryProject(romPath);
            PrepareProjectForEditing();
            SetLoadingState(true, "Detecting profile...", 0.08);
            TryDetectTextProfile();
            await LoadBrowsableDataAsync();
            SetLoadingState(false, string.Empty, 0);
            UpdateStatus();
        }
        catch (Exception ex)
        {
            SetLoadingState(false, string.Empty, 0);
            await DisplayAlertAsync("Open Failed", ex.Message, "OK");
        }
    }
    private async Task LoadBrowsableDataAsync()
    {
        if (_session is null)
        {
            return;
        }

        var profile = RequireProfile();
        if (profile is null)
        {
            return;
        }

        _originalMessages.Clear();
        _eventCache.Clear();
        _eventViewCache.Clear();
        _shopCache.Clear();

        SetLoadingState(true, "Preloading text...", 0.12);
        _loadedMessages = await Task.Run(() => _messageTableReader.ReadAll(_session.RomFile, profile.TextPointerTableOffset));
        foreach (var pair in _loadedMessages)
        {
            _originalMessages[pair.Key] = pair.Value;
        }

        SetLoadingState(true, "Loading tables...", 0.35);
        _loadedBattles = _battleTableReader.ReadAll(_session.RomFile, profile);
        _loadedBattleActionOpcodes = _battleActionOpcodeTableReader.ReadAll(_session.RomFile);
        _loadedBattleActionScripts = _battleActionScriptTableReader.ReadAll(_session.RomFile);
        _loadedParts = _partTableReader.ReadAll(_session.RomFile);
        _loadedEncounters = _encounterTableReader.ReadAll(_session.RomFile);
        _loadedPart = null;
        _spritePreviewCache.Clear();
        _editedOverworldSpriteAssets.Clear();
        _editedPortraitAssets.Clear();
        _spriteEditHistories.Clear();
        _selectedSpriteNode = null;

        RebuildMessageItems();
        _allBattleItems.Clear();
        _allBattleItems.AddRange(_loadedBattles.Select(battle => new BrowserItem(battle.Id, $"{battle.Id:D3}  {_metadata.GetCharacterName(battle.CharacterId)}")));
        _allPartItems.Clear();
        _allPartItems.AddRange(_loadedParts.Select(part => new BrowserItem(part.Id, $"{part.Id:D3}  {_metadata.GetPartName(part.Id)}  ({part.Kind})")));
        _allSpriteNodes.Clear();
        _allSpriteNodes.AddRange(BuildSpriteTreeNodes());
        _allEncounterItems.Clear();
        _allEncounterItems.AddRange(_loadedEncounters.Select(encounter => new BrowserItem(encounter.Id, $"{encounter.Id:D3}  Battles {encounter.Battle1}/{encounter.Battle2}/{encounter.Battle3}/{encounter.Battle4}")));
        _allEventItems.Clear();
        _allEventItems.AddRange(Enumerable.Range(0, profile.EventCount).Select(id => new EventBrowserItem { Id = id }));
        UpdateEventBrowserPatchStatuses();

        RefreshMessageFilter();
        RefreshBattleFilter();
        RefreshPartFilter();
        RefreshSpriteFilter();
        RefreshEncounterFilter();
        RefreshEventFilter();
        LoadShopList();
        PartCollectionView.SelectedItem = null;
        ClearPartEditor();
        ClearSpritePreview();

        _loadedStarter = _starterReader.Read(_session.RomFile, profile);
        PopulateStarterEditor(_loadedStarter);
        StarterEditor.Text = FormatStarter(_loadedStarter, _metadata);

        await PreloadEventsAsync(profile);
    }

    private async void OnSaveCopyClicked(object? sender, RoutedEventArgs e)
    {
        if (_session is null)
        {
            await DisplayAlertAsync("No Session", "Open a ROM before saving a hacked copy.", "OK");
            return;
        }

        var sourcePath = _session.RomFile.FilePath;
        var destinationPath = Path.Combine(Path.GetDirectoryName(sourcePath) ?? Environment.CurrentDirectory, $"{Path.GetFileNameWithoutExtension(sourcePath)}.hack.gba");

        try
        {
            if (!TrySyncPatchEditorIntoSelection(out var errorMessage))
            {
                await DisplayAlertAsync("Project Invalid", errorMessage, "OK");
                return;
            }

            PopulateProjectFromEditor(_project.ProjectFilePath);
            var exportSession = await RomHackSession.OpenAsync(sourcePath);
            _projectApplicator.Apply(_project, exportSession);
            await exportSession.SaveAsAsync(destinationPath);
            await DisplayAlertAsync("ROM Exported", $"Exported patched ROM to:{Environment.NewLine}{destinationPath}", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Export Failed", ex.Message, "OK");
        }
    }

    private void OnMessageFilterChanged(object? sender, TextChangedEventArgs e) => RefreshMessageFilter();
    private void OnEventFilterChanged(object? sender, TextChangedEventArgs e) => RefreshEventFilter();
    private void OnBattleFilterChanged(object? sender, TextChangedEventArgs e) => RefreshBattleFilter();
    private void OnPartFilterChanged(object? sender, TextChangedEventArgs e) => RefreshPartFilter();
    private void OnSpriteFilterChanged(object? sender, TextChangedEventArgs e) => RefreshSpriteFilter();
    private void OnEncounterFilterChanged(object? sender, TextChangedEventArgs e) => RefreshEncounterFilter();
    private void OnShopFilterChanged(object? sender, TextChangedEventArgs e) => RefreshShopFilter();

    private void RefreshMessageFilter() => RefreshCollection(_visiblePatchItems, _allPatchItems.Where(item => MatchesFilter($"{item.DisplayName} {item.Preview} {item.OriginalText}", MessageFilterEntry.Text)));
    private void RefreshEventFilter() => RefreshCollection(_visibleEventItems, _allEventItems.Where(item => MatchesFilter(item.FilterText, EventFilterEntry.Text)));
    private void RefreshBattleFilter() => RefreshCollection(_visibleBattleItems, _allBattleItems.Where(item => MatchesFilter(item.FilterText, BattleFilterEntry.Text)));
    private void RefreshPartFilter() => RefreshCollection(_visiblePartItems, _allPartItems.Where(item => MatchesFilter(item.FilterText, PartFilterEntry.Text)));
    private void RefreshSpriteFilter()
    {
        _visibleSpriteNodes.Clear();
        foreach (var node in FilterSpriteNodes(_allSpriteNodes, SpriteFilterEntry.Text))
        {
            _visibleSpriteNodes.Add(node);
        }
    }
    private void RefreshEncounterFilter() => RefreshCollection(_visibleEncounterItems, _allEncounterItems.Where(item => MatchesFilter(item.FilterText, EncounterFilterEntry.Text)));
    private void RefreshShopFilter() => RefreshCollection(_visibleShopItems, _allShopItems.Where(item => MatchesFilter(item.FilterText, ShopFilterEntry.Text)));

    private static void RefreshCollection<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }

    private static bool MatchesFilter(string text, string? filter)
    {
        return string.IsNullOrWhiteSpace(filter) || text.Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase);
    }

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
        if (sender is not MenuItem menuItem || menuItem.CommandParameter is not EventBrowserItem item)
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

    private void LoadShopList()
    {
        _allShopItems.Clear();
        var count = Math.Max(0, ParseIntOrDefault(ShopCountEntry.Text, 64));
        for (var id = 0; id < count; id++)
        {
            _allShopItems.Add(new BrowserItem(id, $"Shop {id:D3}"));
        }

        RefreshShopFilter();
    }

    private async void OnShopSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_session is null)
        {
            return;
        }

        if (ShopCollectionView.SelectedItem is not BrowserItem item)
        {
            return;
        }

        try
        {
            var entryLength = ParseInt(ShopEntryLengthEntry.Text, "Shop entry length");
            var cacheKey = (entryLength, item.Id);
            if (!_shopCache.TryGetValue(cacheKey, out var shop))
            {
                shop = _shopTableReader.Read(_session.RomFile, item.Id, entryLength);
                _shopCache[cacheKey] = shop;
            }

            _loadedShop = shop;
            ShopContentsEditor.Text = FormatBytes(_loadedShop.Contents);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Shop Load Failed", ex.Message, "OK");
        }
    }

    private async void OnApplyBattleClicked(object? sender, RoutedEventArgs e)
    {
        if (_session is null || _loadedBattle is null)
        {
            await DisplayAlertAsync("No Battle Loaded", "Select a battle first.", "OK");
            return;
        }

        try
        {
            var updated = BuildBattleFromEditor(_loadedBattle);
            _battlePatcher.Apply(_session, updated);
            _loadedBattles = _loadedBattles.Select(battle => battle.Id == updated.Id ? updated : battle).ToArray();
            _loadedBattle = updated;
            BattleEditor.Text = FormatBattle(updated, _metadata);
            UpdateStatus();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Battle Update Failed", ex.Message, "OK");
        }
    }

    private async void OnApplyPartClicked(object? sender, RoutedEventArgs e)
    {
        if (_session is null || _loadedPart is null)
        {
            await DisplayAlertAsync("No Part Loaded", "Select a part first.", "OK");
            return;
        }

        try
        {
            var updated = BuildPartFromEditor(_loadedPart);
            _partPatcher.Apply(_session, updated);
            _loadedParts = _loadedParts.Select(part => part.Id == updated.Id ? updated : part).ToArray();
            _loadedPart = updated;
            PartEditor.Text = FormatPart(updated, _metadata);
            PartActionAnalysisEditor.Text = FormatPartActionAnalysis(updated);
            UpdateStatus();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Part Update Failed", ex.Message, "OK");
        }
    }

    private async void OnApplyEncounterClicked(object? sender, RoutedEventArgs e)
    {
        if (_session is null || _loadedEncounter is null)
        {
            await DisplayAlertAsync("No Encounter Loaded", "Select an encounter first.", "OK");
            return;
        }

        try
        {
            var updated = new EncounterDefinition(_loadedEncounter.Id, _loadedEncounter.DataOffset, ParseByte(EncounterBattle1Entry.Text, "Battle 1"), ParseByte(EncounterBattle2Entry.Text, "Battle 2"), ParseByte(EncounterBattle3Entry.Text, "Battle 3"), ParseByte(EncounterBattle4Entry.Text, "Battle 4"));
            _encounterPatcher.Apply(_session, updated);
            _loadedEncounters = _loadedEncounters.Select(encounter => encounter.Id == updated.Id ? updated : encounter).ToArray();
            _loadedEncounter = updated;
            EncounterEditor.Text = FormatEncounter(updated);
            UpdateStatus();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Encounter Update Failed", ex.Message, "OK");
        }
    }

    private async void OnApplyShopClicked(object? sender, RoutedEventArgs e)
    {
        if (_session is null || _loadedShop is null)
        {
            await DisplayAlertAsync("No Shop Loaded", "Select a shop first.", "OK");
            return;
        }

        try
        {
            var updated = new ShopDefinition(_loadedShop.Id, _loadedShop.DataOffset, ParseBytes(ShopContentsEditor.Text));
            _shopPatcher.Apply(_session, updated);
            _loadedShop = updated;
            var entryLength = ParseInt(ShopEntryLengthEntry.Text, "Shop entry length");
            _shopCache[(entryLength, updated.Id)] = updated;
            ShopContentsEditor.Text = FormatBytes(updated.Contents);
            UpdateStatus();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Shop Update Failed", ex.Message, "OK");
        }
    }

    private async void OnLoadStarterClicked(object? sender, RoutedEventArgs e)
    {
        if (_session is null)
        {
            await DisplayAlertAsync("No Session", "Open a ROM first.", "OK");
            return;
        }

        var profile = RequireProfile();
        if (profile is null)
        {
            return;
        }

        try
        {
            _loadedStarter = _starterReader.Read(_session.RomFile, profile);
            PopulateStarterEditor(_loadedStarter);
            StarterEditor.Text = FormatStarter(_loadedStarter, _metadata);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Starter Load Failed", ex.Message, "OK");
        }
    }

    private async void OnApplyStarterClicked(object? sender, RoutedEventArgs e)
    {
        if (_session is null || _loadedStarter is null)
        {
            await DisplayAlertAsync("No Starter Loaded", "Load starter data first.", "OK");
            return;
        }

        try
        {
            var updated = new StarterDefinition(_loadedStarter.PartsOffset, _loadedStarter.MedalOffset, ParseByte(StarterPartEntry.Text, "Starter part"), ParseByte(StarterMedalEntry.Text, "Starter medal"), StarterIsFemaleSwitch.IsChecked == true);
            _starterPatcher.Apply(_session, updated);
            _loadedStarter = updated;
            StarterEditor.Text = FormatStarter(updated, _metadata);
            UpdateStatus();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Starter Update Failed", ex.Message, "OK");
        }
    }
    private void OnAddPatchClicked(object? sender, RoutedEventArgs e)
    {
        var patch = new MessagePatchItem { Bank = 0, Index = _allPatchItems.Count, Text = "<END:0>", OriginalText = string.Empty };
        _allPatchItems.Add(patch);
        RefreshMessageFilter();
        PatchCollectionView.SelectedItem = patch;
        UpdateStatus();
    }

    private async void OnRemovePatchClicked(object? sender, RoutedEventArgs e)
    {
        if (_selectedPatch is null)
        {
            await DisplayAlertAsync("No Patch Selected", "Select a patch before removing it.", "OK");
            return;
        }

        _allPatchItems.Remove(_selectedPatch);
        RefreshMessageFilter();
        _selectedPatch = null;
        PatchCollectionView.SelectedItem = null;
        ClearPatchEditor();
        UpdateStatus();
    }

    private async void OnSavePatchChangesClicked(object? sender, RoutedEventArgs e)
    {
        if (!TrySyncPatchEditorIntoSelection(out var errorMessage))
        {
            await DisplayAlertAsync("Patch Invalid", errorMessage, "OK");
            return;
        }

        RefreshMessageFilter();
        UpdateStatus();
        await DisplayAlertAsync("Patch Updated", "Saved the selected message patch changes in the editor.", "OK");
    }

    private void OnPatchSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _selectedPatch = PatchCollectionView.SelectedItem as MessagePatchItem;
        PopulatePatchEditor(_selectedPatch);
    }

    private void UpdateStatus()
    {
        var stagedMessagePatchCount = _originalMessages.Count == 0
            ? _allPatchItems.Count
            : _allPatchItems.Count(item => item.IsModified);
        var projectDisplayName = string.IsNullOrWhiteSpace(_project.Name) ? "Unnamed project" : _project.Name;
        var romFileName = _session is null ? "No ROM loaded" : Path.GetFileName(_session.RomFile.FilePath);

        ProjectNameLabel.Text = $"Project: {projectDisplayName}";
        ProfileStatusLabel.Text = $"Text profile: {ResolveProfileName()}";
        SessionStatusLabel.Text = _session is null ? "No ROM loaded." : $"Loaded: {_session.RomFile.FilePath}";
        RomSizeLabel.Text = _session is null ? "ROM size: n/a" : $"ROM size: {_session.RomFile.Length:N0} bytes";
        PatchCountLabel.Text = $"Message patches: {_allPatchItems.Count} | Event patches: {_eventProjectScriptPatches.Count} | Applied actions: {_session?.AppliedActions.Count ?? 0}";
        FooterProjectLabel.Text = $"Project: {projectDisplayName}";
        FooterChangesLabel.Text = $"Staged changes: messages {stagedMessagePatchCount}, events {_eventProjectScriptPatches.Count}, applied actions {_session?.AppliedActions.Count ?? 0}";
        FooterPathLabel.Text = romFileName;
        OpenRomCommandButton.IsEnabled = _session is null;
        OpenProjectCommandButton.IsEnabled = true;
        SaveProjectCommandButton.IsEnabled = true;
        ExportRomCommandButton.IsEnabled = _session is not null;
    }

    private bool TrySyncPatchEditorIntoSelection(out string errorMessage)
    {
        errorMessage = string.Empty;
        if (_selectedPatch is null)
        {
            return true;
        }

        if (!int.TryParse(BankEntry.Text, out var bank) || bank < 0)
        {
            errorMessage = "Bank must be a non-negative integer.";
            return false;
        }

        if (!int.TryParse(IndexEntry.Text, out var index) || index < 0)
        {
            errorMessage = "Index must be a non-negative integer.";
            return false;
        }

        var text = MessageTextEditor.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            errorMessage = "Message text cannot be empty.";
            return false;
        }

        _selectedPatch.Bank = bank;
        _selectedPatch.Index = index;
        _selectedPatch.Text = text;
        return true;
    }

    private void PopulateProjectFromEditor(string? projectFilePath)
    {
        _project.ProjectFilePath = projectFilePath;
        _project.SourceRomPath = _session?.RomFile.FilePath ?? RomPathEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(_project.TextProfileId) && _session is not null)
        {
            _project.TextProfileId = MedabotsRomTextProfiles.Detect(_session.RomFile)?.Id;
        }

        _project.MessagePatches.Clear();
        var itemsToPersist = _originalMessages.Count == 0
            ? _allPatchItems
            : _allPatchItems.Where(static item => item.IsModified);

        foreach (var item in itemsToPersist)
        {
            _project.MessagePatches.Add(item.ToPatch());
        }

        _project.EventLabels.Clear();
        foreach (var eventEntry in _eventCustomLabels.OrderBy(pair => pair.Key))
        {
            foreach (var label in eventEntry.Value.OrderBy(pair => pair.Key))
            {
                _project.EventLabels.Add(new EventLabelPatch(eventEntry.Key, label.Key, label.Value));
            }
        }

        _project.EventScriptPatches.Clear();
        foreach (var patch in _eventProjectScriptPatches.OrderBy(pair => pair.Key))
        {
            _project.EventScriptPatches.Add(new EventScriptPatch(patch.Key, patch.Value));
        }
    }

    private void TryDetectTextProfile()
    {
        if (_session is not null)
        {
            _project.TextProfileId = MedabotsRomTextProfiles.Detect(_session.RomFile)?.Id ?? _project.TextProfileId;
        }
    }

    private RomHackProject CreateInMemoryProject(string? romPath)
    {
        var trimmedRomPath = string.IsNullOrWhiteSpace(romPath) ? null : romPath.Trim();
        var defaultName = string.IsNullOrWhiteSpace(trimmedRomPath)
            ? "New Medabots Hack"
            : $"{Path.GetFileNameWithoutExtension(trimmedRomPath)} Project";

        return new RomHackProject
        {
            Name = defaultName,
            SourceRomPath = trimmedRomPath
        };
    }

    private void PrepareProjectForEditing()
    {
        ProjectPathEntry.Text = _project.ProjectFilePath;
        RomPathEntry.Text = _project.SourceRomPath;
        LoadEventLabelsFromProject();
        LoadEventScriptPatchesFromProject();
        _eventViewCache.Clear();
        _eventCache.Clear();
        _selectedPatch = null;
        PatchCollectionView.SelectedItem = null;
        ClearPatchEditor();
        ClearPartEditor();
    }

    private async Task<string?> ResolveProjectRomPathAsync(string? configuredRomPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredRomPath) && File.Exists(configuredRomPath))
        {
            return configuredRomPath.Trim();
        }

        await DisplayAlertAsync(
            "ROM Required",
            "The project file does not point to a ROM that can be opened. Select the source ROM so the project can be loaded.",
            "OK");

        return PickOpenFilePath("Select the source Medabots GBA ROM", "Game Boy Advance ROM (*.gba)|*.gba|All files (*.*)|*.*");
    }

    private async Task<string?> EnsureProjectSavePathAsync()
    {
        if (!string.IsNullOrWhiteSpace(_project.ProjectFilePath))
        {
            return _project.ProjectFilePath.Trim();
        }

        var projectPath = PickSaveFilePath("Save Medabots Project", "Medabots project (*.medahack.json)|*.medahack.json|JSON files (*.json)|*.json|All files (*.*)|*.*", GetSuggestedProjectFilePath());

        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return null;
        }

        projectPath = projectPath.Trim().Trim('"');
        if (!Path.HasExtension(projectPath))
        {
            projectPath += ".medahack.json";
        }

        return projectPath;
    }

    private string GetSuggestedProjectFilePath()
    {
        if (!string.IsNullOrWhiteSpace(_project.ProjectFilePath))
        {
            return _project.ProjectFilePath.Trim();
        }

        var romPath = _session?.RomFile.FilePath ?? _project.SourceRomPath;
        if (!string.IsNullOrWhiteSpace(romPath))
        {
            var directory = Path.GetDirectoryName(romPath) ?? Environment.CurrentDirectory;
            return Path.Combine(directory, $"{Path.GetFileNameWithoutExtension(romPath)}.medahack.json");
        }

        return Path.Combine(Environment.CurrentDirectory, "medabots.medahack.json");
    }

    private MedabotsRomTextProfile? RequireProfile()
    {
        var profile = MedabotsRomTextProfiles.FindById(_project.TextProfileId);
        if (profile is null)
        {
            _ = DisplayAlertAsync("Unknown Profile", "Load a supported ROM first so the toolkit can detect the correct ROM layout.", "OK");
            return null;
        }

        return profile;
    }

    private string ResolveProfileName()
    {
        var profile = MedabotsRomTextProfiles.FindById(_project.TextProfileId);
        return profile?.Name ?? "not selected";
    }

    private void PopulatePatchEditor(MessagePatchItem? patch)
    {
        if (patch is null)
        {
            ClearPatchEditor();
            return;
        }

        BankEntry.Text = patch.Bank.ToString();
        IndexEntry.Text = patch.Index.ToString();
        MessageTextEditor.Text = patch.Text;
    }

    private void ClearPatchEditor()
    {
        BankEntry.Text = string.Empty;
        IndexEntry.Text = string.Empty;
        MessageTextEditor.Text = string.Empty;
    }

    private void ClearPartEditor()
    {
        PartMedalCompatibilityEntry.Text = string.Empty;
        PartSpecialityEntry.Text = string.Empty;
        PartGenderEntry.Text = string.Empty;
        PartArmorEntry.Text = string.Empty;
        PartValue1Entry.Text = string.Empty;
        PartValue2Entry.Text = string.Empty;
        PartValue3Entry.Text = string.Empty;
        PartValue4Entry.Text = string.Empty;
        PartValue5Entry.Text = string.Empty;
        PartUnknown2Entry.Text = string.Empty;
        PartUnknown3Entry.Text = string.Empty;
        PartUnknown4Entry.Text = string.Empty;
        PartUnknown5Entry.Text = string.Empty;
        PartUnknown6Entry.Text = string.Empty;
        PartUnknown7Entry.Text = string.Empty;
        PartUnknown8Entry.Text = string.Empty;
        PartEditor.Text = "Select a part to inspect its decoded stats.";
        PartActionAnalysisEditor.Text = "Select a combat part to inspect its action family, opcode route, and known sequence notes.";
        ResetPartEditorLabels(PartKind.Head);
    }

    private void ClearSpritePreview()
    {
        _selectedSpriteNode = null;
        SpritePreviewImage.Source = null;
        SpritePreviewImage.Width = double.NaN;
        SpritePreviewImage.Height = double.NaN;
        SpriteGridCanvas.Children.Clear();
        SpriteGridCanvas.Width = 0;
        SpriteGridCanvas.Height = 0;
        SpriteSummaryLabel.Text = "Select an overworld sheet or portrait to inspect its decoded image and palette data.";
        SpritePaletteSummaryLabel.Text = string.Empty;
        SpritePaletteItemsControl.ItemsSource = null;
        SpritePatchStatusLabel.Text = string.Empty;
        _selectedPaletteIndex = 1;
        _hasCapturedUndoForCurrentStroke = false;
    }

    private List<SpriteBrowserNode> BuildSpriteTreeNodes()
    {
        var nodes = new List<SpriteBrowserNode>();
        nodes.Add(BuildOverworldSpriteRoot());
        nodes.Add(BuildPortraitRoot());
        return nodes;
    }

    private SpriteBrowserNode BuildOverworldSpriteRoot()
    {
        const int groupSize = 32;
        const int characterSpriteLimit = 88;
        var root = new SpriteBrowserNode
        {
            Title = "Overworld Event Object Sheets",
            FilterText = "Overworld Event Object Sheets"
        };
        root.Children.Add(BuildOverworldSpriteSubgroup("Character Sheets", Enumerable.Range(0, characterSpriteLimit), groupSize));
        var validOtherIds = GetValidOverworldSheetIds(characterSpriteLimit, MedabotsRomSchema.SpriteCount - 1).ToArray();
        if (validOtherIds.Length > 0)
        {
            root.Children.Add(BuildOverworldSpriteSubgroup("Other Event Object Sheets", validOtherIds, groupSize));
        }
        return root;
    }

    private static SpriteBrowserNode BuildOverworldSpriteSubgroup(string title, IEnumerable<int> spriteIds, int groupSize)
    {
        var root = new SpriteBrowserNode
        {
            Title = title,
            FilterText = title
        };
        var ids = spriteIds.OrderBy(id => id).ToArray();
        for (var blockStart = 0; blockStart < ids.Length; blockStart += groupSize)
        {
            var block = ids.Skip(blockStart).Take(groupSize).ToArray();
            var start = block.First();
            var end = block.Last();
            var group = new SpriteBrowserNode
            {
                Title = $"Sheets {start:D3}-{end:D3}",
                FilterText = $"{title} {start:D3} {end:D3}"
            };

            foreach (var spriteId in block)
            {
                group.Children.Add(new SpriteBrowserNode
                {
                    Title = $"Sheet {spriteId:D3}",
                    FilterText = $"{title} {spriteId:D3} Overworld Sheet",
                    AssetKind = SpriteAssetKind.OverworldEventObject,
                    PrimaryId = spriteId
                });
            }

            root.Children.Add(group);
        }

        return root;
    }

    private IEnumerable<int> GetValidOverworldSheetIds(int firstId, int lastId)
    {
        if (_session is null)
        {
            yield break;
        }

        for (var spriteId = firstId; spriteId <= lastId; spriteId++)
        {
            if (HasValidOverworldSheetPointers(_session.RomFile, spriteId))
            {
                yield return spriteId;
            }
        }
    }

    private static bool HasValidOverworldSheetPointers(RomFile romFile, int spriteId)
    {
        var imagePointerOffset = MedabotsRomSchema.SpritePointerTableOffset + (spriteId * sizeof(uint));
        var palettePointerOffset = MedabotsRomSchema.SpritePaletteTableOffset + (spriteId * sizeof(uint));
        return GbaPointer.TryReadFileOffset(romFile.Data, imagePointerOffset, out var imageOffset) &&
               GbaPointer.TryReadFileOffset(romFile.Data, palettePointerOffset, out var paletteOffset) &&
               imageOffset > 0 &&
               paletteOffset > 0 &&
               imageOffset < romFile.Length &&
               paletteOffset + MedabotsRomSchema.PaletteSize <= romFile.Length;
    }

    private static SpriteBrowserNode BuildPortraitRoot()
    {
        const int groupSize = 16;
        var root = new SpriteBrowserNode
        {
            Title = "Portraits",
            FilterText = "Portraits"
        };
        for (var start = 0; start < MedabotsRomSchema.PortraitCharacterCount; start += groupSize)
        {
            var end = Math.Min(start + groupSize - 1, MedabotsRomSchema.PortraitCharacterCount - 1);
            var group = new SpriteBrowserNode
            {
                Title = $"Characters {start:D3}-{end:D3}",
                FilterText = $"Portrait Character {start:D3} {end:D3}"
            };

            for (var characterId = start; characterId <= end; characterId++)
            {
                var character = new SpriteBrowserNode
                {
                    Title = $"Character {characterId:D3}",
                    FilterText = $"Portrait Character {characterId:D3}"
                };
                for (var portraitIndex = 0; portraitIndex < MedabotsRomSchema.PortraitsPerCharacter; portraitIndex++)
                {
                    character.Children.Add(new SpriteBrowserNode
                    {
                        Title = $"Portrait {portraitIndex}",
                        FilterText = $"Portrait Character {characterId:D3} Portrait {portraitIndex}",
                        AssetKind = SpriteAssetKind.Portrait,
                        PrimaryId = characterId,
                        SecondaryId = portraitIndex
                    });
                }

                group.Children.Add(character);
            }

            root.Children.Add(group);
        }

        return root;
    }

    private static IEnumerable<SpriteBrowserNode> FilterSpriteNodes(IEnumerable<SpriteBrowserNode> nodes, string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return nodes;
        }

        var trimmed = filter.Trim();
        var filtered = new List<SpriteBrowserNode>();
        foreach (var node in nodes)
        {
            if (node.IsAsset)
            {
                if (node.FilterText.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
                    node.Title.Contains(trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    filtered.Add(node);
                }

                continue;
            }

            var matchingChildren = node.Children
                .SelectMany(child => FilterSpriteNodes([child], trimmed))
                .ToList();
            if (matchingChildren.Count == 0)
            {
                continue;
            }

            var parent = new SpriteBrowserNode
            {
                Title = node.Title,
                FilterText = node.FilterText
            };
            foreach (var child in matchingChildren)
            {
                parent.Children.Add(child);
            }

            filtered.Add(parent);
        }

        return filtered;
    }

    private async void OnSpriteSelectionChanged(object? sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (_session is null)
        {
            return;
        }

        if (e.NewValue is not SpriteBrowserNode node || !node.IsAsset)
        {
            _selectedSpriteNode = null;
            if (e.NewValue is not SpriteBrowserNode)
            {
                ClearSpritePreview();
            }
            return;
        }

        try
        {
            _selectedSpriteNode = node;
            var cacheKey = $"{(int)node.AssetKind}:{node.PrimaryId}:{node.SecondaryId}";
            if (!_spritePreviewCache.TryGetValue(cacheKey, out var preview))
            {
                preview = node.AssetKind switch
                {
                    SpriteAssetKind.OverworldEventObject => BuildOverworldSpritePreviewState(GetCurrentOverworldSpriteAsset(node.PrimaryId)),
                    SpriteAssetKind.Portrait => BuildPortraitPreviewState(GetCurrentPortraitAsset(node.PrimaryId, node.SecondaryId)),
                    _ => throw new InvalidOperationException("Unsupported sprite asset kind.")
                };
                _spritePreviewCache[cacheKey] = preview;
            }

            SpritePreviewImage.Source = preview.Bitmap;
            SpritePreviewImage.Width = preview.Bitmap.PixelWidth * _spriteEditorZoom;
            SpritePreviewImage.Height = preview.Bitmap.PixelHeight * _spriteEditorZoom;
            SpriteSummaryLabel.Text = preview.Summary;
            SpritePaletteSummaryLabel.Text = preview.PaletteSummary;
            SpritePaletteItemsControl.ItemsSource = preview.Swatches;
            SpritePatchStatusLabel.Text = GetSpritePatchStatusText(node);
            UpdateSelectedPaletteSwatch();
            UpdateSpriteGridOverlay(preview.Bitmap.PixelWidth, preview.Bitmap.PixelHeight);
        }
        catch (Exception ex)
        {
            ClearSpritePreview();
            await DisplayAlertAsync("Sprite Load Failed", ex.Message, "OK");
        }
    }

    private static SpritePreviewState BuildOverworldSpritePreviewState(SpriteAsset asset)
    {
        var swatches = BuildPaletteSwatches(asset.Image.PaletteBytes);
        var bitmap = CreateBitmapSource(asset.Image.PixelIndices, 2, swatches);
        var width = bitmap.PixelWidth;
        var height = bitmap.PixelHeight;
        var summary = $"Overworld sheet {asset.SpriteId:D3}{Environment.NewLine}" +
                      $"Image: {width}x{height}px{Environment.NewLine}" +
                      $"Layout: tile width 2 (16px){Environment.NewLine}" +
                      $"Image pointer entry: 0x{asset.ImagePointerOffset:X6} -> 0x{asset.ImageOffset:X6}{Environment.NewLine}" +
                      $"Palette pointer entry: 0x{asset.PalettePointerOffset:X6} -> 0x{asset.PaletteOffset:X6}";
        var paletteSummary = $"Palette colors: {swatches.Count}  |  Format: GBA BGR555";
        return new SpritePreviewState(asset.SpriteId, bitmap, summary, paletteSummary, swatches);
    }

    private static SpritePreviewState BuildPortraitPreviewState(PortraitAsset asset)
    {
        var swatches = BuildPaletteSwatches(asset.Image.PaletteBytes);
        var bitmap = CreateBitmapSource(asset.Image.PixelIndices, asset.Image.TileWidth, swatches);
        var summary = $"Portrait {asset.CharacterId:D3}:{asset.PortraitIndex}{Environment.NewLine}" +
                      $"Image: {asset.Image.Width}x{asset.Image.Height}px{Environment.NewLine}" +
                      $"Image pointer entry: 0x{asset.ImagePointerOffset:X6} -> 0x{asset.ImageOffset:X6}{Environment.NewLine}" +
                      $"Palette pointer entry: 0x{asset.PalettePointerOffset:X6} -> 0x{asset.PaletteOffset:X6}";
        var paletteSummary = $"Palette colors: {swatches.Count}  |  Format: GBA BGR555";
        return new SpritePreviewState(asset.CharacterId, bitmap, summary, paletteSummary, swatches);
    }

    private static IReadOnlyList<PaletteSwatchItem> BuildPaletteSwatches(byte[] paletteBytes)
    {
        var swatches = new List<PaletteSwatchItem>(paletteBytes.Length / 2);
        for (var index = 0; index + 1 < paletteBytes.Length; index += 2)
        {
            var raw = (ushort)(paletteBytes[index] | (paletteBytes[index + 1] << 8));
            var color = DecodeGbaColor(raw);
            swatches.Add(new PaletteSwatchItem
            {
                Index = index / 2,
                Color = color,
                Hex = $"#{color.R:X2}{color.G:X2}{color.B:X2}"
            });
        }

        return swatches;
    }

    private static BitmapSource CreateBitmapSource(byte[] pixelIndices, int tileWidth, IReadOnlyList<PaletteSwatchItem> swatches)
    {
        var tileCount = pixelIndices.Length / 64;
        var width = tileWidth * 8;
        var height = Math.Max(1, tileCount / Math.Max(1, tileWidth)) * 8;
        var pixels = new byte[width * height * 4];
        for (var tileIndex = 0; tileIndex < tileCount; tileIndex++)
        {
            var tileX = tileIndex % tileWidth;
            var tileY = tileIndex / tileWidth;
            BlitTile(pixelIndices, tileIndex, width, height, tileX * 8, tileY * 8, swatches, pixels);
        }

        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        bitmap.Freeze();
        return bitmap;
    }

    private static void BlitTile(byte[] pixelIndices, int tileIndex, int bitmapWidth, int bitmapHeight, int destX, int destY, IReadOnlyList<PaletteSwatchItem> swatches, byte[] output)
    {
        var tileBase = tileIndex * 64;
        for (var localY = 0; localY < 8; localY++)
        {
            for (var localX = 0; localX < 8; localX++)
            {
                var sourceIndex = tileBase + (localY * 8) + localX;
                if (sourceIndex >= pixelIndices.Length)
                {
                    return;
                }

                var pixelX = destX + localX;
                var pixelY = destY + localY;
                if (pixelX >= bitmapWidth || pixelY >= bitmapHeight)
                {
                    continue;
                }

                var colorIndex = pixelIndices[sourceIndex];
                var color = colorIndex < swatches.Count ? swatches[colorIndex].Color : Colors.Transparent;
                var outputIndex = ((pixelY * bitmapWidth) + pixelX) * 4;
                output[outputIndex + 0] = color.B;
                output[outputIndex + 1] = color.G;
                output[outputIndex + 2] = color.R;
                output[outputIndex + 3] = colorIndex == 0 ? (byte)0 : (byte)255;
            }
        }
    }

    private static Color DecodeGbaColor(ushort rawColor)
    {
        static byte Expand5To8(int value) => (byte)((value << 3) | (value >> 2));

        var red = Expand5To8(rawColor & 0x1F);
        var green = Expand5To8((rawColor >> 5) & 0x1F);
        var blue = Expand5To8((rawColor >> 10) & 0x1F);
        return Color.FromRgb(red, green, blue);
    }

    private SpriteAsset GetCurrentOverworldSpriteAsset(int spriteId)
    {
        if (_editedOverworldSpriteAssets.TryGetValue(spriteId, out var edited))
        {
            return edited;
        }

        if (_session is null)
        {
            throw new InvalidOperationException("No ROM session is open.");
        }

        return _imageAssetRepository.ReadSprite(_session.RomFile, spriteId);
    }

    private PortraitAsset GetCurrentPortraitAsset(int characterId, int portraitIndex)
    {
        if (_editedPortraitAssets.TryGetValue((characterId, portraitIndex), out var edited))
        {
            return edited;
        }

        if (_session is null)
        {
            throw new InvalidOperationException("No ROM session is open.");
        }

        return _imageAssetRepository.ReadPortrait(_session.RomFile, characterId, portraitIndex);
    }

    private string GetSpritePatchStatusText(SpriteBrowserNode node)
    {
        return node.AssetKind switch
        {
            SpriteAssetKind.OverworldEventObject when _editedOverworldSpriteAssets.ContainsKey(node.PrimaryId)
                => "Status: imported changes are staged in memory. Use Apply Changes to write them into the ROM session.",
            SpriteAssetKind.Portrait when _editedPortraitAssets.ContainsKey((node.PrimaryId, node.SecondaryId))
                => "Status: imported changes are staged in memory. Use Apply Changes to write them into the ROM session.",
            _ => "Status: showing ROM data."
        };
    }

    private void UpdateSelectedPaletteSwatch()
    {
        if (SpritePaletteItemsControl.ItemsSource is not IEnumerable<PaletteSwatchItem> swatches)
        {
            return;
        }

        foreach (var swatch in swatches)
        {
            swatch.IsSelected = swatch.Index == _selectedPaletteIndex;
        }

        SpritePaletteItemsControl.Items.Refresh();
    }

    private void SetSpriteEditorTool(SpriteEditorTool tool)
    {
        _selectedSpriteEditorTool = tool;
        SpriteToolPencilButton.IsChecked = tool == SpriteEditorTool.Pencil;
        SpriteToolEraserButton.IsChecked = tool == SpriteEditorTool.Eraser;
        SpriteToolPickerButton.IsChecked = tool == SpriteEditorTool.Picker;
    }

    private void OnSpriteZoomChanged(object? sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _spriteEditorZoom = Math.Max(1, (int)Math.Round(e.NewValue));
        if (SpriteZoomValueLabel is not null)
        {
            SpriteZoomValueLabel.Text = $"{_spriteEditorZoom}x";
        }

        if (SpritePreviewImage is not null && SpritePreviewImage.Source is BitmapSource bitmap)
        {
            SpritePreviewImage.Width = bitmap.PixelWidth * _spriteEditorZoom;
            SpritePreviewImage.Height = bitmap.PixelHeight * _spriteEditorZoom;
            UpdateSpriteGridOverlay(bitmap.PixelWidth, bitmap.PixelHeight);
        }
    }

    private void OnSpritePreviewMouseWheel(object? sender, MouseWheelEventArgs e)
    {
        if (SpriteZoomSlider is null)
        {
            return;
        }

        var delta = e.Delta > 0 ? 1 : -1;
        var nextValue = Math.Clamp(SpriteZoomSlider.Value + delta, SpriteZoomSlider.Minimum, SpriteZoomSlider.Maximum);
        if (Math.Abs(nextValue - SpriteZoomSlider.Value) < double.Epsilon)
        {
            return;
        }

        SpriteZoomSlider.Value = nextValue;
        e.Handled = true;
    }

    private void OnSpriteToolPencilClicked(object? sender, RoutedEventArgs e) => SetSpriteEditorTool(SpriteEditorTool.Pencil);
    private void OnSpriteToolEraserClicked(object? sender, RoutedEventArgs e) => SetSpriteEditorTool(SpriteEditorTool.Eraser);
    private void OnSpriteToolPickerClicked(object? sender, RoutedEventArgs e) => SetSpriteEditorTool(SpriteEditorTool.Picker);

    private void OnSpritePaletteSwatchClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not int index)
        {
            return;
        }

        _selectedPaletteIndex = index;
        UpdateSelectedPaletteSwatch();
    }

    private void UpdateSpriteGridOverlay(int pixelWidth, int pixelHeight)
    {
        SpriteGridCanvas.Children.Clear();
        SpriteGridCanvas.Width = pixelWidth * _spriteEditorZoom;
        SpriteGridCanvas.Height = pixelHeight * _spriteEditorZoom;

        if (_spriteEditorZoom < 8)
        {
            return;
        }

        var gridBrush = new SolidColorBrush(Color.FromArgb(80, 107, 114, 128));
        for (var x = 0; x <= pixelWidth; x++)
        {
            SpriteGridCanvas.Children.Add(new System.Windows.Shapes.Line
            {
                X1 = x * _spriteEditorZoom,
                Y1 = 0,
                X2 = x * _spriteEditorZoom,
                Y2 = pixelHeight * _spriteEditorZoom,
                Stroke = gridBrush,
                StrokeThickness = x % 8 == 0 ? 1.0 : 0.5
            });
        }

        for (var y = 0; y <= pixelHeight; y++)
        {
            SpriteGridCanvas.Children.Add(new System.Windows.Shapes.Line
            {
                X1 = 0,
                Y1 = y * _spriteEditorZoom,
                X2 = pixelWidth * _spriteEditorZoom,
                Y2 = y * _spriteEditorZoom,
                Stroke = gridBrush,
                StrokeThickness = y % 8 == 0 ? 1.0 : 0.5
            });
        }
    }

    private void OnSpritePreviewMouseLeftButtonDown(object? sender, MouseButtonEventArgs e)
    {
        if (_selectedSpriteNode is null || SpritePreviewImage.Source is not BitmapSource)
        {
            return;
        }

        _isPaintingSprite = true;
        _hasCapturedUndoForCurrentStroke = false;
        SpritePreviewImage.CaptureMouse();
        ApplySpriteToolAtPoint(e.GetPosition(SpritePreviewImage));
    }

    private void OnSpritePreviewMouseMove(object? sender, MouseEventArgs e)
    {
        if (!_isPaintingSprite || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        ApplySpriteToolAtPoint(e.GetPosition(SpritePreviewImage));
    }

    private void OnSpritePreviewMouseLeftButtonUp(object? sender, MouseButtonEventArgs e)
    {
        _isPaintingSprite = false;
        _hasCapturedUndoForCurrentStroke = false;
        SpritePreviewImage.ReleaseMouseCapture();
    }

    private void ApplySpriteToolAtPoint(Point point)
    {
        if (_selectedSpriteNode is null || !TryResolveSpritePixel(point, out var pixelX, out var pixelY))
        {
            return;
        }

        switch (_selectedSpriteNode.AssetKind)
        {
            case SpriteAssetKind.OverworldEventObject:
            {
                var asset = GetEditableOverworldSpriteAsset(_selectedSpriteNode.PrimaryId);
                ApplyToolToIndexedImage(asset.Image, pixelX, pixelY);
                break;
            }
            case SpriteAssetKind.Portrait:
            {
                var asset = GetEditablePortraitAsset(_selectedSpriteNode.PrimaryId, _selectedSpriteNode.SecondaryId);
                ApplyToolToIndexedImage(asset.Image, pixelX, pixelY);
                break;
            }
            default:
                return;
        }

        InvalidateSelectedSpritePreview();
    }

    private bool TryResolveSpritePixel(Point point, out int pixelX, out int pixelY)
    {
        pixelX = (int)(point.X / _spriteEditorZoom);
        pixelY = (int)(point.Y / _spriteEditorZoom);
        if (_selectedSpriteNode is null)
        {
            return false;
        }

        var image = _selectedSpriteNode.AssetKind switch
        {
            SpriteAssetKind.OverworldEventObject => GetCurrentOverworldSpriteAsset(_selectedSpriteNode.PrimaryId).Image,
            SpriteAssetKind.Portrait => GetCurrentPortraitAsset(_selectedSpriteNode.PrimaryId, _selectedSpriteNode.SecondaryId).Image,
            _ => null
        };
        if (image is null)
        {
            return false;
        }

        return pixelX >= 0 && pixelY >= 0 && pixelX < image.Width && pixelY < image.Height;
    }

    private SpriteAsset GetEditableOverworldSpriteAsset(int spriteId)
    {
        if (_editedOverworldSpriteAssets.TryGetValue(spriteId, out var edited))
        {
            return edited;
        }

        var current = GetCurrentOverworldSpriteAsset(spriteId);
        var clone = current with
        {
            Image = new IndexedImage(current.Image.TileWidth, current.Image.TileHeight, current.Image.PixelIndices.ToArray(), current.Image.PaletteBytes.ToArray())
        };
        _editedOverworldSpriteAssets[spriteId] = clone;
        return clone;
    }

    private PortraitAsset GetEditablePortraitAsset(int characterId, int portraitIndex)
    {
        if (_editedPortraitAssets.TryGetValue((characterId, portraitIndex), out var edited))
        {
            return edited;
        }

        var current = GetCurrentPortraitAsset(characterId, portraitIndex);
        var clone = current with
        {
            Image = new IndexedImage(current.Image.TileWidth, current.Image.TileHeight, current.Image.PixelIndices.ToArray(), current.Image.PaletteBytes.ToArray())
        };
        _editedPortraitAssets[(characterId, portraitIndex)] = clone;
        return clone;
    }

    private void ApplyToolToIndexedImage(IndexedImage image, int pixelX, int pixelY)
    {
        var pixelIndex = GetTileOrderedPixelIndex(image, pixelX, pixelY);
        if (pixelIndex < 0 || pixelIndex >= image.PixelIndices.Length)
        {
            return;
        }

        switch (_selectedSpriteEditorTool)
        {
            case SpriteEditorTool.Pencil:
                if (image.PixelIndices[pixelIndex] != (byte)_selectedPaletteIndex)
                {
                    CaptureUndoSnapshotForCurrentStroke(image.PixelIndices);
                    image.PixelIndices[pixelIndex] = (byte)_selectedPaletteIndex;
                }
                break;
            case SpriteEditorTool.Eraser:
                if (image.PixelIndices[pixelIndex] != 0)
                {
                    CaptureUndoSnapshotForCurrentStroke(image.PixelIndices);
                    image.PixelIndices[pixelIndex] = 0;
                }
                break;
            case SpriteEditorTool.Picker:
                _selectedPaletteIndex = image.PixelIndices[pixelIndex];
                UpdateSelectedPaletteSwatch();
                break;
        }
    }

    private void CaptureUndoSnapshotForCurrentStroke(byte[] pixels)
    {
        if (_hasCapturedUndoForCurrentStroke)
        {
            return;
        }

        PushUndoSnapshot(GetSelectedSpriteHistoryKey(), pixels);
        _hasCapturedUndoForCurrentStroke = true;
    }

    private static int GetTileOrderedPixelIndex(IndexedImage image, int pixelX, int pixelY)
    {
        if (pixelX < 0 || pixelY < 0 || pixelX >= image.Width || pixelY >= image.Height)
        {
            return -1;
        }

        var tileX = pixelX / 8;
        var tileY = pixelY / 8;
        var localX = pixelX % 8;
        var localY = pixelY % 8;
        var tileIndex = (tileY * image.TileWidth) + tileX;
        return (tileIndex * 64) + (localY * 8) + localX;
    }

    private static byte[] ConvertRasterToTileOrdered(byte[] rasterPixels, int width, int height, int tileWidth, int tileHeight)
    {
        var tileOrderedPixels = new byte[rasterPixels.Length];
        var image = new IndexedImage(tileWidth, tileHeight, tileOrderedPixels, Array.Empty<byte>());

        for (var pixelY = 0; pixelY < height; pixelY++)
        {
            for (var pixelX = 0; pixelX < width; pixelX++)
            {
                var rasterIndex = (pixelY * width) + pixelX;
                var tileIndex = GetTileOrderedPixelIndex(image, pixelX, pixelY);
                tileOrderedPixels[tileIndex] = rasterPixels[rasterIndex];
            }
        }

        return tileOrderedPixels;
    }

    private string GetSelectedSpriteHistoryKey()
    {
        if (_selectedSpriteNode is null)
        {
            throw new InvalidOperationException("No sprite or portrait is selected.");
        }

        return $"{(int)_selectedSpriteNode.AssetKind}:{_selectedSpriteNode.PrimaryId}:{_selectedSpriteNode.SecondaryId}";
    }

    private void PushUndoSnapshot(string historyKey, byte[] pixels)
    {
        if (!_spriteEditHistories.TryGetValue(historyKey, out var history))
        {
            history = new SpriteEditHistory();
            _spriteEditHistories[historyKey] = history;
        }

        history.Push(pixels);
    }

    private async void OnUndoSpriteEditClicked(object? sender, RoutedEventArgs e)
    {
        if (_selectedSpriteNode is null)
        {
            await DisplayAlertAsync("No Asset Selected", "Select a sprite or portrait to undo.", "OK");
            return;
        }

        var historyKey = GetSelectedSpriteHistoryKey();
        if (!_spriteEditHistories.TryGetValue(historyKey, out var history) || !history.CanUndo)
        {
            SpritePatchStatusLabel.Text = "Status: nothing to undo for this asset.";
            return;
        }

        var pixels = history.Pop();
        switch (_selectedSpriteNode.AssetKind)
        {
            case SpriteAssetKind.OverworldEventObject:
                Array.Copy(pixels, GetEditableOverworldSpriteAsset(_selectedSpriteNode.PrimaryId).Image.PixelIndices, pixels.Length);
                break;
            case SpriteAssetKind.Portrait:
                Array.Copy(pixels, GetEditablePortraitAsset(_selectedSpriteNode.PrimaryId, _selectedSpriteNode.SecondaryId).Image.PixelIndices, pixels.Length);
                break;
        }

        _hasCapturedUndoForCurrentStroke = false;
        InvalidateSelectedSpritePreview();
        SpritePatchStatusLabel.Text = "Status: reverted the last staged edit for this asset.";
    }

    private async void OnRevertSelectedSpriteChangesClicked(object? sender, RoutedEventArgs e)
    {
        if (_selectedSpriteNode is null)
        {
            await DisplayAlertAsync("No Asset Selected", "Select a sprite or portrait to reset.", "OK");
            return;
        }

        switch (_selectedSpriteNode.AssetKind)
        {
            case SpriteAssetKind.OverworldEventObject:
                _editedOverworldSpriteAssets.Remove(_selectedSpriteNode.PrimaryId);
                break;
            case SpriteAssetKind.Portrait:
                _editedPortraitAssets.Remove((_selectedSpriteNode.PrimaryId, _selectedSpriteNode.SecondaryId));
                break;
        }

        _spriteEditHistories.Remove(GetSelectedSpriteHistoryKey());
        _hasCapturedUndoForCurrentStroke = false;
        InvalidateSelectedSpritePreview();
        SpritePatchStatusLabel.Text = "Status: reset this asset back to the ROM version.";
    }

    private void OnRevertAllSpriteChangesClicked(object? sender, RoutedEventArgs e)
    {
        _editedOverworldSpriteAssets.Clear();
        _editedPortraitAssets.Clear();
        _spriteEditHistories.Clear();
        _spritePreviewCache.Clear();
        _hasCapturedUndoForCurrentStroke = false;

        if (_selectedSpriteNode is not null)
        {
            InvalidateSelectedSpritePreview();
        }
        else
        {
            ClearSpritePreview();
        }

        SpritePatchStatusLabel.Text = "Status: cleared all staged sprite and portrait changes.";
    }

    private async void OnExportSpritePngClicked(object? sender, RoutedEventArgs e)
    {
        if (_selectedSpriteNode is null || !(_selectedSpriteNode?.IsAsset ?? false) || SpritePreviewImage.Source is not BitmapSource bitmap)
        {
            await DisplayAlertAsync("No Asset Selected", "Select a sprite or portrait to export.", "OK");
            return;
        }

        var title = _selectedSpriteNode.AssetKind switch
        {
            SpriteAssetKind.OverworldEventObject => $"sprite_{_selectedSpriteNode.PrimaryId:D3}.png",
            SpriteAssetKind.Portrait => $"portrait_{_selectedSpriteNode.PrimaryId:D3}_{_selectedSpriteNode.SecondaryId}.png",
            _ => "asset.png"
        };
        var path = PickSaveFilePath("Export sprite PNG", "PNG image (*.png)|*.png", title);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        await using var stream = File.Create(path);
        encoder.Save(stream);
        SpritePatchStatusLabel.Text = $"Status: exported PNG to {path}";
    }

    private async void OnImportSpritePngClicked(object? sender, RoutedEventArgs e)
    {
        if (_selectedSpriteNode is null || !(_selectedSpriteNode?.IsAsset ?? false))
        {
            await DisplayAlertAsync("No Asset Selected", "Select a sprite or portrait to import.", "OK");
            return;
        }

        var path = PickOpenFilePath("Import PNG", "PNG image (*.png)|*.png|All files (*.*)|*.*");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            switch (_selectedSpriteNode.AssetKind)
            {
                case SpriteAssetKind.OverworldEventObject:
                {
                    var current = GetEditableOverworldSpriteAsset(_selectedSpriteNode.PrimaryId);
                    PushUndoSnapshot(GetSelectedSpriteHistoryKey(), current.Image.PixelIndices);
                    var updated = current with { Image = ImportIndexedImageFromPng(path, current.Image) };
                    _editedOverworldSpriteAssets[_selectedSpriteNode.PrimaryId] = updated;
                    break;
                }
                case SpriteAssetKind.Portrait:
                {
                    var current = GetEditablePortraitAsset(_selectedSpriteNode.PrimaryId, _selectedSpriteNode.SecondaryId);
                    PushUndoSnapshot(GetSelectedSpriteHistoryKey(), current.Image.PixelIndices);
                    var updated = current with { Image = ImportIndexedImageFromPng(path, current.Image) };
                    _editedPortraitAssets[(_selectedSpriteNode.PrimaryId, _selectedSpriteNode.SecondaryId)] = updated;
                    break;
                }
            }

            _hasCapturedUndoForCurrentStroke = false;
            InvalidateSelectedSpritePreview();
            SpritePatchStatusLabel.Text = "Status: imported PNG and staged changes in memory.";
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Import Failed", ex.Message, "OK");
        }
    }

    private async void OnApplySpriteChangesClicked(object? sender, RoutedEventArgs e)
    {
        if (_session is null || _selectedSpriteNode is null || !_selectedSpriteNode.IsAsset)
        {
            await DisplayAlertAsync("No Asset Selected", "Select a sprite or portrait to apply changes.", "OK");
            return;
        }

        try
        {
            switch (_selectedSpriteNode.AssetKind)
            {
                case SpriteAssetKind.OverworldEventObject:
                    if (_editedOverworldSpriteAssets.TryGetValue(_selectedSpriteNode.PrimaryId, out var sprite))
                    {
                        _imageAssetPatcher.ApplySpriteSmart(_session, sprite);
                        _editedOverworldSpriteAssets.Remove(_selectedSpriteNode.PrimaryId);
                        _spriteEditHistories.Remove(GetSelectedSpriteHistoryKey());
                    }
                    break;
                case SpriteAssetKind.Portrait:
                    var portraitKey = (_selectedSpriteNode.PrimaryId, _selectedSpriteNode.SecondaryId);
                    if (_editedPortraitAssets.TryGetValue(portraitKey, out var portrait))
                    {
                        _imageAssetPatcher.ApplyPortraitSmart(_session, portrait);
                        _editedPortraitAssets.Remove(portraitKey);
                        _spriteEditHistories.Remove(GetSelectedSpriteHistoryKey());
                    }
                    break;
            }

            UpdateStatus();
            _hasCapturedUndoForCurrentStroke = false;
            InvalidateSelectedSpritePreview();
            SpritePatchStatusLabel.Text = "Status: changes applied to the current ROM session.";
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Apply Failed", ex.Message, "OK");
        }
    }

    private void InvalidateSelectedSpritePreview()
    {
        if (_selectedSpriteNode is null)
        {
            return;
        }

        _spritePreviewCache.Remove($"{(int)_selectedSpriteNode.AssetKind}:{_selectedSpriteNode.PrimaryId}:{_selectedSpriteNode.SecondaryId}");
        OnSpriteSelectionChanged(SpriteTreeView, new RoutedPropertyChangedEventArgs<object>(_selectedSpriteNode, _selectedSpriteNode));
    }

    private static IndexedImage ImportIndexedImageFromPng(string path, IndexedImage referenceImage)
    {
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var source = decoder.Frames.FirstOrDefault() ?? throw new InvalidOperationException("The PNG did not contain a readable frame.");

        var targetWidth = referenceImage.Width;
        var targetHeight = referenceImage.Height;
        if (source.PixelWidth != targetWidth || source.PixelHeight != targetHeight)
        {
            throw new InvalidOperationException($"Imported image must be exactly {targetWidth}x{targetHeight} pixels.");
        }

        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var pixels = new byte[targetWidth * targetHeight * 4];
        converted.CopyPixels(pixels, targetWidth * 4, 0);

        var swatches = BuildPaletteSwatches(referenceImage.PaletteBytes);
        var rasterPixels = new byte[targetWidth * targetHeight];
        for (var i = 0; i < rasterPixels.Length; i++)
        {
            var pixelOffset = i * 4;
            var blue = pixels[pixelOffset + 0];
            var green = pixels[pixelOffset + 1];
            var red = pixels[pixelOffset + 2];
            var alpha = pixels[pixelOffset + 3];
            if (alpha < 0x80)
            {
                rasterPixels[i] = 0;
                continue;
            }

            rasterPixels[i] = FindNearestPaletteIndex(swatches, Color.FromRgb(red, green, blue));
        }

        var indexedPixels = ConvertRasterToTileOrdered(rasterPixels, targetWidth, targetHeight, referenceImage.TileWidth, referenceImage.TileHeight);
        return new IndexedImage(referenceImage.TileWidth, referenceImage.TileHeight, indexedPixels, referenceImage.PaletteBytes.ToArray());
    }

    private static byte FindNearestPaletteIndex(IReadOnlyList<PaletteSwatchItem> swatches, Color color)
    {
        var bestIndex = 0;
        var bestDistance = int.MaxValue;
        for (var i = 0; i < swatches.Count; i++)
        {
            var swatch = swatches[i].Color;
            var dr = swatch.R - color.R;
            var dg = swatch.G - color.G;
            var db = swatch.B - color.B;
            var distance = (dr * dr) + (dg * dg) + (db * db);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return (byte)bestIndex;
    }

    private static string BuildEventSummary(EventScript script)
    {
        if (script.Instructions.Count == 0)
        {
            return "Empty";
        }

        var firstInstruction = script.Instructions[0];
        return $"{firstInstruction.Name} ({script.Instructions.Count} instructions)";
    }
    private void PopulateBattleEditor(BattleDefinition battle)
    {
        BattleCharacterEntry.Text = battle.CharacterId.ToString();
        BattleBotCountEntry.Text = battle.NumberOfBots.ToString();
        PopulateBattleBotEntries(battle.Bots[0], BattleBot1HeadEntry, BattleBot1RightEntry, BattleBot1LeftEntry, BattleBot1LegsEntry, BattleBot1MedalEntry, BattleBot1LevelEntry);
        PopulateBattleBotEntries(battle.Bots[1], BattleBot2HeadEntry, BattleBot2RightEntry, BattleBot2LeftEntry, BattleBot2LegsEntry, BattleBot2MedalEntry, BattleBot2LevelEntry);
        PopulateBattleBotEntries(battle.Bots[2], BattleBot3HeadEntry, BattleBot3RightEntry, BattleBot3LeftEntry, BattleBot3LegsEntry, BattleBot3MedalEntry, BattleBot3LevelEntry);
    }

    private static void PopulateBattleBotEntries(BattleBot bot, TextBox head, TextBox right, TextBox left, TextBox legs, TextBox medal, TextBox level)
    {
        head.Text = bot.HeadPartId.ToString();
        right.Text = bot.RightArmPartId.ToString();
        left.Text = bot.LeftArmPartId.ToString();
        legs.Text = bot.LegsPartId.ToString();
        medal.Text = bot.MedalId.ToString();
        level.Text = bot.MedalLevel.ToString();
    }

    private BattleDefinition BuildBattleFromEditor(BattleDefinition original)
    {
        BattleBot[] bots =
        [
            BuildBattleBot(original.Bots[0], BattleBot1HeadEntry, BattleBot1RightEntry, BattleBot1LeftEntry, BattleBot1LegsEntry, BattleBot1MedalEntry, BattleBot1LevelEntry),
            BuildBattleBot(original.Bots[1], BattleBot2HeadEntry, BattleBot2RightEntry, BattleBot2LeftEntry, BattleBot2LegsEntry, BattleBot2MedalEntry, BattleBot2LevelEntry),
            BuildBattleBot(original.Bots[2], BattleBot3HeadEntry, BattleBot3RightEntry, BattleBot3LeftEntry, BattleBot3LegsEntry, BattleBot3MedalEntry, BattleBot3LevelEntry)
        ];

        return new BattleDefinition(original.Id, original.PointerOffset, original.DataOffset, ParseByte(BattleCharacterEntry.Text, "Battle character"), original.Unknown1, ParseByte(BattleBotCountEntry.Text, "Battle bot count"), bots, original.AlwaysZero);
    }

    private static BattleBot BuildBattleBot(BattleBot original, TextBox head, TextBox right, TextBox left, TextBox legs, TextBox medal, TextBox level)
    {
        return new BattleBot(original.Unknown, ParseByte(head.Text, "Head part"), ParseByte(right.Text, "Right arm part"), ParseByte(left.Text, "Left arm part"), ParseByte(legs.Text, "Leg part"), ParseByte(medal.Text, "Medal"), ParseByte(level.Text, "Medal level"), original.Unknown1, original.Unknown2, original.Unknown3, original.Unknown4, original.Unknown5);
    }

    private void PopulatePartEditor(PartDefinition part)
    {
        PartMedalCompatibilityEntry.Text = part.MedalCompatibility.ToString();
        PartSpecialityEntry.Text = part.Speciality.ToString();
        PartGenderEntry.Text = part.Gender.ToString();
        PartArmorEntry.Text = part.Armor.ToString();
        PartUnknown2Entry.Text = part.Unknown2.ToString();
        PartUnknown3Entry.Text = part.Unknown3.ToString();
        PartUnknown4Entry.Text = part.Unknown4.ToString();
        PartUnknown5Entry.Text = part.Unknown5.ToString();
        PartUnknown6Entry.Text = part.Unknown6.ToString();
        PartUnknown7Entry.Text = part.Unknown7.ToString();
        PartUnknown8Entry.Text = part.Unknown8.ToString();
        ResetPartEditorLabels(part.Kind);

        if (part.IsLegPart)
        {
            var stats = part.AsLegPartStats();
            PartValue1Entry.Text = stats.LegType.ToString();
            PartValue2Entry.Text = stats.Propulsion.ToString();
            PartValue3Entry.Text = stats.Evasion.ToString();
            PartValue4Entry.Text = stats.Defense.ToString();
            PartValue5Entry.Text = stats.Conceal.ToString();
            return;
        }

        var combat = part.AsCombatPartStats();
        PartValue1Entry.Text = combat.Technique.ToString();
        PartValue2Entry.Text = combat.Success.ToString();
        PartValue3Entry.Text = combat.Power.ToString();
        PartValue4Entry.Text = combat.ChargeOrChainReaction.ToString();
        PartValue5Entry.Text = combat.Uses.ToString();
    }

    private void ResetPartEditorLabels(PartKind kind)
    {
        if (kind == PartKind.Legs)
        {
            PartValue1Label.Text = "Leg Type";
            PartValue2Label.Text = "Propulsion";
            PartValue3Label.Text = "Evasion";
            PartValue4Label.Text = "Defense";
            PartValue5Label.Text = "Conceal";
            return;
        }

        PartValue1Label.Text = "Technique";
        PartValue2Label.Text = "Success";
        PartValue3Label.Text = "Power";
        PartValue4Label.Text = "Charge / CR";
        PartValue5Label.Text = "Uses";
    }

    private PartDefinition BuildPartFromEditor(PartDefinition original)
    {
        return new PartDefinition(original.Id, original.MedabotId, original.Kind, original.DataOffset, ParseByte(PartMedalCompatibilityEntry.Text, "Medal compatibility"), ParseByte(PartValue1Entry.Text, PartValue1Label.Text), ParseByte(PartSpecialityEntry.Text, "Speciality"), ParseByte(PartGenderEntry.Text, "Gender"), ParseByte(PartArmorEntry.Text, "Armor"), ParseByte(PartValue2Entry.Text, PartValue2Label.Text), ParseByte(PartValue3Entry.Text, PartValue3Label.Text), ParseByte(PartValue4Entry.Text, PartValue4Label.Text), ParseByte(PartValue5Entry.Text, PartValue5Label.Text), ParseByte(PartUnknown2Entry.Text, "Unknown2"), ParseByte(PartUnknown3Entry.Text, "Unknown3"), ParseByte(PartUnknown4Entry.Text, "Unknown4"), ParseByte(PartUnknown5Entry.Text, "Unknown5"), ParseByte(PartUnknown6Entry.Text, "Unknown6"), ParseByte(PartUnknown7Entry.Text, "Unknown7"), ParseByte(PartUnknown8Entry.Text, "Unknown8"));
    }

    private void PopulateStarterEditor(StarterDefinition starter)
    {
        StarterPartEntry.Text = starter.PartId.ToString();
        StarterMedalEntry.Text = starter.MedalId.ToString();
        StarterIsFemaleSwitch.IsChecked = starter.IsFemale;
    }

    private void PopulateEncounterEditor(EncounterDefinition encounter)
    {
        EncounterBattle1Entry.Text = encounter.Battle1.ToString();
        EncounterBattle2Entry.Text = encounter.Battle2.ToString();
        EncounterBattle3Entry.Text = encounter.Battle3.ToString();
        EncounterBattle4Entry.Text = encounter.Battle4.ToString();
    }
    private EventVisualState BuildEventVisualState(short eventId, EventScript script)
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

    private static string FormatBattle(BattleDefinition battle, MedabotsMetadata metadata)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Battle {battle.Id} @ 0x{battle.DataOffset:X}");
        builder.AppendLine($"Character: {metadata.GetCharacterName(battle.CharacterId)} ({battle.CharacterId})");
        builder.AppendLine($"NumberOfBots: {battle.NumberOfBots}");
        for (var index = 0; index < battle.Bots.Count; index++)
        {
            var bot = battle.Bots[index];
            builder.AppendLine($"Bot {index + 1}: head={metadata.GetPartName(bot.HeadPartId)} ({bot.HeadPartId}), right={metadata.GetPartName(bot.RightArmPartId)} ({bot.RightArmPartId}), left={metadata.GetPartName(bot.LeftArmPartId)} ({bot.LeftArmPartId}), legs={metadata.GetPartName(bot.LegsPartId)} ({bot.LegsPartId}), medal={metadata.GetMedalName(bot.MedalId)} ({bot.MedalId}), level={bot.MedalLevel}");
        }

        return builder.ToString().TrimEnd();
    }

    private static string FormatPart(PartDefinition part, MedabotsMetadata metadata)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Part {part.Id} - {metadata.GetPartName(part.Id)} ({part.Kind}) @ 0x{part.DataOffset:X}");
        builder.AppendLine($"Medabot: {metadata.GetBotName(part.MedabotId)} ({part.MedabotId})");
        builder.AppendLine($"MedalCompatibility: {metadata.GetMedalName(part.MedalCompatibility)} ({part.MedalCompatibility})");
        builder.AppendLine($"Speciality: {metadata.GetSpecialityName(part.Speciality)} ({part.Speciality})");
        builder.AppendLine($"Gender: {part.Gender}");
        builder.AppendLine($"Armor: {part.Armor}");
        AppendPartKindSpecificFields(builder, part, metadata);
        return builder.ToString().TrimEnd();
    }

    private static string FormatStarter(StarterDefinition starter, MedabotsMetadata metadata)
    {
        return $"Starter part: {metadata.GetPartName(starter.PartId)} ({starter.PartId}){Environment.NewLine}Starter medal: {metadata.GetMedalName(starter.MedalId)} ({starter.MedalId}){Environment.NewLine}Female: {starter.IsFemale}";
    }

    private static string FormatEncounter(EncounterDefinition encounter)
    {
        return $"Encounter {encounter.Id} @ 0x{encounter.DataOffset:X}{Environment.NewLine}Battles: {encounter.Battle1}, {encounter.Battle2}, {encounter.Battle3}, {encounter.Battle4}";
    }

    private static void AppendPartKindSpecificFields(StringBuilder builder, PartDefinition part, MedabotsMetadata metadata)
    {
        if (part.IsLegPart)
        {
            var stats = part.AsLegPartStats();
            builder.AppendLine($"LegType: {stats.LegType}");
            builder.AppendLine($"Propulsion: {stats.Propulsion}");
            builder.AppendLine($"Evasion: {stats.Evasion}");
            builder.AppendLine($"Defense: {stats.Defense}");
            builder.AppendLine($"Conceal: {stats.Conceal}");
            return;
        }

        var combat = part.AsCombatPartStats();
        builder.AppendLine($"Technique: {metadata.GetTechniqueName(combat.Technique)} ({combat.Technique})");
        builder.AppendLine($"Success: {combat.Success}");
        builder.AppendLine($"Power: {combat.Power}");
        builder.AppendLine($"ChargeOrChainReaction: {combat.ChargeOrChainReaction}");
        builder.AppendLine($"Uses: {combat.Uses}");
    }

    private string FormatPartActionAnalysis(PartDefinition part)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Part {part.Id} - {_metadata.GetPartName(part.Id)}");

        if (part.IsLegPart)
        {
            builder.AppendLine("Leg parts do not use battle action script routes.");
            builder.AppendLine($"Leg type: {part.AsLegPartStats().LegType}");
            return builder.ToString().TrimEnd();
        }

        var actionId = part.AsCombatPartStats().Technique;
        var actionName = _metadata.GetTechniqueName(actionId);
        var analysis = _battleActionRegistry.Analyze(actionId, actionName, _loadedBattleActionOpcodes, _loadedBattleActionScripts);

        builder.AppendLine($"Technique: {analysis.ActionName} (0x{analysis.ActionId:X2})");

        if (analysis.Route is null && analysis.Script is null)
        {
            builder.AppendLine("No documented action route or parsed script yet.");
            return builder.ToString().TrimEnd();
        }

        if (analysis.Script is not null)
        {
            builder.AppendLine($"Script id: 0x{analysis.Script.ActionScriptId:X2}");
            builder.AppendLine($"Script @ 0x{analysis.Script.ScriptRomAddress:X8} (file 0x{analysis.Script.ScriptOffset:X})");
            builder.AppendLine($"Script length: 0x{analysis.Script.ScriptLength:X}");
            builder.AppendLine();
            builder.AppendLine("Actual action script:");

            foreach (var node in analysis.ScriptNodes)
            {
                if (node.IsLabel)
                {
                    builder.AppendLine($"  +0x{node.RelativeOffset:X2}  {node.DisplayName}");
                    continue;
                }

                builder.AppendLine($"  +0x{node.RelativeOffset:X2}  0x{node.Value:X2}  {node.DisplayName}");
                if (node.InlineArguments.Count > 0)
                {
                    builder.AppendLine($"    args: {string.Join(' ', node.InlineArguments.Select(value => $"0x{value:X2}"))}");
                }

                builder.AppendLine($"    {node.Summary}");
                if (node.HandlerRomAddress != 0)
                {
                    builder.AppendLine($"    handler @ 0x{node.HandlerRomAddress:X8} (file 0x{node.HandlerOffset:X})");
                }
            }
        }

        if (analysis.Route is null)
        {
            return builder.ToString().TrimEnd();
        }

        builder.AppendLine();
        builder.AppendLine("Documented route notes:");
        builder.AppendLine($"Family handler: {analysis.Route.FamilyHandler}");
        builder.AppendLine($"Family summary: {analysis.Route.FamilySummary}");

        if (!string.IsNullOrWhiteSpace(analysis.Route.FamilySubsequence))
        {
            builder.AppendLine($"Family subsequence: {analysis.Route.FamilySubsequence}");
        }

        if (!string.IsNullOrWhiteSpace(analysis.Route.SharedScriptName))
        {
            builder.AppendLine($"Shared script: {analysis.Route.SharedScriptName}");
        }

        if (!string.IsNullOrWhiteSpace(analysis.Route.SharedScriptSummary))
        {
            builder.AppendLine($"Script summary: {analysis.Route.SharedScriptSummary}");
        }

        builder.AppendLine();
        builder.AppendLine("Curated opcode summary:");

        if (analysis.Opcodes.Count == 0)
        {
            builder.AppendLine("  No curated opcode summary yet.");
        }
        else
        {
            foreach (var opcode in analysis.Opcodes)
            {
                builder.AppendLine($"  0x{opcode.Opcode:X2}  {opcode.Name}");
                builder.AppendLine($"    {opcode.HandlerName}");
                builder.AppendLine($"    inline args: {opcode.InlineArgumentCount}");
                builder.AppendLine($"    {opcode.Summary}");
                if (opcode.HandlerRomAddress != 0)
                {
                    builder.AppendLine($"    handler @ 0x{opcode.HandlerRomAddress:X8} (file 0x{opcode.HandlerOffset:X})");
                }
            }
        }

        if (analysis.Route.ActualFlow.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Actual flow:");
            foreach (var step in analysis.Route.ActualFlow)
            {
                builder.AppendLine($"  - {step}");
            }
        }

        if (analysis.Route.Notes.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Notes:");
            foreach (var note in analysis.Route.Notes)
            {
                builder.AppendLine($"  - {note}");
            }
        }

        return builder.ToString().TrimEnd();
    }

    private void ApplyEventVisualState(EventVisualState visualState)
    {
        _selectedEventVisualState = visualState;
        RefreshCollection(_visibleEventInstructions, visualState.Instructions);
        EventKnownLabelsLabel.Text = visualState.OrderedLabels.Count == 0
            ? "Labels: none"
            : $"Labels: {string.Join(", ", visualState.OrderedLabels.Take(8))}{(visualState.OrderedLabels.Count > 8 ? ", ..." : string.Empty)}";
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

        if (_eventCustomLabels.TryGetValue(eventId, out var customLabels))
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
        var messageText = _loadedMessages.TryGetValue(messageId, out var text) ? text : "<missing message>";
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
            EventArgumentType.Bot => $"{_metadata.GetBotName(argument.RawValue)} ({argument.RawValue})",
            EventArgumentType.Medal => $"{_metadata.GetMedalName(argument.RawValue)} ({argument.RawValue})",
            EventArgumentType.Music => $"{_metadata.GetSongName(argument.RawValue)} ({argument.RawValue})",
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

    private static bool IsConditionalJumpInstruction(EventInstruction instruction)
    {
        return string.Equals(instruction.Name, "Yes_or_No_Box", StringComparison.Ordinal)
            || string.Equals(instruction.Name, "No_or_Yes_Box", StringComparison.Ordinal)
            || instruction.Name.StartsWith("Jump_If_", StringComparison.Ordinal);
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

    private string BuildJumpArgumentHelpText(EventInstructionItem instructionItem, EventArgumentValue argument)
    {
        var targetOffset = instructionItem.Offset + argument.RawValue + 1;
        var targetLabel = _selectedEventVisualState?.LabelMap.TryGetValue(targetOffset, out var label) == true
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

    private static int ParseConditionalBranchIndex(string argumentName)
    {
        if (!argumentName.StartsWith("jump", StringComparison.OrdinalIgnoreCase))
        {
            return -1;
        }

        return int.TryParse(argumentName["jump".Length..], out var oneBasedIndex) ? oneBasedIndex - 1 : -1;
    }

    private static EventOperationDefinition? ResolveEditorOperationDefinition(EventInstruction? instruction)
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

    private static int ResolveJumpArgumentValue(EventArgumentEditorItem argument, int sourceOffset, IReadOnlyDictionary<int, string>? labelMap)
    {
        var rawText = argument.ValueText?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(rawText) && !LooksLikeNumericValue(rawText))
        {
            if (labelMap is not null)
            {
                foreach (var pair in labelMap)
                {
                    if (string.Equals(pair.Value, rawText, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals($"{pair.Value} (0x{pair.Key:X})", rawText, StringComparison.OrdinalIgnoreCase))
                    {
                        return pair.Key - sourceOffset - 1;
                    }
                }
            }
        }

        return argument.GetEditedValue();
    }

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
        if (sender is MenuItem menuItem && menuItem.CommandParameter is EventInstructionItem item && item.Instruction is not null)
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

    private static string FormatBytes(IEnumerable<byte> bytes) => string.Join(" ", bytes.Select(value => value.ToString("X2")));
    private static int ParseIntOrDefault(string? text, int fallback) => int.TryParse(text, out var value) && value >= 0 ? value : fallback;

    private static byte[] ParseBytes(string? text)
    {
        var tokens = (text ?? string.Empty).Split([' ', '\r', '\n', '\t', ',', ';', '-'], StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            throw new InvalidOperationException("Enter at least one byte.");
        }

        return tokens.Select(token =>
        {
            if (byte.TryParse(token, System.Globalization.NumberStyles.HexNumber, null, out var hex))
            {
                return hex;
            }

            if (byte.TryParse(token, out var dec))
            {
                return dec;
            }

            throw new InvalidOperationException($"'{token}' is not a valid byte value.");
        }).ToArray();
    }

    private static int ParseInt(string? text, string fieldName)
    {
        if (!int.TryParse(text, out var value) || value < 0)
        {
            throw new InvalidOperationException($"{fieldName} must be a non-negative integer.");
        }

        return value;
    }

    private static byte ParseByte(string? text, string fieldName)
    {
        if (!byte.TryParse(text, out var value))
        {
            throw new InvalidOperationException($"{fieldName} must be a byte value between 0 and 255.");
        }

        return value;
    }

    private Task DisplayAlertAsync(string title, string message, string buttonText)
    {
        MessageBox.Show(this, message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        return Task.CompletedTask;
    }

    private Task<string?> DisplayPromptAsync(string title, string message, string accept, string cancel, string? placeholder, string? initialValue)
    {
        var dialog = new InputDialog(title, message, accept, cancel, placeholder, initialValue)
        {
            Owner = this
        };

        return Task.FromResult(dialog.ShowDialog() == true ? dialog.ResponseText : null);
    }

    private static string? PickOpenFilePath(string title, string filter)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = filter,
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private static string? PickSaveFilePath(string title, string filter, string initialPath)
    {
        var dialog = new SaveFileDialog
        {
            Title = title,
            Filter = filter,
            FileName = Path.GetFileName(initialPath),
            InitialDirectory = Path.GetDirectoryName(initialPath)
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
