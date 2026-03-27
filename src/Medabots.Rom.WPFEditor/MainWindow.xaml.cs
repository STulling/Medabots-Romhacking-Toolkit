
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
    private readonly Dictionary<(int MedabotId, int ComponentIndex), BattleCompositeSpriteComponentAsset> _editedBattleCompositeComponentAssets = [];
    private readonly Dictionary<(int PartId, int ComponentIndex), BattleCompositeSpriteComponentAsset> _battleCompositeComponentCache = [];
    private readonly Dictionary<(int PartId, int VariantSelector), LargePartDisplayAsset> _largePartDisplayAssetCache = [];
    private readonly Dictionary<(int PartId, int VariantSelector), LargePartDisplayAsset> _editedLargePartDisplayAssets = [];
    private readonly Dictionary<string, SpriteEditHistory> _spriteEditHistories = [];
    private readonly Dictionary<(int EntryLength, int ShopId), ShopDefinition> _shopCache = [];
    private readonly List<EventOperationOption> _eventOperationOptions = [];
    private readonly List<SpritePaletteFamilyOption> _spritePaletteFamilyOptions = [];

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
    private bool _isPanningSpritePreview;
    private bool _isUpdatingSpritePaletteFamilyUi;
    private WpfPoint _spritePanStartPoint;
    private double _spritePanStartHorizontalOffset;
    private double _spritePanStartVerticalOffset;
    private int _spriteEditorZoom = 4;
    private const double SpriteViewportPadding = 160d;
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
        SpritePaletteFamilyComboBox.SelectedValuePath = nameof(SpritePaletteFamilyOption.Value);
        RefreshSpritePaletteFamilyOptions();
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
            _session.ApplyPatches(_project.PendingActions);
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
        _battleCompositeComponentCache.Clear();
        _largePartDisplayAssetCache.Clear();
        _editedOverworldSpriteAssets.Clear();
        _editedPortraitAssets.Clear();
        _editedBattleCompositeComponentAssets.Clear();
        _editedLargePartDisplayAssets.Clear();
        _spriteEditHistories.Clear();
        _selectedSpriteNode = null;
        RefreshSpritePaletteFamilyOptions();

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

        _project.PendingActions.Clear();
        if (_session is not null)
        {
            foreach (var action in _session.AppliedActions)
            {
                _project.PendingActions.Add(new RomPatchAction(action.Offset, action.Data.ToArray(), action.Description));
            }
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

    private static void PopulateBattleBotEntries(BattleBot bot, WpfTextBox head, WpfTextBox right, WpfTextBox left, WpfTextBox legs, WpfTextBox medal, WpfTextBox level)
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

    private static BattleBot BuildBattleBot(BattleBot original, WpfTextBox head, WpfTextBox right, WpfTextBox left, WpfTextBox legs, WpfTextBox medal, WpfTextBox level)
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
        WpfMessageBox.Show(this, message, title, MessageBoxButton.OK, MessageBoxImage.Information);
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
        var dialog = new Win32OpenFileDialog
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
        var dialog = new Win32SaveFileDialog
        {
            Title = title,
            Filter = filter,
            FileName = Path.GetFileName(initialPath),
            InitialDirectory = Path.GetDirectoryName(initialPath)
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
