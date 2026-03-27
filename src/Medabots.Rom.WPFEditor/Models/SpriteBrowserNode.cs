using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Medabots.Rom.WPFEditor.Models;

public sealed class SpriteBrowserNode : INotifyPropertyChanged
{
    private bool _isExpanded;

    public string Title { get; init; } = string.Empty;
    public string FilterText { get; init; } = string.Empty;
    public SpriteAssetKind AssetKind { get; init; }
    public int PrimaryId { get; init; } = -1;
    public int SecondaryId { get; init; } = -1;
    public bool IsAsset => AssetKind != SpriteAssetKind.Group;
    public ObservableCollection<SpriteBrowserNode> Children { get; } = [];

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value)
            {
                return;
            }

            _isExpanded = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
