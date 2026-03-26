using Medabots.Rom.Parts;
using Xunit;

namespace Medabots.Rom.Tests;

public sealed class PartDefinitionTests
{
    [Fact]
    public void CombatParts_ExposeCombatStats()
    {
        var part = new PartDefinition(
            0,
            0,
            PartKind.Head,
            0x100,
            1,
            2,
            3,
            4,
            5,
            6,
            7,
            8,
            9,
            10,
            11,
            12,
            13,
            14,
            15,
            16);

        var stats = part.AsCombatPartStats();

        Assert.Equal((byte)2, stats.Technique);
        Assert.Equal((byte)6, stats.Success);
        Assert.Equal((byte)7, stats.Power);
        Assert.Equal((byte)8, stats.ChargeOrChainReaction);
        Assert.Equal((byte)9, stats.Uses);
        Assert.Throws<InvalidOperationException>(() => part.AsLegPartStats());
    }

    [Fact]
    public void LegParts_ExposeMovementStats()
    {
        var part = new PartDefinition(
            3,
            0,
            PartKind.Legs,
            0x200,
            1,
            12,
            3,
            4,
            5,
            16,
            17,
            18,
            19,
            10,
            11,
            12,
            13,
            14,
            15,
            16);

        var stats = part.AsLegPartStats();

        Assert.Equal((byte)12, stats.LegType);
        Assert.Equal((byte)16, stats.Propulsion);
        Assert.Equal((byte)17, stats.Evasion);
        Assert.Equal((byte)18, stats.Defense);
        Assert.Equal((byte)19, stats.Conceal);
        Assert.Throws<InvalidOperationException>(() => part.AsCombatPartStats());
    }
}
