namespace Medabots.Rom.Metadata;

public sealed class MedabotsCatalog
{
    public List<string> Characters { get; set; } = [];

    public List<string> Maps { get; set; } = [];

    public List<string> Parts { get; set; } = [];

    public List<string> Bots { get; set; } = [];

    public List<string> Specialities { get; set; } = [];

    public List<string> Techniques { get; set; } = [];

    public List<string> Medals { get; set; } = [];

    public List<string> SongNames { get; set; } = [];

    public List<int> BestMedalByBot { get; set; } = [];

    public List<bool> FemaleBots { get; set; } = [];
}
