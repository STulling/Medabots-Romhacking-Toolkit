using System.ComponentModel;
using System.Runtime.CompilerServices;
using Medabots.Rom.Text;

namespace Medabots.Rom.Editor;

public sealed class MessagePatchItem : INotifyPropertyChanged
{
    private int _bank;
    private int _index;
    private string _text = "<END:0>";
    private string _originalText = "<END:0>";

    public event PropertyChangedEventHandler? PropertyChanged;

    public int Bank
    {
        get => _bank;
        set
        {
            if (_bank == value)
            {
                return;
            }

            _bank = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    public int Index
    {
        get => _index;
        set
        {
            if (_index == value)
            {
                return;
            }

            _index = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    public string Text
    {
        get => _text;
        set
        {
            if (string.Equals(_text, value, StringComparison.Ordinal))
            {
                return;
            }

            _text = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Preview));
            OnPropertyChanged(nameof(IsModified));
        }
    }

    public string OriginalText
    {
        get => _originalText;
        set
        {
            if (string.Equals(_originalText, value, StringComparison.Ordinal))
            {
                return;
            }

            _originalText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsModified));
        }
    }

    public string DisplayName => $"Bank {Bank}, Message {Index}";

    public string Preview => Text.Length <= 48 ? Text : $"{Text[..48]}...";

    public bool IsModified => !string.Equals(Text, OriginalText, StringComparison.Ordinal);

    public MessagePatch ToPatch() => new(new MessageId(Bank, Index), Text);

    public static MessagePatchItem FromPatch(MessagePatch patch)
    {
        return new MessagePatchItem
        {
            Bank = patch.Id.Bank,
            Index = patch.Id.Index,
            Text = patch.Text,
            OriginalText = patch.Text
        };
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
