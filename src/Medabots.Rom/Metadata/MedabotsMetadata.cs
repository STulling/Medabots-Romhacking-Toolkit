using System.Collections.Frozen;
using System.Reflection;
using System.Text.Json;

namespace Medabots.Rom.Metadata;

public sealed class MedabotsMetadata
{
    private const string ResourceName = "Medabots.Rom.medabots.catalog.json";

    private readonly FrozenDictionary<int, int> _bestMedalByBot;
    private readonly FrozenSet<int> _femaleBots;

    private MedabotsMetadata(MedabotsCatalog catalog)
    {
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _bestMedalByBot = catalog.BestMedalByBot
            .Select((medalId, botId) => new KeyValuePair<int, int>(botId, medalId))
            .ToFrozenDictionary();
        _femaleBots = catalog.FemaleBots
            .Select((isFemale, botId) => new { isFemale, botId })
            .Where(entry => entry.isFemale)
            .Select(entry => entry.botId)
            .ToFrozenSet();
    }

    public static MedabotsMetadata Default { get; } = LoadDefault();

    public MedabotsCatalog Catalog { get; }

    public string GetCharacterName(int id) => GetName(Catalog.Characters, id, "character");

    public string GetMapName(int id) => GetName(Catalog.Maps, id, "map");

    public string GetPartName(int id) => GetName(Catalog.Parts, id, "part");

    public string GetBotName(int id) => GetName(Catalog.Bots, id, "bot");

    public string GetSpecialityName(int id) => GetName(Catalog.Specialities, id, "speciality");

    public string GetTechniqueName(int id) => GetName(Catalog.Techniques, id, "technique");

    public string GetMedalName(int id) => GetName(Catalog.Medals, id, "medal");

    public string GetSongName(int id) => GetName(Catalog.SongNames, id, "song");

    public bool TryGetBestMedalId(int botId, out int medalId) => _bestMedalByBot.TryGetValue(botId, out medalId);

    public bool IsFemaleBot(int botId) => _femaleBots.Contains(botId);

    public static MedabotsMetadata LoadDefault()
    {
        var assembly = typeof(MedabotsMetadata).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Could not find embedded metadata resource '{ResourceName}'.");
        return Load(stream);
    }

    public static MedabotsMetadata Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;

        var catalog = new MedabotsCatalog
        {
            Characters = ReadStringArray(root, "characters"),
            Maps = ReadStringArray(root, "maps"),
            Parts = ReadFlattenedStringArray(root, "parts"),
            Bots = ReadStringArray(root, "bots"),
            Specialities = ReadStringArray(root, "specialities"),
            Techniques = ReadStringArray(root, "techniques"),
            Medals = ReadStringArray(root, "medals"),
            SongNames = ReadStringArray(root, "songNames"),
            BestMedalByBot = ReadIntArray(root, "bestMedalByBot"),
            FemaleBots = ReadBoolArray(root, "femaleBots")
        };

        return new MedabotsMetadata(catalog);
    }

    private static string GetName(IReadOnlyList<string> values, int id, string category)
    {
        if (id >= 0 && id < values.Count && !string.IsNullOrWhiteSpace(values[id]))
        {
            return values[id];
        }

        return $"Unknown {category} #{id}";
    }

    private static List<string> ReadStringArray(JsonElement root, string propertyName)
    {
        return root.GetProperty(propertyName)
            .EnumerateArray()
            .Select(value => value.GetString() ?? string.Empty)
            .ToList();
    }

    private static List<string> ReadFlattenedStringArray(JsonElement root, string propertyName)
    {
        var values = new List<string>();

        foreach (var entry in root.GetProperty(propertyName).EnumerateArray())
        {
            if (entry.ValueKind == JsonValueKind.Array)
            {
                values.AddRange(entry.EnumerateArray().Select(value => value.GetString() ?? string.Empty));
                continue;
            }

            values.Add(entry.GetString() ?? string.Empty);
        }

        return values;
    }

    private static List<int> ReadIntArray(JsonElement root, string propertyName)
    {
        return root.GetProperty(propertyName)
            .EnumerateArray()
            .Select(value => value.GetInt32())
            .ToList();
    }

    private static List<bool> ReadBoolArray(JsonElement root, string propertyName)
    {
        return root.GetProperty(propertyName)
            .EnumerateArray()
            .Select(value => value.GetBoolean())
            .ToList();
    }
}
