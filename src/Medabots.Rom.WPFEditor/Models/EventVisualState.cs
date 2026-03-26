namespace Medabots.Rom.Editor;

public sealed class EventVisualState
{
    public required IReadOnlyList<EventInstructionItem> Instructions { get; init; }

    public required IReadOnlyDictionary<int, string> LabelMap { get; init; }

    public required IReadOnlyList<string> OrderedLabels { get; init; }
}
