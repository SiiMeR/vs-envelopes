﻿using System.Collections.Generic;
using System.Text;
using Envelopes.Gui;
using Envelopes.Util;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace Envelopes.Items;

public class ItemWaxSealStamp : Item
{
    #region Render

    private Dictionary<string, MultiTextureMeshRef> Meshrefs =>
        ObjectCacheUtil.GetOrCreate(api, "stampmeshrefs",
            () => new Dictionary<string, MultiTextureMeshRef>());

    #endregion

    public override bool ConsumeCraftingIngredients(ItemSlot[] slots, ItemSlot outputSlot, GridRecipe matchingRecipe)
    {
        var code = outputSlot.Itemstack.Collectible.Code.Path;
        if (code.Contains("sealstamp-engraved") && outputSlot.Itemstack.Attributes.TryGetInt("durability").HasValue)
        {
            outputSlot.Itemstack.Attributes.SetInt("durability", 200);
        }

        return base.ConsumeCraftingIngredients(slots, outputSlot, matchingRecipe);
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel,
        EntitySelection entitySel,
        bool firstEvent, ref EnumHandHandling handling)
    {
        if (api.Side == EnumAppSide.Server)
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            return;
        }

        if (byEntity.Controls.ShiftKey)
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            return;
        }


        var alreadyEngraved = slot.Itemstack.Collectible.Code.Path.Contains("engraved");
        if (firstEvent && !alreadyEngraved)
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
        var stampTitle = attributes.GetString(StampAttributes.StampTitle);
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
        var stampId = attributes.TryGetLong(StampAttributes.StampId);
        if (stampId != null)
        {
            dsc.AppendLine($"<font color=\"orange\">ID: {stampId}</font>");
        }
    }
}