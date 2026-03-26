using Medabots.Rom.Metadata;

namespace Medabots.Rom.Shops;

public sealed class ShopTableReader
{
    public int FindTableOffset(RomFile romFile)
    {
        ArgumentNullException.ThrowIfNull(romFile);
        return MedabotsRomSchema.ShopTable.Locate(romFile.Data);
    }

    public ShopDefinition Read(RomFile romFile, int shopId, int entryLength)
    {
        ArgumentNullException.ThrowIfNull(romFile);
        ArgumentOutOfRangeException.ThrowIfNegative(shopId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(entryLength);

        var tableOffset = FindTableOffset(romFile);
        var dataOffset = tableOffset + (shopId * entryLength);
        return new ShopDefinition(shopId, dataOffset, romFile.ReadBytes(dataOffset, entryLength).ToArray());
    }
}
