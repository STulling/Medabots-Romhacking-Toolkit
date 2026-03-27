using Medabots.Rom.Events;
using Xunit;

namespace Medabots.Rom.Tests;

public sealed class EventRegistryTests
{
    [Fact]
    public void Registry_LoadsKnownOperations()
    {
        var registry = EventOperationRegistry.LoadDefault();

        Assert.True(registry.TryGetDefinition(0x01, out var definition));
        Assert.Equal("Show_Message_A", definition.Name);
        Assert.Equal(2, definition.Arguments.Count);
        Assert.Equal(EventArgumentType.EventBank, definition.Arguments[0].Type);
        Assert.Equal(EventArgumentType.Short, definition.Arguments[1].Type);
    }
}
