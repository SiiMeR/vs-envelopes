using Vintagestory.API.Common;

namespace Envelopes.Items;

public class ItemSealableEnvelope : ItemSealableContainer
{
    protected override string GetEmptyItemCode() => "envelopes:envelope-empty";
    protected override string GetUnsealedItemCode() => "envelopes:envelope-unsealed";
    protected override string GetSealedItemCode() => "envelopes:envelope-sealed";
    protected override string GetOpenedItemCode() => "envelopes:envelope-opened";
    protected override string GetContainerType() => "envelope";

    protected override bool CanContainItem(ItemSlot itemSlot)
    {
        var code = itemSlot?.Itemstack?.Collectible?.Code?.Path;
        return code != null && (code.Contains("parchment") || code.Contains("book"));
    }
}
