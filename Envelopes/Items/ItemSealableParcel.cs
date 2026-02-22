using System.Collections.Generic;
using System.IO;
using Envelopes.Database;
using Envelopes.Messages;
using Envelopes.Util;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Envelopes.Items;

public class ItemSealableParcel : ItemSealableContainer, IContainedInteractable
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

    public override string GetHeldItemName(ItemStack itemStack)
    {
        if (itemStack.Collectible?.Code?.Path == "parcel-empty"
            && !string.IsNullOrEmpty(itemStack.Attributes.GetString(EnvelopeAttributes.ContentsId)))
        {
            var contentsCode = itemStack.Attributes.GetString(EnvelopeAttributes.ContentsCode);
            if (!string.IsNullOrEmpty(contentsCode))
            {
                var loc = new AssetLocation(contentsCode);
                var stackSize = itemStack.Attributes.GetInt(EnvelopeAttributes.ContentsStackSize, 1);
                var countPrefix = stackSize > 1 ? $"{stackSize}x " : "";
                var item = api.World.GetItem(loc);
                if (item != null && !item.IsMissing)
                    return
                        $"{Lang.Get("envelopes:item-parcel-unsealed")} (contains {countPrefix}{item.GetHeldItemName(new ItemStack(item))})";
                var block = api.World.GetBlock(loc);
                if (block != null && !block.IsMissing)
                    return
                        $"{Lang.Get("envelopes:item-parcel-unsealed")} (contains {countPrefix}{block.GetHeldItemName(new ItemStack(block))})";
            }

            return Lang.Get("envelopes:item-parcel-unsealed");
        }

        return base.GetHeldItemName(itemStack);
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel,
        EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
        var code = slot.Itemstack?.Collectible?.Code?.Path;
        var contentsId = slot.Itemstack?.Attributes?.GetString(EnvelopeAttributes.ContentsId);

        if (code == "parcel-empty" && !string.IsNullOrEmpty(contentsId)
                                   && !byEntity.Controls.ShiftKey && !byEntity.Controls.CtrlKey)
        {
            handling = EnumHandHandling.Handled;
            if (api.Side == EnumAppSide.Client)
            {
                EnvelopesModSystem.ClientNetworkChannel?.SendPacket(
                    new OpenEnvelopePacket { ContentsId = contentsId });
            }

            return;
        }

        base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
    }

    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
    {
        var interactions = new List<WorldInteraction>();
        var code = inSlot.Itemstack?.Collectible?.Code?.Path;
        var hasContents = !string.IsNullOrEmpty(inSlot.Itemstack?.Attributes?.GetString(EnvelopeAttributes.ContentsId));

        if (code == "parcel-empty" && hasContents)
        {
            var parchment = api.World.GetItem(new AssetLocation("game:paper-parchment"));
            if (parchment != null)
            {
                interactions.Add(new WorldInteraction
                {
                    ActionLangCode = "envelopes:parcel-wrapwithparchment",
                    MouseButton = EnumMouseButton.Right,
                    HotKeyCodes = new[] { "shift", "ctrl" },
                    Itemstacks = new[] { new ItemStack(parchment) }
                });
            }
        }

        return interactions.ToArray().Append(base.GetHeldInteractionHelp(inSlot));
    }

    public bool OnContainedInteractStart(BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer,
        BlockSelection blockSel)
    {
        if (slot.Itemstack == null) return false;

        var code = slot.Itemstack.Collectible?.Code?.Path;
        var heldSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        if (heldSlot == null) return false;

        var heldItem = heldSlot.Itemstack;
        var contentsId = slot.Itemstack.Attributes.GetString(EnvelopeAttributes.ContentsId);
        var hasItem = !string.IsNullOrEmpty(contentsId);

        if (!hasItem && (code == "parcel-empty" || code == "parcel-opened"))
        {
            if (heldItem?.Collectible == null) return false;
            if (heldItem.Collectible is ItemSealableParcel) return false;

            var groundStorable = heldItem.Collectible.HasBehavior<CollectibleBehaviorGroundStorable>();
            if (!byPlayer.WorldData.EntityControls.Sneak) return groundStorable;

            if (be.Api.Side == EnumAppSide.Server)
            {
                var database = EnvelopesModSystem.EnvelopeDatabase;
                if (database != null)
                {
                    using var stream = new MemoryStream();
                    using var writer = new BinaryWriter(stream);
                    heldItem.ToBytes(writer);
                    var id = database.InsertEnvelope(new EnvelopeContents
                        { CreatorId = byPlayer.PlayerUID, ItemBlob = stream.ToArray() });

                    if (code != "parcel-empty")
                        slot.Itemstack =
                            new ItemStack(be.Api.World.GetItem(new AssetLocation("envelopes:parcel-empty")));

                    slot.Itemstack.Attributes.SetString(EnvelopeAttributes.ContentsId, id);
                    slot.Itemstack.Attributes.SetString(EnvelopeAttributes.ContentsCode,
                        heldItem.Collectible.Code.ToString());
                    slot.Itemstack.Attributes.SetInt(EnvelopeAttributes.ContentsStackSize, heldItem.StackSize);
                    slot.MarkDirty();
                    be.MarkDirty();
                    heldSlot.TakeOut(heldItem.StackSize);
                    heldSlot.MarkDirty();
                    be.Api.Event.EnqueueMainThreadTask(() =>
                        be.Api.World.PlaySoundAt(new AssetLocation("game:sounds/player/collect1"),
                            be.Pos.X + 0.5, be.Pos.Y + 0.5, be.Pos.Z + 0.5, null, true, 16f, 1f),
                        "parcel-sound");
                }
            }

            return true;
        }

        if (hasItem && heldItem == null && byPlayer.WorldData.EntityControls.Sneak &&
            (code == "parcel-empty" || code == "parcel-unsealed" || code == "parcel-opened"))
        {
            if (be.Api.Side == EnumAppSide.Server)
            {
                OpenContainer(slot, byPlayer);
                be.MarkDirty();
            }

            return true;
        }

        if (hasItem && (code == "parcel-empty" || code == "parcel-opened"))
        {
            if (heldItem?.Collectible == null) return false;

            var controls = byPlayer.WorldData.EntityControls;
            if (controls.Sneak && controls.CtrlKey && heldItem.Collectible.Code.Path == "paper-parchment")
            {
                if (be.Api.Side == EnumAppSide.Server)
                {
                    slot.Itemstack =
                        new ItemStack(be.Api.World.GetItem(new AssetLocation("envelopes:parcel-unsealed")));
                    slot.Itemstack.Attributes.SetString(EnvelopeAttributes.ContentsId, contentsId);
                    slot.MarkDirty();
                    be.MarkDirty();
                    heldSlot.TakeOut(1);
                    heldSlot.MarkDirty();
                    be.Api.Event.EnqueueMainThreadTask(() =>
                        be.Api.World.PlaySoundAt(new AssetLocation("game:sounds/held/bookclose*"),
                            be.Pos.X + 0.5, be.Pos.Y + 0.5, be.Pos.Z + 0.5, null, true, 16f, 1f),
                        "parcel-sound");
                }

                return true;
            }

            return heldItem.Collectible.HasBehavior<CollectibleBehaviorGroundStorable>();
        }

        return false;
    }

    public bool OnContainedInteractStep(float secondsUsed, BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer,
        BlockSelection blockSel) => false;

    public void OnContainedInteractStop(float secondsUsed, BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer,
        BlockSelection blockSel)
    {
    }
}