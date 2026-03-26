using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Medabots.Rom.Editor;

public sealed class EventBrowserItem : INotifyPropertyChanged
{
    private string _summary = "Not loaded";
    private bool _isCached;
    private bool _isPatched;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int Id { get; init; }

    public string Summary
    {
        get => _summary;
        set
        {
            if (string.Equals(_summary, value, StringComparison.Ordinal))
            {
                return;
            }

            _summary = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FilterText));
        }
    }

    public bool IsCached
    {
        get => _isCached;
        set
        {
            if (_isCached == value)
            {
                return;
            }

            _isCached = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CacheStatus));
            OnPropertyChanged(nameof(FilterText));
        }
    }

    public bool IsPatched
    {
        get => _isPatched;
        set
        {
            if (_isPatched == value)
            {
                return;
            }

            _isPatched = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PatchStatus));
            OnPropertyChanged(nameof(FilterText));
        }
    }

    public string CacheStatus => IsCached ? "Cached" : string.Empty;

    public string PatchStatus => IsPatched ? "Patched" : string.Empty;

    public string FilterText => $"{Id} {Summary} {CacheStatus} {PatchStatus}";

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
