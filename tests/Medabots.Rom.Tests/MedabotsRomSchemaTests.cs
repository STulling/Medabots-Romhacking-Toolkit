using Medabots.Rom.Metadata;
using Xunit;

namespace Medabots.Rom.Tests;

public sealed class MedabotsRomSchemaTests
{
    [Fact]
    public void Profiles_ExposeTypedAddressGroups()
    {
        var profile = MedabotsRomTextProfiles.FindById("MEDABOTSRKSVA9BPE9");

        Assert.NotNull(profile);
        Assert.Equal(0x47DF44, profile!.Addresses.TextPointerTableOffset);
        Assert.Equal(0x7F5500, profile.Addresses.TextDumpOffset);
        Assert.Equal(0x7852F4, profile.Addresses.StarterOffset);
        Assert.Equal(0x3C1BA0, profile.Addresses.BattlePointerTableOffset);
        Assert.Equal(0x769EE4, profile.Addresses.EventTableOffset);
    }

    [Fact]
    public void SharedSchema_StarterLocatorAppliesAdjustment()
    {
        var rom = new byte[64];
        var signature = MedabotsRomSchema.StarterMedalSlot.Signature;
        Array.Copy(signature, 0, rom, 10, signature.Length);

        var offset = MedabotsRomSchema.StarterMedalSlot.Locate(rom);

        Assert.Equal(9, offset);
    }

    [Fact]
    public void SharedSchema_ExposesCoreFormatConstants()
    {
        Assert.Equal(16, MedabotsRomSchema.MessageBankCount);
        Assert.Equal(0x11E0, MedabotsRomSchema.EventBankTableOffset);
        Assert.Equal(0x4000, MedabotsRomSchema.EventBankSize);
        Assert.Equal(0x28, MedabotsRomSchema.BattleSize);
        Assert.Equal(3, MedabotsRomSchema.BattleBotCount);
    }
}
