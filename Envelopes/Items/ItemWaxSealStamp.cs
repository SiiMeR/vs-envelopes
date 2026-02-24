using System.Linq;
using System.Text;
using Envelopes.Behaviors;
using Envelopes.Gui;
using Envelopes.Messages;
using Envelopes.Util;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Envelopes.Items;

public class ItemWaxSealStamp : Item
{
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
        EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
        var alreadyEngraved = slot.Itemstack.Collectible.Code.Path.Contains("engraved");

        if (alreadyEngraved && firstEvent
            && byEntity.LeftHandItemSlot?.Itemstack?.Collectible?.Code?.Path?.StartsWith("waxstick") == true
            && blockSel != null && GetUnsealedContainerSlot(blockSel.Position) != null)
        {
            handling = EnumHandHandling.PreventDefault;
            return;
        }

        if (firstEvent && !alreadyEngraved && api.Side != EnumAppSide.Server)
        {
            var dialog = new GuiSealStampDesigner(EnvelopesModSystem.Api as ICoreClientAPI);
            dialog.TryOpen(true);
            handling = EnumHandHandling.PreventDefault;
        }

        base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
    }

    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity,
        BlockSelection blockSel, EntitySelection entitySel)
    {
        if (!slot.Itemstack.Collectible.Code.Path.Contains("engraved")) return false;
        if (byEntity.LeftHandItemSlot?.Itemstack?.Collectible?.Code?.Path?.StartsWith("waxstick") != true) return false;
        if (blockSel == null) return false;
        return GetUnsealedContainerSlot(blockSel.Position) != null && secondsUsed < 1.0f;
    }

    public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity,
        BlockSelection blockSel, EntitySelection entitySel)
    {
        if (secondsUsed < 1.0f) return;
        if (api.Side != EnumAppSide.Server) return;
        if (blockSel == null) return;

        var targetSlot = GetUnsealedContainerSlot(blockSel.Position);
        if (targetSlot == null) return;

        var contentsId = targetSlot.Itemstack.Attributes.GetString(EnvelopeAttributes.ContentsId);

        var waxSlot = byEntity.LeftHandItemSlot;
        if (waxSlot?.Itemstack?.Collectible?.Code?.Path?.StartsWith("waxstick") != true) return;

        var waxColor = waxSlot.Itemstack.Collectible.Attributes["color"].AsString();
        var unsealedCode = targetSlot.Itemstack.Collectible.Code;
        var sealedCode = new AssetLocation(unsealedCode.Domain, unsealedCode.Path.Replace("-unsealed", "-sealed"));
        var nextStack = new ItemStack(api.World.GetItem(sealedCode));
        (targetSlot.Itemstack.Collectible as ItemSealableContainer)?.CopyContainerAttributes(targetSlot.Itemstack, nextStack);
        nextStack.Attributes.SetString(EnvelopeAttributes.ContentsId, contentsId);
        nextStack.Attributes.SetString(EnvelopeAttributes.WaxColor, waxColor);

        var stampId = slot.Itemstack.Attributes.TryGetLong(StampAttributes.StampId);
        if (stampId.HasValue) nextStack.Attributes.SetLong(StampAttributes.StampId, stampId.Value);
        var stampTitle = slot.Itemstack.Attributes.GetString(StampAttributes.StampTitle);
        if (!string.IsNullOrEmpty(stampTitle)) nextStack.Attributes.SetString(StampAttributes.StampTitle, stampTitle);
        var stampDesign = slot.Itemstack.Attributes.GetString(StampAttributes.StampDesign);
        if (!string.IsNullOrEmpty(stampDesign))
            nextStack.Attributes.SetString(StampAttributes.StampDesign, stampDesign);

        waxSlot.Itemstack.Collectible.DamageItem(api.World, byEntity, waxSlot, 1);
        waxSlot.MarkDirty();

        targetSlot.Itemstack = nextStack;
        targetSlot.MarkDirty();

        (api.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityContainer)?.MarkDirty();

        api.World.PlaySoundAt(new AssetLocation("game:sounds/held/bookclose*"), byEntity, null, true, 16f, 1f);
    }

    private ItemSlot? GetUnsealedContainerSlot(BlockPos pos)
    {
        var be = api.World.BlockAccessor.GetBlockEntity<BlockEntityGroundStorage>(pos);
        return be?.Inventory.FirstOrDefault(s =>
            s.Itemstack?.Collectible is ItemSealableContainer
            && s.Itemstack.Collectible.Code?.Path?.Contains("-unsealed") == true);
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

        if (!string.IsNullOrEmpty(stampTitle) && !attributes.HasAttribute(StampAttributes.StampDesign))
        {
            EnvelopesModSystem.ClientNetworkChannel?.SendPacket(new AddStampDesignAttributePacket());
            RenderStampEmblem.InvalidateMeshCacheKey(itemStack);
        }

        heldItemName += $" (\"{stampTitle}\")";

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
