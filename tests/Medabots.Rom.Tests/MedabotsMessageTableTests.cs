using System.Buffers.Binary;
using Medabots.Rom.Text;
using Xunit;

namespace Medabots.Rom.Tests;

public sealed class MedabotsMessageTableTests
{
    [Fact]
    public void Reader_LoadsMessagesFromPointerTable()
    {
        var rom = new byte[256];

        WritePointer(rom, 0x10, 0x40);
        WritePointer(rom, 0x40, 0x60);
        WritePointer(rom, 0x44, 0x70);

        rom[0x48] = 0x00;
        rom[0x49] = 0x00;
        rom[0x4A] = 0x00;
        rom[0x4B] = 0x00;

        Array.Copy(new byte[] { 0x01, 0x02, 0xFF, 0x00 }, 0, rom, 0x60, 4);
        Array.Copy(new byte[] { 0x03, 0x04, 0xFD, 0xFF, 0x00 }, 0, rom, 0x70, 5);

        var reader = new MedabotsMessageTableReader();
        var romFile = new RomFile("test.gba", rom);

        var messages = reader.ReadAll(romFile, 0x10, bankCount: 1);

        Assert.Equal("AB<END:0>", messages[new MessageId(0, 0)]);
        Assert.Equal("CD<NL><END:0>", messages[new MessageId(0, 1)]);
    }

    [Fact]
    public void Patcher_BuildsDeterministicWriteAndPointerActions()
    {
        var rom = new byte[256];
        WritePointer(rom, 0x10, 0x40);
        WritePointer(rom, 0x40, 0x60);
        WritePointer(rom, 0x44, 0x70);

        var romFile = new RomFile("test.gba", rom);
        var session = CreateSession(romFile);
        var patcher = new MedabotsTextPatcher();

        patcher.Apply(
            session,
            pointerTableOffset: 0x10,
            dumpOffset: 0x80,
            patches:
            [
                new MessagePatch(new MessageId(0, 1), "BC<END:0>"),
                new MessagePatch(new MessageId(0, 0), "A<END:0>")
            ]);

        Assert.Equal([0x01, 0xFF, 0x00], romFile.ReadBytes(0x80, 3).ToArray());
        Assert.Equal([0x02, 0x03, 0xFF, 0x00], romFile.ReadBytes(0x83, 4).ToArray());
        Assert.Equal((uint)0x08000080, ReadUInt32(romFile.Data, 0x40));
        Assert.Equal((uint)0x08000083, ReadUInt32(romFile.Data, 0x44));
    }

    private static RomHackSession CreateSession(RomFile romFile)
    {
        var constructor = typeof(RomHackSession).GetConstructors(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Single(constructor => constructor.GetParameters() is [{ ParameterType: var parameterType }] && parameterType == typeof(RomFile));

        return (RomHackSession)constructor.Invoke([romFile]);
    }

    private static void WritePointer(byte[] buffer, int offset, int fileOffset)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(offset, sizeof(uint)), GbaPointer.ToRomAddress(fileOffset));
    }

    private static uint ReadUInt32(byte[] buffer, int offset)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(offset, sizeof(uint)));
    }
}
