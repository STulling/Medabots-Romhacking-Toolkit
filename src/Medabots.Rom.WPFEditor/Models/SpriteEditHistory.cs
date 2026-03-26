namespace Medabots.Rom.WPFEditor.Models;

public sealed class SpriteEditHistory
{
    private readonly Stack<byte[]> _undoStack = new();

    public bool CanUndo => _undoStack.Count > 0;

    public void Push(byte[] pixels)
    {
        _undoStack.Push(pixels.ToArray());
    }

    public byte[] Pop()
    {
        return _undoStack.Pop();
    }

    public void Clear()
    {
        _undoStack.Clear();
    }
}
