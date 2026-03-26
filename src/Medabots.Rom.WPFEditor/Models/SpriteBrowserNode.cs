using System.Collections.ObjectModel;

namespace Medabots.Rom.WPFEditor.Models;

public sealed class SpriteBrowserNode
{
    public string Title { get; init; } = string.Empty;
    public string FilterText { get; init; } = string.Empty;
    public SpriteAssetKind AssetKind { get; init; }
    public int PrimaryId { get; init; } = -1;
    public int SecondaryId { get; init; } = -1;
    public bool IsAsset => AssetKind != SpriteAssetKind.Group;
    public ObservableCollection<SpriteBrowserNode> Children { get; } = [];
}
