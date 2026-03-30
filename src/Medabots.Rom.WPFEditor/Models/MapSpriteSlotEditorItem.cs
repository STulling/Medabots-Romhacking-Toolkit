using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;

namespace Medabots.Rom.WPFEditor.Models;

public sealed class MapSpriteSlotEditorItem
{
    public int SlotIndex { get; init; }

    public ObservableCollection<MapSpriteSlotOption> Options { get; } = [];

    public MapSpriteSlotOption? SelectedOption { get; set; }

    public string DisplayTitle { get; set; } = string.Empty;

    public BitmapSource? Thumbnail { get; set; }

    public string ResolvedDisplay { get; set; } = "Unused";
}
