namespace Medabots.Rom.Events;

public sealed record EventOperationDefinition(byte Opcode, string Name, IReadOnlyList<EventArgumentDefinition> Arguments)
{
    public int Size => 1 + Arguments.Sum(argument => argument.Size);

    public bool HasJumpArgument => Arguments.Any(argument => string.Equals(argument.Name, "jump", StringComparison.Ordinal));
}
