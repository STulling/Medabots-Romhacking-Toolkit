namespace Medabots.Rom.Events;

public sealed record EventArgumentValue(string Name, EventArgumentType Type, int RawValue, string DisplayValue);
