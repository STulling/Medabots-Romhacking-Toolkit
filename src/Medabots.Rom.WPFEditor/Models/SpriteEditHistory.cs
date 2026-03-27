namespace Medabots.Rom.WPFEditor.Models;

public sealed class SpriteEditHistory
{
    private readonly Stack<SpriteEditSnapshot> _undoStack = new();

    public bool CanUndo => _undoStack.Count > 0;

    public void Push(byte[] pixels, byte[] palette)
    {
        _undoStack.Push(new SpriteEditSnapshot([new SpriteEditImageSnapshot(pixels, palette)]));
    }

    public void Push(IEnumerable<(byte[] Pixels, byte[] Palette)> images)
    {
        _undoStack.Push(new SpriteEditSnapshot(images.Select(image => new SpriteEditImageSnapshot(image.Pixels, image.Palette)).ToArray()));
    }

    public SpriteEditSnapshot Pop()
    {
        return _undoStack.Pop();
    }

    public void Clear()
    {
        _undoStack.Clear();
    }
}

public sealed record SpriteEditSnapshot(IReadOnlyList<SpriteEditImageSnapshot> Images);

public sealed record SpriteEditImageSnapshot
{
    public SpriteEditImageSnapshot(byte[] pixels, byte[] palette)
    {
        Pixels = pixels.ToArray();
        Palette = palette.ToArray();
    }

    public byte[] Pixels { get; }

    public byte[] Palette { get; }
}
