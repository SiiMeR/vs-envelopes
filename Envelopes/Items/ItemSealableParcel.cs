using Vintagestory.API.Common;

namespace Envelopes.Items;

public class ItemSealableParcel : ItemSealableContainer
{
    protected override string GetEmptyItemCode() => "envelopes:parcel-empty";
    protected override string GetUnsealedItemCode() => "envelopes:parcel-unsealed";
    protected override string GetSealedItemCode() => "envelopes:parcel-sealed";
    protected override string GetOpenedItemCode() => "envelopes:parcel-opened";
    protected override string GetContainerType() => "parcel";

    protected override bool CanContainItem(ItemSlot itemSlot)
    {
        return itemSlot?.Itemstack?.Collectible != null;
    }
}
