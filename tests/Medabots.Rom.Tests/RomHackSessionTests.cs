using Medabots.Rom;
using Xunit;

namespace Medabots.Rom.Tests;

public sealed class RomHackSessionTests
{
    [Fact]
    public void ApplyPatch_AllowsOverlappingWritesWhenBytesMatch()
    {
        var session = RomHackSession.FromRomFile(new RomFile("overlap-ok.gba", new byte[64]));

        session.ApplyPatch(new RomPatchAction(0x10, [0x11, 0x22, 0x33, 0x44], "First"));
        session.ApplyPatch(new RomPatchAction(0x12, [0x33, 0x44], "Second"));

        Assert.Equal(2, session.AppliedActions.Count);
        Assert.Equal<byte>([0x11, 0x22, 0x33, 0x44], session.RomFile.ReadBytes(0x10, 4).ToArray());
    }

    [Fact]
    public void ApplyPatch_RejectsOverlappingWritesWhenBytesConflict()
    {
        var session = RomHackSession.FromRomFile(new RomFile("overlap-conflict.gba", new byte[64]));

        session.ApplyPatch(new RomPatchAction(0x10, [0x11, 0x22, 0x33, 0x44], "First"));
        var exception = Assert.Throws<InvalidOperationException>(() => session.ApplyPatch(new RomPatchAction(0x12, [0xAA, 0xBB], "Second")));

        Assert.Contains("Conflicting ROM patch overwrite", exception.Message);
        Assert.Contains("First", exception.Message);
        Assert.Contains("Second", exception.Message);
    }

    [Fact]
    public void ApplyPatch_AllowsConflictingOverwritesWithinSamePatchScope()
    {
        var session = RomHackSession.FromRomFile(new RomFile("overlap-same-scope.gba", new byte[64]));

        using (session.BeginPatchScope("Event Script"))
        {
            session.ApplyPatch(new RomPatchAction(0x10, [0x11, 0x22, 0x33, 0x44], "First"));
            session.ApplyPatch(new RomPatchAction(0x12, [0xAA, 0xBB], "Second"));
        }

        Assert.Equal<byte>([0x11, 0x22, 0xAA, 0xBB], session.RomFile.ReadBytes(0x10, 4).ToArray());
    }
}
