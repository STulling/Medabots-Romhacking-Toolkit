namespace Medabots.Rom.Parts;

public sealed record PartDefinition(
    int Id,
    int MedabotId,
    PartKind Kind,
    int DataOffset,
    byte MedalCompatibility,
    byte TechniqueOrLegType,
    byte Speciality,
    byte Gender,
    byte Armor,
    byte RateOfSuccessOrPropulsion,
    byte PowerOrEvasion,
    byte ChainReactionOrDefense,
    byte AmountOfUsesOrProximity,
    byte Unknown2,
    byte Unknown3,
    byte Unknown4,
    byte Unknown5,
    byte Unknown6,
    byte Unknown7,
    byte Unknown8)
{
    public bool IsLegPart => Kind == PartKind.Legs;

    public bool IsCombatPart => !IsLegPart;

    public CombatPartStats AsCombatPartStats()
    {
        if (!IsCombatPart)
        {
            throw new InvalidOperationException("Leg parts use movement stats instead of combat stats.");
        }

        return new CombatPartStats(
            TechniqueOrLegType,
            RateOfSuccessOrPropulsion,
            PowerOrEvasion,
            ChainReactionOrDefense,
            AmountOfUsesOrProximity);
    }

    public LegPartStats AsLegPartStats()
    {
        if (!IsLegPart)
        {
            throw new InvalidOperationException("Head and arm parts use combat stats instead of leg movement stats.");
        }

        return new LegPartStats(
            TechniqueOrLegType,
            RateOfSuccessOrPropulsion,
            PowerOrEvasion,
            ChainReactionOrDefense,
            AmountOfUsesOrProximity);
    }
}
