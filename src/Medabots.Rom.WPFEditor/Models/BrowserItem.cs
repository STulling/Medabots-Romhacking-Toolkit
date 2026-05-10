using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace Medabots.Rom.Editor;

public sealed class BrowserItem(int id, string title) : INotifyPropertyChanged
{
    private bool _isModified;
    private string _title = title;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int Id { get; } = id;

    public string Title
    {
        get => _title;
        set
        {
            if (_title == value)
            {
                return;
            }

            _title = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FilterText));
        }
    }

    public bool IsModified
    {
        get => _isModified;
        set
        {
            if (_isModified == value)
            {
                return;
            }

            _isModified = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TextColor));
        }
    }

    public System.Windows.Media.Color TextColor => IsModified ? Colors.Red : Colors.Black;

    public string FilterText => $"{Id} {Title}";

    public override string ToString() => Title;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
