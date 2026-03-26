using Medabots.Rom.Events;

namespace Medabots.Rom.Editor;

public sealed class EventOperationOption
{
    public required EventOperationDefinition Definition { get; init; }

    public required string DisplayName { get; init; }

    public override string ToString() => DisplayName;
}
