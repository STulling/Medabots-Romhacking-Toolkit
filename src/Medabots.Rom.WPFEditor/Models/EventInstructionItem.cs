using Medabots.Rom.Events;

namespace Medabots.Rom.Editor;

public sealed class EventInstructionItem
{
    public EventInstruction? Instruction { get; init; }

    public int Order { get; init; }

    public int Offset { get; init; }

    public byte Opcode { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Kind { get; init; } = string.Empty;

    public string OffsetDisplay { get; init; } = string.Empty;

    public string LabelDisplay { get; init; } = string.Empty;

    public bool HasLabelDisplay => !string.IsNullOrWhiteSpace(LabelDisplay);

    public string Category { get; init; } = string.Empty;

    public bool HasCategory => !string.IsNullOrWhiteSpace(Category);

    public string CategoryBackgroundColor { get; init; } = "#E5E7EB";

    public string CategoryTextColor { get; init; } = "#374151";

    public string AccentColor { get; init; } = "#D1D5DB";

    public string Summary { get; init; } = string.Empty;

    public string Detail { get; init; } = string.Empty;

    public bool HasDetail => !string.IsNullOrWhiteSpace(Detail);

    public bool IsEditable { get; init; }

    public IReadOnlyList<EventArgumentValue> Arguments { get; init; } = [];
}
