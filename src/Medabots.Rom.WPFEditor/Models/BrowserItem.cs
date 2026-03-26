namespace Medabots.Rom.Editor;

public sealed record BrowserItem(int Id, string Title)
{
    public string FilterText => $"{Id} {Title}";
}
