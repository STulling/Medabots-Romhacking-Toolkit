namespace Medabots.Rom.WPFEditor.Models;

public sealed class SpriteEditHistory
{
    private readonly Stack<(byte[] Pixels, byte[] Palette)> _undoStack = new();

    public bool CanUndo => _undoStack.Count > 0;

    public void Push(byte[] pixels, byte[] palette)
    {
        _undoStack.Push((pixels.ToArray(), palette.ToArray()));
    }

    public (byte[] Pixels, byte[] Palette) Pop()
    {
        return _undoStack.Pop();
    }

    public void Clear()
    {
        _undoStack.Clear();
    }
}
