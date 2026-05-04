
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
using WpfTextBlock = System.Windows.Controls.TextBlock;
using WpfCursors = System.Windows.Input.Cursors;
using WpfMessageBox = System.Windows.MessageBox;
using Win32OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using Win32SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using Medabots.Rom.Battles;
using Medabots.Rom.Editor;
using Medabots.Rom.Encounters;
using Medabots.Rom.Events;
using Medabots.Rom.Images;
using Medabots.Rom.Maps;
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
    private readonly ObservableCollection<BrowserItem> _visibleMapItems = [];
    private readonly ObservableCollection<SpriteBrowserNode> _visibleSpriteNodes = [];
    private readonly ObservableCollection<BrowserItem> _visibleEncounterItems = [];
    private readonly ObservableCollection<BrowserItem> _visibleShopItems = [];
    private readonly ObservableCollection<BrowserItem> _visibleChangeItems = [];

    private readonly List<MessagePatchItem> _allPatchItems = [];
    private readonly List<EventBrowserItem> _allEventItems = [];
    private readonly List<BrowserItem> _allBattleItems = [];
    private readonly List<BrowserItem> _allPartItems = [];
    private readonly List<BrowserItem> _allMapItems = [];
    private readonly List<SpriteBrowserNode> _allSpriteNodes = [];
    private readonly List<BrowserItem> _allEncounterItems = [];
    private readonly List<BrowserItem> _allShopItems = [];
    private readonly List<BrowserItem> _allChangeItems = [];

    private readonly RomHackProjectApplicator _projectApplicator = new();
    private readonly MedabotsMessageTableReader _messageTableReader = new();
    private readonly EventScriptReader _eventScriptReader = new();
    private readonly EventInstructionPatcher _eventInstructionPatcher = new();
    private readonly EventScriptRewriter _eventScriptRewriter = new();
    private readonly BattleTableReader _battleTableReader = new();
    private readonly BattleActionOpcodeTableReader _battleActionOpcodeTableReader = new();
    private readonly BattleActionScriptTableReader _battleActionScriptTableReader = new();
    private readonly BattleProjectEditor _battleProjectEditor = new();
    private readonly BattleActionRegistry _battleActionRegistry = BattleActionRegistry.LoadDefault();
    private readonly PartTableReader _partTableReader = new();
    private readonly PartProjectEditor _partProjectEditor = new();
    private readonly ImageAssetRepository _imageAssetRepository = new();
    private readonly ImageAssetPatcher _imageAssetPatcher = new();
    private readonly MapOverlayRepository _mapOverlayRepository = new();
    private readonly MapLayerProjectEditor _mapLayerProjectEditor = new();
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
    private readonly Dictionary<int, Medabots.Rom.Maps.MapTilesetAsset> _mapTilesetCache = [];
    private readonly Dictionary<int, MapOverlayAsset> _mapOverlayCache = [];
    private readonly Dictionary<string, SpriteEditHistory> _spriteEditHistories = [];
    private readonly Dictionary<(int EntryLength, int ShopId), ShopDefinition> _shopCache = [];
    private readonly Dictionary<int, BattleDefinition> _sourceBattleDefinitions = [];
    private readonly Dictionary<int, PartDefinition> _sourcePartDefinitions = [];
    private readonly List<EventOperationOption> _eventOperationOptions = [];
    private readonly List<SpritePaletteFamilyOption> _spritePaletteFamilyOptions = [];
    private readonly List<BrowserItem> _partMedalOptions = [];
    private readonly List<BrowserItem> _partSpecialityOptions = [];
    private readonly List<BrowserItem> _partTechniqueOptions = [];
    private readonly List<BrowserItem> _partGenderOptions = [];
    private readonly List<BrowserItem> _partLegTypeOptions = [];
    private readonly List<BrowserItem> _battleCycleEntryOptions = [];
    private readonly List<BattleLoadoutOption> _battleHeadOptions = [];
    private readonly List<BattleLoadoutOption> _battleRightOptions = [];
    private readonly List<BattleLoadoutOption> _battleLeftOptions = [];
    private readonly List<BattleLoadoutOption> _battleLegsOptions = [];
    private readonly List<BrowserItem> _battleMedalOptions = [];
    private readonly List<BrowserItem> _battleLevelOptions = [];
    private readonly List<BrowserItem> _botBattleFacingOptions = [];

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
    private bool _isWindowFullyInitialized;
    private int _selectedBotBattleFacing;
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
    private Medabots.Rom.Maps.MapTilesetAsset? _loadedMapTileset;
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
        MapCollectionView.ItemsSource = _visibleMapItems;
        SpriteTreeView.ItemsSource = _visibleSpriteNodes;
        EncounterCollectionView.ItemsSource = _visibleEncounterItems;
        ShopCollectionView.ItemsSource = _visibleShopItems;
        ChangesCollectionView.ItemsSource = _visibleChangeItems;
        SpritePaletteFamilyComboBox.SelectedValuePath = nameof(SpritePaletteFamilyOption.Value);
        RefreshSpritePaletteFamilyOptions();
        _partMedalOptions.AddRange(Enumerable.Range(0, 27).Select(id => new BrowserItem(id, $"{id:D3}  {_metadata.GetPartAttributeName(id)}")));
        _partSpecialityOptions.AddRange(Enumerable.Range(0, _metadata.Catalog.Specialities.Count).Select(id => new BrowserItem(id, $"{id:D3}  {_metadata.GetSpecialityName(id)}")));
        _partTechniqueOptions.AddRange(Enumerable.Range(0, _metadata.Catalog.Techniques.Count).Select(id => new BrowserItem(id, $"{id:D3}  {_metadata.GetTechniqueName(id)}")));
        _partGenderOptions.Add(new BrowserItem(0, "000  Male / Default"));
        _partGenderOptions.Add(new BrowserItem(1, "001  Female / Alternate"));
        _battleCycleEntryOptions.AddRange(Enumerable.Range(0, 16).Select(value => new BrowserItem(value, $"{value:D2}  {GetBattleCycleEntryName((byte)value)}")));
        _battleMedalOptions.AddRange(Enumerable.Range(0, _metadata.Catalog.Medals.Count).Select(id => new BrowserItem(id, $"{id:D3}  {_metadata.GetMedalName(id)}")));
        _battleLevelOptions.AddRange(Enumerable.Range(0, 101).Select(level => new BrowserItem(level, $"Level {level:D3}")));
        _botBattleFacingOptions.Add(new BrowserItem(0, "Facing Right / Default"));
        _botBattleFacingOptions.Add(new BrowserItem(1, "Facing Left / Mirrored"));
        PartMedalCompatibilityComboBox.ItemsSource = _partMedalOptions;
        PartSpecialityComboBox.ItemsSource = _partSpecialityOptions;
        PartTechniqueComboBox.ItemsSource = _partTechniqueOptions;
        PartGenderComboBox.ItemsSource = _partGenderOptions;
        PartLegTypeComboBox.ItemsSource = _partLegTypeOptions;
        BotBattleFacingComboBox.ItemsSource = _botBattleFacingOptions;
        InitializeBattleCycleComboBoxes();
        InitializeBattleLoadoutComboBoxes();
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
        _isWindowFullyInitialized = true;
    }

    private void OnExitClicked(object? sender, RoutedEventArgs e) => Close();

    private void OnMessagesTabClicked(object? sender, EventArgs e) => SetActiveSection("Messages");
    private void OnEventsTabClicked(object? sender, EventArgs e) => SetActiveSection("Events");
    private void OnBattlesTabClicked(object? sender, EventArgs e) => SetActiveSection("Battles");
    private void OnPartsTabClicked(object? sender, EventArgs e) => SetActiveSection("Parts");
    private void OnMapsTabClicked(object? sender, EventArgs e) => SetActiveSection("Maps");
    private void OnSpritesTabClicked(object? sender, EventArgs e) => SetActiveSection("Sprites");
    private void OnEncountersTabClicked(object? sender, EventArgs e) => SetActiveSection("Encounters");
    private void OnShopsTabClicked(object? sender, EventArgs e) => SetActiveSection("Shops");
    private void OnStarterTabClicked(object? sender, EventArgs e) => SetActiveSection("Starter");
    private void OnChangesTabClicked(object? sender, EventArgs e) => SetActiveSection("Changes");

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
            var projectSession = await OpenCompatibleProjectSessionAsync(loadedProject);
            if (projectSession is null)
            {
                return;
            }

            loadedProject.ProjectFilePath = projectPath;

            _session = projectSession.Value.Session;
            _project = loadedProject;
            _project.SourceRomPath = projectSession.Value.RomPath;
            _project.TextProfileId = projectSession.Value.Profile.Id;
            PrepareProjectForEditing();
            _session.ApplyPatches(_project.PendingActions);
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
        _sourceBattleDefinitions.Clear();
        _sourcePartDefinitions.Clear();

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
        foreach (var battle in _loadedBattles)
        {
            _sourceBattleDefinitions[battle.Id] = battle;
        }

        foreach (var part in _loadedParts)
        {
            _sourcePartDefinitions[part.Id] = part;
        }

        _loadedBattles = OverlayProjectBattleEdits(_loadedBattles);
        _loadedParts = OverlayProjectPartEdits(_loadedParts);
        RefreshPartLegTypeOptions(_loadedParts);
        RefreshBattleLoadoutOptions();
        _loadedEncounters = _encounterTableReader.ReadAll(_session.RomFile);
        _loadedPart = null;
        _loadedMapTileset = null;
        _spritePreviewCache.Clear();
        _battleCompositeComponentCache.Clear();
        _largePartDisplayAssetCache.Clear();
        _mapTilesetCache.Clear();
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
        _allMapItems.Clear();
        _allMapItems.AddRange(Enumerable.Range(0, Math.Min(MedabotsRomSchema.MapCount, _metadata.Catalog.Maps.Count)).Select(mapId => new BrowserItem(mapId, $"{mapId:D3}  {_metadata.GetMapName(mapId)}")));
        _allSpriteNodes.Clear();
        _allSpriteNodes.AddRange(BuildSpriteTreeNodes());
        _allEncounterItems.Clear();
        _allEncounterItems.AddRange(_loadedEncounters.Select(encounter => new BrowserItem(encounter.Id, $"{encounter.Id:D3}  Battles {encounter.Battle1}/{encounter.Battle2}/{encounter.Battle3}/{encounter.Battle4}")));
        RebuildEventBrowserItems(profile);
        UpdateEventBrowserPatchStatuses();

        RefreshMessageFilter();
        RefreshBattleFilter();
        RefreshPartFilter();
        RefreshMapFilter();
        RefreshSpriteFilter();
        RefreshEncounterFilter();
        RefreshEventFilter();
        LoadShopList();
        PartCollectionView.SelectedItem = null;
        ClearPartEditor();
        MapCollectionView.SelectedItem = null;
        ClearMapPreview();
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
    private void OnMapFilterChanged(object? sender, TextChangedEventArgs e) => RefreshMapFilter();
    private void OnSpriteFilterChanged(object? sender, TextChangedEventArgs e) => RefreshSpriteFilter();
    private void OnEncounterFilterChanged(object? sender, TextChangedEventArgs e) => RefreshEncounterFilter();
    private void OnShopFilterChanged(object? sender, TextChangedEventArgs e) => RefreshShopFilter();

    private void RefreshMessageFilter() => RefreshCollection(_visiblePatchItems, _allPatchItems.Where(item => MatchesFilter($"{item.DisplayName} {item.Preview} {item.OriginalText}", MessageFilterEntry.Text)));
    private void RefreshEventFilter() => RefreshCollection(_visibleEventItems, _allEventItems.Where(item => MatchesEventFilter(item, EventFilterEntry.Text)));
    private void RefreshBattleFilter() => RefreshCollection(_visibleBattleItems, _allBattleItems.Where(item => MatchesFilter(item.FilterText, BattleFilterEntry.Text)));
    private void RefreshPartFilter() => RefreshCollection(_visiblePartItems, _allPartItems.Where(item => MatchesFilter(item.FilterText, PartFilterEntry.Text)));
    private void RefreshMapFilter() => RefreshCollection(_visibleMapItems, _allMapItems.Where(item => MatchesFilter(item.FilterText, MapFilterEntry.Text)));
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

    private static bool MatchesEventFilter(EventBrowserItem item, string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return true;
        }

        var tokens = filter.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var token in tokens)
        {
            if (token.StartsWith("op:", StringComparison.OrdinalIgnoreCase) ||
                token.StartsWith("opcode:", StringComparison.OrdinalIgnoreCase))
            {
                var separatorIndex = token.IndexOf(':');
                var opcodeQuery = separatorIndex >= 0 ? token[(separatorIndex + 1)..].Trim() : string.Empty;
                if (!MatchesEventOpcodeQuery(item, opcodeQuery))
                {
                    return false;
                }

                continue;
            }

            if (!item.FilterText.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static bool MatchesEventOpcodeQuery(EventBrowserItem item, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        query = query.Trim();
        if (item.OpcodeFilterText.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (TryParseByteQuery(query, out var opcode))
        {
            return item.OpcodeFilterText.Contains($"0x{opcode:X2}", StringComparison.OrdinalIgnoreCase) ||
                   item.OpcodeFilterText.Contains($" {opcode} ", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static bool TryParseByteQuery(string text, out byte value)
    {
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return byte.TryParse(text[2..], System.Globalization.NumberStyles.HexNumber, null, out value);
        }

        if (byte.TryParse(text, out value))
        {
            return true;
        }

        if (text.Length <= 2)
        {
            return byte.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out value);
        }

        return false;
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
        if (_loadedBattle is null)
        {
            await DisplayAlertAsync("No Battle Loaded", "Select a battle first.", "OK");
            return;
        }

        try
        {
            var updated = BuildBattleFromEditor(_loadedBattle);
            var source = GetSourceBattleDefinition(updated.Id);
            var staged = _battleProjectEditor.StageBattle(_project, source, updated);
            var effective = staged ?? source;
            _loadedBattles = _loadedBattles.Select(battle => battle.Id == effective.Id ? effective : battle).ToArray();
            _loadedBattle = effective;
            BattleEditor.Text = FormatBattle(effective);
            UpdateStatus();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Battle Update Failed", ex.Message, "OK");
        }
    }

    private async void OnApplyPartClicked(object? sender, RoutedEventArgs e)
    {
        if (_loadedPart is null)
        {
            await DisplayAlertAsync("No Part Loaded", "Select a part first.", "OK");
            return;
        }

        try
        {
            var updated = BuildPartFromEditor(_loadedPart);
            var source = GetSourcePartDefinition(updated.Id);
            var staged = _partProjectEditor.StagePart(_project, source, updated);
            var effective = staged ?? source;
            _loadedParts = _loadedParts.Select(part => part.Id == effective.Id ? effective : part).ToArray();
            _loadedPart = effective;
            PopulatePartEditor(effective);
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
        PatchCountLabel.Text = $"Staged messages: {stagedMessagePatchCount} | Staged events: {_eventProjectScriptPatches.Count} | Pending patch actions: {_project.PendingActions.Count}";
        FooterProjectLabel.Text = $"Project: {projectDisplayName}";
        FooterChangesLabel.Text = $"Staged changes: messages {stagedMessagePatchCount}, events {_eventProjectScriptPatches.Count}, pending patch actions {_project.PendingActions.Count}";
        FooterPathLabel.Text = romFileName;
        OpenRomCommandButton.IsEnabled = _session is null;
        OpenProjectCommandButton.IsEnabled = true;
        SaveProjectCommandButton.IsEnabled = true;
        ExportRomCommandButton.IsEnabled = _session is not null;
        RefreshChangesView(stagedMessagePatchCount);
    }

    private void RefreshChangesView(int stagedMessagePatchCount)
    {
        _allChangeItems.Clear();
        AddCompiledProjectChanges();
        AddDraftChanges();

        _visibleChangeItems.Clear();
        foreach (var item in _allChangeItems)
        {
            _visibleChangeItems.Add(item);
        }

        var totalCount = _allChangeItems.Count;
        ChangesSummaryLabel.Text = $"Total staged changes: {totalCount}{Environment.NewLine}Messages: {stagedMessagePatchCount}  |  Event scripts: {_project.EventScriptPatches.Count}  |  Battles: {_project.BattleEdits.Count}  |  Parts: {_project.PartEdits.Count}  |  Map metadata: {_project.MapMusicPatches.Count + _project.MapEncounterPatches.Count + _project.MapEncounterStatePatches.Count + _project.MapEventObjectResourcePatches.Count}  |  Map overlays/layers: {_project.MapEntitySpawnPatches.Count + _project.MapWarpPatches.Count + _project.MapCollisionPatches.Count + _project.MapLayerPatches.Count}  |  Staged sprites: {_project.OverworldSpriteEdits.Count + _project.PortraitEdits.Count + _project.BattleCompositeSpriteEdits.Count + _project.LargePartDisplayEdits.Count}";
    }

    private void AddCompiledProjectChanges()
    {
        if (_session is null)
        {
            foreach (var system in _projectApplicator.Systems)
            {
                foreach (var value in system.DescribeChanges(_project))
                {
                    _allChangeItems.Add(new BrowserItem(_allChangeItems.Count, $"{system.DisplayName}: {value}"));
                }
            }

            return;
        }

        foreach (var change in _projectApplicator.BuildChanges(_project, _session.RomFile))
        {
            _allChangeItems.Add(new BrowserItem(_allChangeItems.Count, $"{change.Owner}: {change.Description}"));
            foreach (var action in change.Actions)
            {
                _allChangeItems.Add(new BrowserItem(_allChangeItems.Count, $"  0x{action.Offset:X6} ({action.Data.Length} bytes) {action.Description}"));
            }
        }
    }

    private void AddDraftChanges()
    {
        foreach (var id in _project.SplitLargeDisplayPartIds.Select(id => $"Part {id:D3}"))
        {
            _allChangeItems.Add(new BrowserItem(_allChangeItems.Count, $"Large Display Split: {id}"));
        }

        foreach (var id in _editedOverworldSpriteAssets.Keys.Where(id => !_project.OverworldSpriteEdits.Any(asset => asset.SpriteId == id)).Select(id => $"Overworld {id:D3}"))
        {
            _allChangeItems.Add(new BrowserItem(_allChangeItems.Count, $"Sprite Draft: {id}"));
        }

        foreach (var key in _editedPortraitAssets.Keys.Where(key => !_project.PortraitEdits.Any(asset => asset.CharacterId == key.CharacterId && asset.PortraitIndex == key.PortraitIndex)).Select(key => $"Portrait {key.CharacterId:D3}:{key.PortraitIndex:D2}"))
        {
            _allChangeItems.Add(new BrowserItem(_allChangeItems.Count, $"Sprite Draft: {key}"));
        }

        foreach (var key in _editedBattleCompositeComponentAssets.Keys.Where(key => !_project.BattleCompositeSpriteEdits.Any(asset => asset.MedabotId == key.MedabotId && asset.ComponentIndex == key.ComponentIndex)).Select(key => $"Battle {key.MedabotId:D3}/{key.ComponentIndex}"))
        {
            _allChangeItems.Add(new BrowserItem(_allChangeItems.Count, $"Sprite Draft: {key}"));
        }

        foreach (var key in _editedLargePartDisplayAssets.Keys.Where(key => !_project.LargePartDisplayEdits.Any(asset => asset.PartId == key.PartId && asset.VariantSelector == key.VariantSelector)).Select(key => $"Large {key.PartId:D3}/{key.VariantSelector}"))
        {
            _allChangeItems.Add(new BrowserItem(_allChangeItems.Count, $"Sprite Draft: {key}"));
        }
    }

    private void RebuildEventBrowserItems(MedabotsRomTextProfile profile)
    {
        _allEventItems.Clear();
        var maxPatchedEventId = _project.EventScriptPatches.Count == 0
            ? -1
            : _project.EventScriptPatches.Max(patch => (int)patch.EventId);
        var totalCount = Math.Max(profile.EventCount, maxPatchedEventId + 1);
        _allEventItems.AddRange(Enumerable.Range(0, totalCount).Select(id => new EventBrowserItem { Id = id }));
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
    }

    private static int GetLargeDisplayComponentIndexForVariant(PartKind kind, int variantSelector) => kind switch
    {
        PartKind.RightArm => variantSelector == 0 ? 1 : 2,
        PartKind.LeftArm => variantSelector == 0 ? 3 : 4,
        PartKind.Head => 0,
        PartKind.Legs => 5,
        _ => 0
    };

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

    private async Task<(RomHackSession Session, string RomPath, MedabotsRomTextProfile Profile)?> OpenCompatibleProjectSessionAsync(RomHackProject project)
    {
        string? candidateRomPath = null;

        while (true)
        {
            candidateRomPath = await ResolveProjectRomPathAsync(candidateRomPath ?? project.SourceRomPath);
            if (string.IsNullOrWhiteSpace(candidateRomPath))
            {
                return null;
            }

            SetLoadingState(true, "Opening ROM...", 0.02);
            var session = await RomHackSession.OpenAsync(candidateRomPath);
            var detectedProfile = MedabotsRomTextProfiles.Detect(session.RomFile);
            if (detectedProfile is null)
            {
                SetLoadingState(false, string.Empty, 0);
                await DisplayAlertAsync("Unsupported ROM", "The selected ROM does not match a supported Medabots ROM profile. Select a compatible ROM.", "OK");
                candidateRomPath = null;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(project.TextProfileId) &&
                !string.Equals(project.TextProfileId, detectedProfile.Id, StringComparison.Ordinal))
            {
                SetLoadingState(false, string.Empty, 0);
                var expectedProfile = MedabotsRomTextProfiles.FindById(project.TextProfileId);
                var expectedName = expectedProfile?.Name ?? project.TextProfileId;
                await DisplayAlertAsync(
                    "Incompatible ROM",
                    $"This project expects ROM profile '{expectedName}', but the selected ROM is '{detectedProfile.Name}'. Select a compatible ROM.",
                    "OK");
                candidateRomPath = null;
                continue;
            }

            return (session, candidateRomPath.Trim(), detectedProfile);
        }
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

    private IReadOnlyList<BattleDefinition> OverlayProjectBattleEdits(IReadOnlyList<BattleDefinition> sourceBattles)
    {
        return sourceBattles
            .Select(battle => ProjectEditCollection.Find(_project, ProjectEditAdapters.Battle, battle.Id) ?? battle)
            .ToArray();
    }

    private IReadOnlyList<PartDefinition> OverlayProjectPartEdits(IReadOnlyList<PartDefinition> sourceParts)
    {
        return sourceParts
            .Select(part => ProjectEditCollection.Find(_project, ProjectEditAdapters.Part, part.Id) ?? part)
            .ToArray();
    }

    private BattleDefinition GetSourceBattleDefinition(int battleId)
    {
        if (_sourceBattleDefinitions.TryGetValue(battleId, out var battle))
        {
            return battle;
        }

        throw new InvalidOperationException($"Could not resolve source battle {battleId}.");
    }

    private PartDefinition GetSourcePartDefinition(int partId)
    {
        if (_sourcePartDefinitions.TryGetValue(partId, out var part))
        {
            return part;
        }

        throw new InvalidOperationException($"Could not resolve source part {partId}.");
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
        PartMedalCompatibilityComboBox.SelectedIndex = -1;
        PartSpecialityComboBox.SelectedIndex = -1;
        PartGenderComboBox.SelectedIndex = -1;
        PartArmorEntry.Text = string.Empty;
        PartValue1Entry.Text = string.Empty;
        PartTechniqueComboBox.SelectedIndex = -1;
        PartLegTypeComboBox.SelectedIndex = -1;
        PartValue2Entry.Text = string.Empty;
        PartValue3Entry.Text = string.Empty;
        PartValue4Entry.Text = string.Empty;
        PartValue4CheckBox.IsChecked = false;
        PartValue5Entry.Text = string.Empty;
        PartUnknown2Entry.Text = string.Empty;
        PartUnknown3Entry.Text = string.Empty;
        PartUnknown4Entry.Text = string.Empty;
        PartUnknown5Entry.Text = string.Empty;
        PartUnknown6Entry.Text = string.Empty;
        PartUnknown7Entry.Text = string.Empty;
        PartUnknown8Entry.Text = string.Empty;
        PartOverviewLabel.Text = "No part selected.";
        PartOverviewHintLabel.Text = "Select a part to inspect its role, stats, and unresolved raw bytes.";
        PartMedalCompatibilityHintLabel.Text = string.Empty;
        PartSpecialityHintLabel.Text = string.Empty;
        PartGenderHintLabel.Text = string.Empty;
        PartArmorHintLabel.Text = string.Empty;
        PartValue1HintLabel.Text = string.Empty;
        PartValue2HintLabel.Text = string.Empty;
        PartValue3HintLabel.Text = string.Empty;
        PartValue4HintLabel.Text = string.Empty;
        PartValue5HintLabel.Text = string.Empty;
        PartUnknown2Label.Text = "Unknown2";
        PartUnknown3Label.Text = "Bot Shared Value";
        PartUnknown2HintLabel.Text = string.Empty;
        PartUnknown3HintLabel.Text = string.Empty;
        PartUnknown4Label.Text = "Unknown4";
        PartUnknown5Label.Text = "Unknown5";
        PartUnknown45HintLabel.Text = string.Empty;
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

    private static string BuildEventOpcodeFilterText(EventScript script)
    {
        if (script.Instructions.Count == 0)
        {
            return string.Empty;
        }

        var uniqueEntries = script.Instructions
            .GroupBy(instruction => instruction.Opcode)
            .Select(group => group.First())
            .OrderBy(instruction => instruction.Opcode)
            .Select(instruction => $"0x{instruction.Opcode:X2} {instruction.Opcode} {instruction.Name}");

        return $" {string.Join(" ", uniqueEntries)} ";
    }

    private void PopulateBattleEditor(BattleDefinition battle)
    {
        BattleOverviewLabel.Text = $"{battle.Id:D3}  Battle";
        BattleOverviewHintLabel.Text = $"Character: {_metadata.GetCharacterName(battle.CharacterId)} ({battle.CharacterId})   Data: 0x{battle.DataOffset:X}   Pointer: 0x{battle.PointerOffset:X}";
        BattleCharacterEntry.Text = battle.CharacterId.ToString();
        BattleBotCountEntry.Text = battle.NumberOfBots.ToString();
        BattleCharacterHintLabel.Text = $"Battle owner / script-facing character: {_metadata.GetCharacterName(battle.CharacterId)} ({battle.CharacterId}).";
        BattleBotCountHintLabel.Text = "Number of active bot slots used by this encounter. The ROM stores three loadout slots total.";
        PopulateBattleBotEntries(battle.Bots[0], BattleBot1HeadEntry, BattleBot1RightEntry, BattleBot1LeftEntry, BattleBot1LegsEntry, BattleBot1MedalEntry, BattleBot1LevelEntry);
        PopulateBattleBotEntries(battle.Bots[1], BattleBot2HeadEntry, BattleBot2RightEntry, BattleBot2LeftEntry, BattleBot2LegsEntry, BattleBot2MedalEntry, BattleBot2LevelEntry);
        PopulateBattleBotEntries(battle.Bots[2], BattleBot3HeadEntry, BattleBot3RightEntry, BattleBot3LeftEntry, BattleBot3LegsEntry, BattleBot3MedalEntry, BattleBot3LevelEntry);
        BattleBot1SummaryLabel.Text = BuildBattleBotSummary(battle.Bots[0].HeadPartId, battle.Bots[0].RightArmPartId, battle.Bots[0].LeftArmPartId, battle.Bots[0].LegsPartId, battle.Bots[0].MedalId, battle.Bots[0].MedalLevel);
        BattleBot2SummaryLabel.Text = BuildBattleBotSummary(battle.Bots[1].HeadPartId, battle.Bots[1].RightArmPartId, battle.Bots[1].LeftArmPartId, battle.Bots[1].LegsPartId, battle.Bots[1].MedalId, battle.Bots[1].MedalLevel);
        BattleBot3SummaryLabel.Text = BuildBattleBotSummary(battle.Bots[2].HeadPartId, battle.Bots[2].RightArmPartId, battle.Bots[2].LeftArmPartId, battle.Bots[2].LegsPartId, battle.Bots[2].MedalId, battle.Bots[2].MedalLevel);
        RefreshBattleDerivedLabels();
        BattleAdvancedHintLabel.Text = "Initialization Mode: 0 = normal battle setup from the current player state, 1 = reuse preinitialized battle-side state. Part Loadout Mode: 0 = reroll from four candidate tier ids, 1 = use the stored loadout bytes directly. Each packed speciality seed byte contains two 4-bit speciality seed values that expand into the live 8-slot speciality cycle.";
        BattleUnknown1Entry.Text = $"{battle.InitializationMode} ({GetBattleInitializationModeName(battle.InitializationMode)})";
        BattleAlwaysZeroEntry.Text = $"{battle.TemplateFlags} ({GetBattleTemplateFlagName(battle.TemplateFlags)})";
        PopulateBattleUnknownEntries(battle.Bots[0], battle.Bots[0].MedalId, battle.Bots[0].MedalLevel, [BattleBot1Cycle1ComboBox, BattleBot1Cycle2ComboBox, BattleBot1Cycle3ComboBox, BattleBot1Cycle4ComboBox, BattleBot1Cycle5ComboBox, BattleBot1Cycle6ComboBox, BattleBot1Cycle7ComboBox, BattleBot1Cycle8ComboBox], [BattleBot1Unknown0Entry, BattleBot1Unknown2Entry, BattleBot1Unknown4Entry, BattleBot1Cycle4Label, BattleBot1Cycle5Label, BattleBot1Cycle6Label, BattleBot1Cycle7Label, BattleBot1Cycle8Label], [BattleBot1Unknown1Entry, BattleBot1Unknown3Entry, BattleBot1Unknown5Entry, BattleBot1Cycle4ValueLabel, BattleBot1Cycle5ValueLabel, BattleBot1Cycle6ValueLabel, BattleBot1Cycle7ValueLabel, BattleBot1Cycle8ValueLabel], BattleBot1CycleResetEntry, BattleBot1ReservedEntry);
        PopulateBattleUnknownEntries(battle.Bots[1], battle.Bots[1].MedalId, battle.Bots[1].MedalLevel, [BattleBot2Cycle1ComboBox, BattleBot2Cycle2ComboBox, BattleBot2Cycle3ComboBox, BattleBot2Cycle4ComboBox, BattleBot2Cycle5ComboBox, BattleBot2Cycle6ComboBox, BattleBot2Cycle7ComboBox, BattleBot2Cycle8ComboBox], [BattleBot2Unknown0Entry, BattleBot2Unknown2Entry, BattleBot2Unknown4Entry, BattleBot2Cycle4Label, BattleBot2Cycle5Label, BattleBot2Cycle6Label, BattleBot2Cycle7Label, BattleBot2Cycle8Label], [BattleBot2Unknown1Entry, BattleBot2Unknown3Entry, BattleBot2Unknown5Entry, BattleBot2Cycle4ValueLabel, BattleBot2Cycle5ValueLabel, BattleBot2Cycle6ValueLabel, BattleBot2Cycle7ValueLabel, BattleBot2Cycle8ValueLabel], BattleBot2CycleResetEntry, BattleBot2ReservedEntry);
        PopulateBattleUnknownEntries(battle.Bots[2], battle.Bots[2].MedalId, battle.Bots[2].MedalLevel, [BattleBot3Cycle1ComboBox, BattleBot3Cycle2ComboBox, BattleBot3Cycle3ComboBox, BattleBot3Cycle4ComboBox, BattleBot3Cycle5ComboBox, BattleBot3Cycle6ComboBox, BattleBot3Cycle7ComboBox, BattleBot3Cycle8ComboBox], [BattleBot3Unknown0Entry, BattleBot3Unknown2Entry, BattleBot3Unknown4Entry, BattleBot3Cycle4Label, BattleBot3Cycle5Label, BattleBot3Cycle6Label, BattleBot3Cycle7Label, BattleBot3Cycle8Label], [BattleBot3Unknown1Entry, BattleBot3Unknown3Entry, BattleBot3Unknown5Entry, BattleBot3Cycle4ValueLabel, BattleBot3Cycle5ValueLabel, BattleBot3Cycle6ValueLabel, BattleBot3Cycle7ValueLabel, BattleBot3Cycle8ValueLabel], BattleBot3CycleResetEntry, BattleBot3ReservedEntry);
    }

    private string BuildBattleBotSummary(byte headFamilyId, byte rightFamilyId, byte leftFamilyId, byte legsFamilyId, byte medalId, byte medalLevel)
    {
        return $"Head {GetBattleSlotDisplayName(PartKind.Head, headFamilyId)} ({headFamilyId}), Right {GetBattleSlotDisplayName(PartKind.RightArm, rightFamilyId)} ({rightFamilyId}), Left {GetBattleSlotDisplayName(PartKind.LeftArm, leftFamilyId)} ({leftFamilyId}), Legs {GetBattleSlotDisplayName(PartKind.Legs, legsFamilyId)} ({legsFamilyId}), Medal {_metadata.GetMedalName(medalId)} ({medalId}), Level {medalLevel}.";
    }

    private static void PopulateBattleBotEntries(BattleBot bot, System.Windows.Controls.ComboBox head, System.Windows.Controls.ComboBox right, System.Windows.Controls.ComboBox left, System.Windows.Controls.ComboBox legs, System.Windows.Controls.ComboBox medal, System.Windows.Controls.ComboBox level)
    {
        head.SelectedValue = (int)bot.HeadPartId;
        right.SelectedValue = (int)bot.RightArmPartId;
        left.SelectedValue = (int)bot.LeftArmPartId;
        legs.SelectedValue = (int)bot.LegsPartId;
        medal.SelectedValue = (int)bot.MedalId;
        level.SelectedValue = (int)bot.MedalLevel;
    }

    private void PopulateBattleUnknownEntries(BattleBot bot, byte medalId, byte medalLevel, System.Windows.Controls.ComboBox[] cycleCombos, WpfTextBlock[] cycleLabels, WpfTextBlock[] valueLabels, WpfTextBox cycleResetEntry, WpfTextBox reservedEntry)
    {
        var cycleEntries = BattleSpecialityTemplateHelper.UnpackCycleEntries(bot);
        var scaledValues = _session is null
            ? Enumerable.Repeat((byte)1, BattleSpecialityTemplateHelper.CycleSlotCount).ToArray()
            : BattleSpecialityTemplateHelper.ComputeScaledMedalSlotValues(_session.RomFile, medalId, medalLevel);

        for (var index = 0; index < BattleSpecialityTemplateHelper.CycleSlotCount; index++)
        {
            cycleLabels[index].Text = GetBattleDerivedSpecialityName(index);
            valueLabels[index].Text = $"In-game value: {scaledValues[index]}";
            cycleCombos[index].SelectedValue = (int)cycleEntries[index];
        }

        cycleResetEntry.Text = bot.SpecialityCycleResetValue.ToString();
        reservedEntry.Text = bot.ReservedZeroByte.ToString();
    }

    private BattleDefinition BuildBattleFromEditor(BattleDefinition original)
    {
        BattleBot[] bots =
        [
            BuildBattleBot(original.Bots[0], BattleBot1HeadEntry, BattleBot1RightEntry, BattleBot1LeftEntry, BattleBot1LegsEntry, BattleBot1MedalEntry, BattleBot1LevelEntry, [BattleBot1Cycle1ComboBox, BattleBot1Cycle2ComboBox, BattleBot1Cycle3ComboBox, BattleBot1Cycle4ComboBox, BattleBot1Cycle5ComboBox, BattleBot1Cycle6ComboBox, BattleBot1Cycle7ComboBox, BattleBot1Cycle8ComboBox]),
            BuildBattleBot(original.Bots[1], BattleBot2HeadEntry, BattleBot2RightEntry, BattleBot2LeftEntry, BattleBot2LegsEntry, BattleBot2MedalEntry, BattleBot2LevelEntry, [BattleBot2Cycle1ComboBox, BattleBot2Cycle2ComboBox, BattleBot2Cycle3ComboBox, BattleBot2Cycle4ComboBox, BattleBot2Cycle5ComboBox, BattleBot2Cycle6ComboBox, BattleBot2Cycle7ComboBox, BattleBot2Cycle8ComboBox]),
            BuildBattleBot(original.Bots[2], BattleBot3HeadEntry, BattleBot3RightEntry, BattleBot3LeftEntry, BattleBot3LegsEntry, BattleBot3MedalEntry, BattleBot3LevelEntry, [BattleBot3Cycle1ComboBox, BattleBot3Cycle2ComboBox, BattleBot3Cycle3ComboBox, BattleBot3Cycle4ComboBox, BattleBot3Cycle5ComboBox, BattleBot3Cycle6ComboBox, BattleBot3Cycle7ComboBox, BattleBot3Cycle8ComboBox])
        ];

        return new BattleDefinition(original.Id, original.PointerOffset, original.DataOffset, ParseByte(BattleCharacterEntry.Text, "Battle character"), original.InitializationMode, ParseByte(BattleBotCountEntry.Text, "Battle bot count"), original.TemplateFlags, bots);
    }

    private static BattleBot BuildBattleBot(BattleBot original, System.Windows.Controls.ComboBox head, System.Windows.Controls.ComboBox right, System.Windows.Controls.ComboBox left, System.Windows.Controls.ComboBox legs, System.Windows.Controls.ComboBox medal, System.Windows.Controls.ComboBox level, System.Windows.Controls.ComboBox[] cycleCombos)
    {
        var cycleEntries = cycleCombos.Select((comboBox, index) => ParseCycleEntry(comboBox, index)).ToArray();
        var packed = BattleSpecialityTemplateHelper.PackCycleEntries(cycleEntries);
        return new BattleBot(ParseComboBoxByte(head, "Head part"), ParseComboBoxByte(right, "Right arm part"), ParseComboBoxByte(left, "Left arm part"), ParseComboBoxByte(legs, "Leg part"), ParseComboBoxByte(medal, "Medal"), ParseComboBoxByte(level, "Medal level"), packed[0], packed[1], packed[2], packed[3], original.SpecialityCycleResetValue, original.ReservedZeroByte);
    }

    private void OnBattleLoadoutSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        RefreshBattleDerivedLabels();
        RefreshBattleBotSummariesFromSelections();
    }

    private void PopulatePartEditor(PartDefinition part)
    {
        PartOverviewLabel.Text = $"{part.Id:D3}  {_metadata.GetPartName(part.Id)}";
        PartOverviewHintLabel.Text = $"Kind: {part.Kind}   Medabot: {_metadata.GetBotName(part.MedabotId)} ({part.MedabotId})   Data: 0x{part.DataOffset:X}";
        PartMedalCompatibilityComboBox.SelectedValue = part.MedalCompatibility;
        PartSpecialityComboBox.SelectedValue = part.Speciality;
        PartGenderComboBox.SelectedValue = part.Gender;
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
            PartTechniqueComboBox.Visibility = Visibility.Collapsed;
            PartLegTypeComboBox.Visibility = Visibility.Visible;
            PartValue1Entry.Visibility = Visibility.Collapsed;
            PartLegTypeComboBox.SelectedValue = stats.LegType;
            PartValue1Entry.Text = stats.LegType.ToString();
            PartValue2Entry.Text = stats.Propulsion.ToString();
            PartValue3Entry.Text = stats.Evasion.ToString();
            PartValue4CheckBox.Visibility = Visibility.Collapsed;
            PartValue4Entry.Visibility = Visibility.Visible;
            PartValue4Entry.Text = stats.Defense.ToString();
            PartValue5Entry.Text = stats.Conceal.ToString();
            RefreshPartEditorHelp(part);
            return;
        }

        var combat = part.AsCombatPartStats();
        PartTechniqueComboBox.Visibility = Visibility.Visible;
        PartLegTypeComboBox.Visibility = Visibility.Collapsed;
        PartValue1Entry.Visibility = Visibility.Collapsed;
        PartTechniqueComboBox.SelectedValue = combat.Technique;
        PartValue1Entry.Text = combat.Technique.ToString();
        PartValue2Entry.Text = combat.Success.ToString();
        PartValue3Entry.Text = combat.Power.ToString();
        PartValue4Entry.Visibility = Visibility.Collapsed;
        PartValue4CheckBox.Visibility = Visibility.Visible;
        PartValue4CheckBox.IsChecked = combat.ChargeOrChainReaction != 0;
        PartValue5Entry.Text = combat.Uses.ToString();
        RefreshPartEditorHelp(part);
    }

    private void ResetPartEditorLabels(PartKind kind)
    {
        if (kind == PartKind.Legs)
        {
            PartTechniqueComboBox.Visibility = Visibility.Collapsed;
            PartLegTypeComboBox.Visibility = Visibility.Visible;
            PartValue4CheckBox.Visibility = Visibility.Collapsed;
            PartValue4Entry.Visibility = Visibility.Visible;
            PartValue1Entry.Visibility = Visibility.Collapsed;
            PartValue1Label.Text = "Leg Type";
            PartValue2Label.Text = "Propulsion";
            PartValue3Label.Text = "Evasion";
            PartValue4Label.Text = "Defense";
            PartValue5Label.Text = "Proximity";
            PartUnknown2Label.Text = "Remoteness";
            PartUnknown4Label.Text = "Price (x100)";
            PartUnknown5Label.Text = "Attack Scalar";
            return;
        }

        PartTechniqueComboBox.Visibility = Visibility.Visible;
        PartLegTypeComboBox.Visibility = Visibility.Collapsed;
        PartValue4Entry.Visibility = Visibility.Collapsed;
        PartValue4CheckBox.Visibility = Visibility.Visible;
        PartValue1Entry.Visibility = Visibility.Collapsed;
        PartValue1Label.Text = "Technique";
        PartValue2Label.Text = "Rate of Success";
        PartValue3Label.Text = "Power";
        PartValue4Label.Text = "Chain Reaction";
        PartValue5Label.Text = kind == PartKind.Head ? "Uses" : "Charge";
        PartUnknown2Label.Text = "Radiation";
        PartUnknown4Label.Text = "Price (x100)";
        PartUnknown5Label.Text = "Attack Scalar";
    }

    private void RefreshPartEditorHelp(PartDefinition part)
    {
        PartMedalCompatibilityHintLabel.Text = $"Compatibility attribute: {_metadata.GetPartAttributeName(part.MedalCompatibility)} ({part.MedalCompatibility}). Matching this against the bot's medal class contributes the medal-part compatibility bonus and drives the attribute name shown in the part-detail UI.";
        PartSpecialityHintLabel.Text = $"Speciality grouping: {_metadata.GetSpecialityName(part.Speciality)} ({part.Speciality}).";
        PartGenderHintLabel.Text = part.Gender switch
        {
            0 => "Male / default gender restriction value.",
            1 => "Female / alternate gender restriction value.",
            _ => $"Unexpected gender restriction value: {part.Gender}."
        };
        PartArmorHintLabel.Text = "Armor is the part HP value contributed in battle.";

        if (part.IsLegPart)
        {
            PartValue1HintLabel.Text = $"Leg type is the movement/chassis family used by movement formulas: {_metadata.GetLegTypeName(part.TechniqueOrLegType)} ({part.TechniqueOrLegType}).";
            PartValue2HintLabel.Text = "Propulsion controls movement performance and speed-related handling.";
            PartValue3HintLabel.Text = "Evasion affects how well the Medabot avoids incoming attacks.";
            PartValue4HintLabel.Text = "Defense affects how well the legs absorb or mitigate hits.";
            PartValue5HintLabel.Text = "Proximity is the legs-derived bonus used for close-range specialities.";
            PartUnknown2HintLabel.Text = "Remoteness is the legs-derived bonus used for ranged specialities.";
            PartUnknown45HintLabel.Text = "Price is stored in units of 100 currency. The attack-scalar slot is still present in the raw record, but its battle meaning is not proven for legs yet.";
            PartUnknown3HintLabel.Text = "Bot Shared Value is duplicated across the full 4-part set for each Medabot, but its exact gameplay meaning is still unresolved.";
            return;
        }

        var combat = part.AsCombatPartStats();
        PartValue1HintLabel.Text = $"Technique selects the action family: {_metadata.GetTechniqueName(combat.Technique)} ({combat.Technique}).";
        PartValue2HintLabel.Text = "Rate of Success is the base accuracy / reliability stat for the action.";
        PartValue3HintLabel.Text = "Power is the base damage or effect strength.";
        PartValue4HintLabel.Text = "Chain Reaction is a boolean toggle stored in the shared slot-specific stat byte at record offset +7.";
        PartValue5HintLabel.Text = part.Kind == PartKind.Head
            ? "Uses is the head ammo / remaining-use count. Battle code clamps head consumption against this byte."
            : "Charge is the arm action charge value. This shares the same raw record byte that heads use for Uses.";
        PartUnknown2HintLabel.Text = "Radiation is a per-part scalar used by combat logic for radiation-type interactions.";
        PartUnknown45HintLabel.Text = "Price is stored in units of 100 currency. Attack Scalar is the per-part coefficient used by battle damage/attack scaling code.";
        PartUnknown3HintLabel.Text = "Bot Shared Value is duplicated across the full 4-part set for each Medabot, but its exact gameplay meaning is still unresolved.";
    }

    private PartDefinition BuildPartFromEditor(PartDefinition original)
    {
        var medalCompatibility = ParseComboBoxByte(PartMedalCompatibilityComboBox, "Compatibility attribute");
        var speciality = ParseComboBoxByte(PartSpecialityComboBox, "Speciality");
        var gender = ParseComboBoxByte(PartGenderComboBox, "Gender");
        var value1 = original.Kind == PartKind.Legs
            ? ParseComboBoxByte(PartLegTypeComboBox, PartValue1Label.Text)
            : ParseComboBoxByte(PartTechniqueComboBox, PartValue1Label.Text);

        var value4 = original.Kind == PartKind.Legs
            ? ParseByte(PartValue4Entry.Text, PartValue4Label.Text)
            : (byte)(PartValue4CheckBox.IsChecked == true ? 1 : 0);

        return new PartDefinition(original.Id, original.MedabotId, original.Kind, original.DataOffset, medalCompatibility, value1, speciality, gender, ParseByte(PartArmorEntry.Text, "Armor"), ParseByte(PartValue2Entry.Text, PartValue2Label.Text), ParseByte(PartValue3Entry.Text, PartValue3Label.Text), value4, ParseByte(PartValue5Entry.Text, PartValue5Label.Text), ParseByte(PartUnknown2Entry.Text, "Unknown2"), ParseByte(PartUnknown3Entry.Text, "Unknown3"), ParseByte(PartUnknown4Entry.Text, "Unknown4"), ParseByte(PartUnknown5Entry.Text, "Unknown5"), ParseByte(PartUnknown6Entry.Text, "Unknown6"), ParseByte(PartUnknown7Entry.Text, "Unknown7"), ParseByte(PartUnknown8Entry.Text, "Unknown8"));
    }

    private void RefreshPartLegTypeOptions(IReadOnlyList<PartDefinition> parts)
    {
        _partLegTypeOptions.Clear();

        foreach (var legType in parts
                     .Where(part => part.Kind == PartKind.Legs)
                     .Select(part => (int)part.TechniqueOrLegType)
                     .Distinct()
                     .OrderBy(value => value))
        {
            _partLegTypeOptions.Add(new BrowserItem(legType, $"{legType:D3}  {_metadata.GetLegTypeName(legType)}"));
        }
    }

    private static byte ParseComboBoxByte(System.Windows.Controls.ComboBox comboBox, string fieldName)
    {
        if (comboBox.SelectedValue is int intValue && intValue >= 0 && intValue <= byte.MaxValue)
        {
            return (byte)intValue;
        }

        if (comboBox.SelectedValue is byte byteValue)
        {
            return byteValue;
        }

        throw new InvalidOperationException($"{fieldName} must be selected from the dropdown.");
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

    private string FormatBattle(BattleDefinition battle)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Battle {battle.Id} @ 0x{battle.DataOffset:X}");
        builder.AppendLine($"Character: {_metadata.GetCharacterName(battle.CharacterId)} ({battle.CharacterId})");
        builder.AppendLine($"InitializationMode: {battle.InitializationMode} ({GetBattleInitializationModeName(battle.InitializationMode)})");
        builder.AppendLine($"NumberOfBots: {battle.NumberOfBots}");
        builder.AppendLine($"PartLoadoutMode: {battle.TemplateFlags} ({GetBattleTemplateFlagName(battle.TemplateFlags)})");
        for (var index = 0; index < battle.Bots.Count; index++)
        {
            var bot = battle.Bots[index];
            builder.AppendLine($"Bot {index + 1}: head={GetBattleSlotDisplayName(PartKind.Head, bot.HeadPartId)} ({bot.HeadPartId}), right={GetBattleSlotDisplayName(PartKind.RightArm, bot.RightArmPartId)} ({bot.RightArmPartId}), left={GetBattleSlotDisplayName(PartKind.LeftArm, bot.LeftArmPartId)} ({bot.LeftArmPartId}), legs={GetBattleSlotDisplayName(PartKind.Legs, bot.LegsPartId)} ({bot.LegsPartId}), medal={_metadata.GetMedalName(bot.MedalId)} ({bot.MedalId}), level={bot.MedalLevel}");
        }

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


    private static string FormatBytes(IEnumerable<byte> bytes) => string.Join(" ", bytes.Select(value => value.ToString("X2")));
    private static string GetBattleInitializationModeName(byte value) => value switch
    {
        0 => "Normal initialization",
        1 => "Reuse preinitialized battle state",
        _ => $"Unknown mode {value}"
    };

    private static string GetBattleTemplateFlagName(byte value) => value switch
    {
        0 => "Reroll from candidate tiers",
        1 => "Use stored loadout directly",
        _ => $"Unknown mode {value}"
    };

    private void InitializeBattleCycleComboBoxes()
    {
        foreach (var comboBox in new[]
                 {
                     BattleBot1Cycle1ComboBox, BattleBot1Cycle2ComboBox, BattleBot1Cycle3ComboBox, BattleBot1Cycle4ComboBox,
                     BattleBot1Cycle5ComboBox, BattleBot1Cycle6ComboBox, BattleBot1Cycle7ComboBox, BattleBot1Cycle8ComboBox,
                     BattleBot2Cycle1ComboBox, BattleBot2Cycle2ComboBox, BattleBot2Cycle3ComboBox, BattleBot2Cycle4ComboBox,
                     BattleBot2Cycle5ComboBox, BattleBot2Cycle6ComboBox, BattleBot2Cycle7ComboBox, BattleBot2Cycle8ComboBox,
                     BattleBot3Cycle1ComboBox, BattleBot3Cycle2ComboBox, BattleBot3Cycle3ComboBox, BattleBot3Cycle4ComboBox,
                     BattleBot3Cycle5ComboBox, BattleBot3Cycle6ComboBox, BattleBot3Cycle7ComboBox, BattleBot3Cycle8ComboBox
                 })
        {
            comboBox.ItemsSource = _battleCycleEntryOptions;
            comboBox.DisplayMemberPath = nameof(BrowserItem.Title);
            comboBox.SelectedValuePath = nameof(BrowserItem.Id);
        }
    }

    private static string GetBattleCycleEntryName(byte value) => value switch
    {
        0 => "No action",
        1 => "Head part action",
        2 => "Right arm action",
        3 => "Left arm action",
        4 => "Head fallback",
        5 => "Medaforce 1",
        6 => "Medaforce 2",
        7 => "Medaforce 3",
        _ => $"Unknown cycle entry {value}"
    };

    private static byte ParseCycleEntry(System.Windows.Controls.ComboBox comboBox, int slotIndex)
    {
        if (comboBox.SelectedValue is int intValue && intValue >= 0 && intValue <= 0x0F)
        {
            return (byte)intValue;
        }

        if (comboBox.SelectedValue is byte byteValue && byteValue <= 0x0F)
        {
            return byteValue;
        }

        throw new InvalidOperationException($"Battle cycle slot {slotIndex + 1} must be selected from the dropdown.");
    }

    private void RefreshBattleDerivedLabels()
    {
        if (_session is null)
        {
            BattleBot1DerivedLabel.Text = string.Empty;
            BattleBot2DerivedLabel.Text = string.Empty;
            BattleBot3DerivedLabel.Text = string.Empty;
            return;
        }

        BattleBot1DerivedLabel.Text = BuildBattleDerivedLabel(BattleBot1MedalEntry, BattleBot1LevelEntry, [BattleBot1Cycle1ComboBox, BattleBot1Cycle2ComboBox, BattleBot1Cycle3ComboBox, BattleBot1Cycle4ComboBox, BattleBot1Cycle5ComboBox, BattleBot1Cycle6ComboBox, BattleBot1Cycle7ComboBox, BattleBot1Cycle8ComboBox]);
        BattleBot2DerivedLabel.Text = BuildBattleDerivedLabel(BattleBot2MedalEntry, BattleBot2LevelEntry, [BattleBot2Cycle1ComboBox, BattleBot2Cycle2ComboBox, BattleBot2Cycle3ComboBox, BattleBot2Cycle4ComboBox, BattleBot2Cycle5ComboBox, BattleBot2Cycle6ComboBox, BattleBot2Cycle7ComboBox, BattleBot2Cycle8ComboBox]);
        BattleBot3DerivedLabel.Text = BuildBattleDerivedLabel(BattleBot3MedalEntry, BattleBot3LevelEntry, [BattleBot3Cycle1ComboBox, BattleBot3Cycle2ComboBox, BattleBot3Cycle3ComboBox, BattleBot3Cycle4ComboBox, BattleBot3Cycle5ComboBox, BattleBot3Cycle6ComboBox, BattleBot3Cycle7ComboBox, BattleBot3Cycle8ComboBox]);
    }

    private string BuildBattleDerivedLabel(System.Windows.Controls.ComboBox medalComboBox, System.Windows.Controls.ComboBox levelComboBox, System.Windows.Controls.ComboBox[] cycleCombos)
    {
        if (!TryGetComboBoxByte(medalComboBox, out var medalId) || !TryGetComboBoxByte(levelComboBox, out var medalLevel))
        {
            return "Derived medal slots: enter a valid medal id and medal level.";
        }

        var scaledValues = BattleSpecialityTemplateHelper.ComputeScaledMedalSlotValues(_session!.RomFile, medalId, medalLevel);
        return string.Join(Environment.NewLine, scaledValues.Select((value, index) => $"{GetBattleDerivedSpecialityName(index)}: {value}"));
    }

    private void InitializeBattleLoadoutComboBoxes()
    {
        foreach (var comboBox in new[] { BattleBot1MedalEntry, BattleBot2MedalEntry, BattleBot3MedalEntry })
        {
            comboBox.ItemsSource = _battleMedalOptions;
        }

        foreach (var comboBox in new[] { BattleBot1LevelEntry, BattleBot2LevelEntry, BattleBot3LevelEntry })
        {
            comboBox.ItemsSource = _battleLevelOptions;
        }
    }

    private void RefreshBattleLoadoutOptions()
    {
        _battleHeadOptions.Clear();
        _battleRightOptions.Clear();
        _battleLeftOptions.Clear();
        _battleLegsOptions.Clear();

        if (_session is null)
        {
            return;
        }

        foreach (var part in _loadedParts.Where(static part => part.Kind == PartKind.Head).OrderBy(part => part.MedabotId))
        {
            _battleHeadOptions.Add(BuildBattleLoadoutOption(part, 0));
        }

        foreach (var part in _loadedParts.Where(static part => part.Kind == PartKind.RightArm).OrderBy(part => part.MedabotId))
        {
            _battleRightOptions.Add(BuildBattleLoadoutOption(part, 1));
        }

        foreach (var part in _loadedParts.Where(static part => part.Kind == PartKind.LeftArm).OrderBy(part => part.MedabotId))
        {
            _battleLeftOptions.Add(BuildBattleLoadoutOption(part, 3));
        }

        foreach (var part in _loadedParts.Where(static part => part.Kind == PartKind.Legs).OrderBy(part => part.MedabotId))
        {
            _battleLegsOptions.Add(BuildBattleLoadoutOption(part, 5));
        }

        foreach (var comboBox in new[] { BattleBot1HeadEntry, BattleBot2HeadEntry, BattleBot3HeadEntry })
        {
            comboBox.ItemsSource = _battleHeadOptions;
        }

        foreach (var comboBox in new[] { BattleBot1RightEntry, BattleBot2RightEntry, BattleBot3RightEntry })
        {
            comboBox.ItemsSource = _battleRightOptions;
        }

        foreach (var comboBox in new[] { BattleBot1LeftEntry, BattleBot2LeftEntry, BattleBot3LeftEntry })
        {
            comboBox.ItemsSource = _battleLeftOptions;
        }

        foreach (var comboBox in new[] { BattleBot1LegsEntry, BattleBot2LegsEntry, BattleBot3LegsEntry })
        {
            comboBox.ItemsSource = _battleLegsOptions;
        }
    }

    private BattleLoadoutOption BuildBattleLoadoutOption(PartDefinition part, int componentIndex)
    {
        var bitmap = CreateBattleLoadoutThumbnail(part, componentIndex);
        var title = _metadata.GetPartName(part.Id);
        var subtitle = part.MedabotId < ImageAssetRepository.CompositeBattleSpritePartCount
            ? $"Bot {part.MedabotId:D3}  Part {part.Id:D3}"
            : $"Uninitialized family {part.MedabotId:D3}  Part {part.Id:D3}";
        return new BattleLoadoutOption(part.MedabotId, part.Id, title, subtitle, bitmap);
    }

    private BitmapSource CreateBattleLoadoutThumbnail(PartDefinition part, int componentIndex)
    {
        if (part.MedabotId < 0 || part.MedabotId >= ImageAssetRepository.CompositeBattleSpritePartCount)
        {
            return CreateBattleLoadoutPlaceholderBitmap();
        }

        var asset = GetCurrentBattleCompositeComponentAsset(part.MedabotId, componentIndex);
        var image = componentIndex == 5 ? GetBattleLoadoutLegPreviewImage(asset.Image) : asset.Image;
        var swatches = BuildPaletteSwatches(image.PaletteBytes);
        return CreateBitmapSource(image.PixelIndices, image.TileWidth, swatches);
    }

    private static BitmapSource CreateBattleLoadoutPlaceholderBitmap()
    {
        const int width = 16;
        const int height = 16;
        const int stride = width * 4;
        var pixels = new byte[height * stride];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var index = (y * stride) + (x * 4);
                var dark = ((x / 4) + (y / 4)) % 2 == 0;
                var shade = dark ? (byte)0x9C : (byte)0xD1;
                pixels[index + 0] = shade;
                pixels[index + 1] = shade;
                pixels[index + 2] = shade;
                pixels[index + 3] = 0xFF;
            }
        }

        return BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
    }

    private static IndexedImage GetBattleLoadoutLegPreviewImage(IndexedImage image)
    {
        if (image.TileHeight < 3)
        {
            return image;
        }

        var firstSegmentTileHeight = Math.Max(1, image.TileHeight / 3);
        var tileCount = image.TileWidth * firstSegmentTileHeight;
        var pixelLength = tileCount * 64;
        var croppedPixels = new byte[pixelLength];
        Array.Copy(image.PixelIndices, 0, croppedPixels, 0, Math.Min(pixelLength, image.PixelIndices.Length));
        return new IndexedImage(image.TileWidth, firstSegmentTileHeight, croppedPixels, image.PaletteBytes);
    }

    private void RefreshBattleBotSummariesFromSelections()
    {
        BattleBot1SummaryLabel.Text = BuildBattleBotSummary(GetSelectedBattleFamilyId(BattleBot1HeadEntry), GetSelectedBattleFamilyId(BattleBot1RightEntry), GetSelectedBattleFamilyId(BattleBot1LeftEntry), GetSelectedBattleFamilyId(BattleBot1LegsEntry), GetSelectedBattleMedalId(BattleBot1MedalEntry), GetSelectedBattleLevel(BattleBot1LevelEntry));
        BattleBot2SummaryLabel.Text = BuildBattleBotSummary(GetSelectedBattleFamilyId(BattleBot2HeadEntry), GetSelectedBattleFamilyId(BattleBot2RightEntry), GetSelectedBattleFamilyId(BattleBot2LeftEntry), GetSelectedBattleFamilyId(BattleBot2LegsEntry), GetSelectedBattleMedalId(BattleBot2MedalEntry), GetSelectedBattleLevel(BattleBot2LevelEntry));
        BattleBot3SummaryLabel.Text = BuildBattleBotSummary(GetSelectedBattleFamilyId(BattleBot3HeadEntry), GetSelectedBattleFamilyId(BattleBot3RightEntry), GetSelectedBattleFamilyId(BattleBot3LeftEntry), GetSelectedBattleFamilyId(BattleBot3LegsEntry), GetSelectedBattleMedalId(BattleBot3MedalEntry), GetSelectedBattleLevel(BattleBot3LevelEntry));
    }

    private string GetBattleSlotDisplayName(PartKind kind, int familyId)
    {
        var partId = ResolveBattleSlotPartId(kind, familyId);
        return partId >= 0 ? _metadata.GetPartName(partId) : _metadata.GetBotName(familyId);
    }

    private int ResolveBattleSlotPartId(PartKind kind, int familyId)
    {
        var match = _loadedParts.FirstOrDefault(part => part.MedabotId == familyId && part.Kind == kind);
        return match?.Id ?? -1;
    }

    private static bool TryGetComboBoxByte(System.Windows.Controls.ComboBox comboBox, out byte value)
    {
        if (comboBox.SelectedValue is int intValue && intValue >= 0 && intValue <= byte.MaxValue)
        {
            value = (byte)intValue;
            return true;
        }

        if (comboBox.SelectedValue is byte byteValue)
        {
            value = byteValue;
            return true;
        }

        value = 0;
        return false;
    }

    private static byte GetSelectedBattleFamilyId(System.Windows.Controls.ComboBox comboBox) => TryGetComboBoxByte(comboBox, out var value) ? value : (byte)0;
    private static byte GetSelectedBattleMedalId(System.Windows.Controls.ComboBox comboBox) => TryGetComboBoxByte(comboBox, out var value) ? value : (byte)0;
    private static byte GetSelectedBattleLevel(System.Windows.Controls.ComboBox comboBox) => TryGetComboBoxByte(comboBox, out var value) ? value : (byte)0;
    private static string GetSelectedBattleCycleEntryName(System.Windows.Controls.ComboBox comboBox) => TryGetComboBoxByte(comboBox, out var value) ? GetBattleCycleEntryName(value) : "Unselected";

    private string GetBattleDerivedSpecialityName(int index)
    {
        return index >= 0 && index < _metadata.Catalog.Specialities.Count
            ? _metadata.GetSpecialityName(index)
            : $"Speciality {index + 1}";
    }
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
