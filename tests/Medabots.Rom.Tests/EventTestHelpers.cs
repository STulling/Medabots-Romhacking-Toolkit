using Medabots.Rom.Events;
using Xunit;

namespace Medabots.Rom.Tests;

internal static class EventTestHelpers
{
    public static EventOperationDefinition AssertDefinition(EventOperationRegistry registry, byte opcode)
    {
        Assert.True(registry.TryGetDefinition(opcode, out var definition));
        return definition;
    }
}
