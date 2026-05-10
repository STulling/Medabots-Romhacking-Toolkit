namespace Medabots.Rom.Shops;

public sealed class ShopPatcher
{
    public RomPatchAction BuildPatch(ShopDefinition shop)
    {
        ArgumentNullException.ThrowIfNull(shop);
        return RomPatchAction.Create(shop.DataOffset, shop.Contents, $"Update shop {shop.Id}");
    }

    public void Apply(RomHackSession session, ShopDefinition shop)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(shop);
        session.ApplyPatch(BuildPatch(shop));
    }
}
