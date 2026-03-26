namespace Medabots.Rom.Shops;

public sealed class ShopPatcher
{
    public void Apply(RomHackSession session, ShopDefinition shop)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(shop);
        session.ApplyPatch(RomPatchAction.Create(shop.DataOffset, shop.Contents, $"Update shop {shop.Id}"));
    }
}
