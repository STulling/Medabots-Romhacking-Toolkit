namespace Medabots.Rom.Battles;

public sealed record BattleActionRouteDefinition(
    byte ActionId,
    string FamilyHandler,
    string FamilySummary,
    string? FamilySubsequence,
    string? SharedScriptName,
    string? SharedScriptSummary,
    IReadOnlyList<byte> KnownOpcodeSequence,
    IReadOnlyList<string> ActualFlow,
    IReadOnlyList<string> Notes);
