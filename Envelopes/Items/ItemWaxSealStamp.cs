using System.Text;
using Envelopes.Gui;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace Envelopes.Items;

public class ItemWaxSealStamp : Item
{
    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel,
        EntitySelection entitySel,
        bool firstEvent, ref EnumHandHandling handling)
    {
        if (api.Side == EnumAppSide.Server)
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            return;
        }

        var hasStampId = slot.Itemstack?.Attributes.HasAttribute("StampId") ?? false;
        if (firstEvent && !hasStampId)
        {
            var dialog = new GuiSealStampDesigner(EnvelopesModSystem.Api as ICoreClientAPI);
            dialog.TryOpen(true);
            handling = EnumHandHandling.PreventDefault;
        }

        base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
    }

    public override string GetHeldItemName(ItemStack itemStack)
    {
        var heldItemName = base.GetHeldItemName(itemStack);

        var attributes = itemStack.Attributes;
        var stampTitle = attributes.GetString("StampTitle");
        if (stampTitle == null)
        {
            return heldItemName;
        }

        heldItemName += $" (“{stampTitle}”)";

        return heldItemName;
    }

    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
    {
        base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

        var attributes = inSlot.Itemstack.Attributes;
        var stampId = attributes.TryGetLong("StampId");
        if (stampId != null)
        {
            dsc.AppendLine($"<font color=\"orange\">ID: {stampId}</font>");
        }
    }
}