using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Envelopes.Database;
using Envelopes.Messages;
using Envelopes.Util;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

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

    public override string GetHeldItemName(ItemStack itemStack)
    {
        if (itemStack.Collectible?.Code?.Path == "parcel-empty")
        {
            var blob = itemStack.Attributes.GetBytes(EnvelopeAttributes.VisibleContent);
            if (blob?.Length > 0)
            {
                try
                {
                    using var ms = new MemoryStream(blob);
                    using var br = new BinaryReader(ms);
                    var contained = new ItemStack(br, api.World);
                    if (contained.Collectible != null && !contained.Collectible.IsMissing)
                    {
                        var prefix = contained.StackSize > 1 ? $"{contained.StackSize}x " : "";
                        return
                            $"{Lang.Get("envelopes:item-parcel-unsealed")} (contains {prefix}{contained.Collectible.GetHeldItemName(contained)})";
                    }
                }
                catch
                {
                }
            }

            if (!string.IsNullOrEmpty(itemStack.Attributes.GetString(EnvelopeAttributes.ContentsId)))
                return Lang.Get("envelopes:item-parcel-unsealed");
        }

        return base.GetHeldItemName(itemStack);
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel,
        EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
        var code = slot.Itemstack?.Collectible?.Code?.Path;
        var contentsId = slot.Itemstack?.Attributes?.GetString(EnvelopeAttributes.ContentsId);
        var hasVisibleContent = slot.Itemstack?.Attributes?.GetBytes(EnvelopeAttributes.VisibleContent)?.Length > 0;

        if (code == "parcel-empty" && !byEntity.Controls.ShiftKey && !byEntity.Controls.CtrlKey
            && (!string.IsNullOrEmpty(contentsId) || hasVisibleContent))
        {
            handling = EnumHandHandling.Handled;
            if (api.Side == EnumAppSide.Client)
            {
                EnvelopesModSystem.ClientNetworkChannel?.SendPacket(
                    new OpenEnvelopePacket { ContentsId = contentsId ?? "" });
            }

            return;
        }

        base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
    }

    public override void OpenContainer(ItemSlot slot, IPlayer opener)
    {
        if (opener.Entity.World.Side != EnumAppSide.Server) return;

        var container = slot.Itemstack;
        if (container == null) return;

        var codePath = container.Collectible.Code.Path;
        var emptyPath = new AssetLocation(GetEmptyItemCode()).Path;
        var openedPath = new AssetLocation(GetOpenedItemCode()).Path;

        if (codePath == emptyPath || codePath == openedPath)
        {
            var blob = container.Attributes.GetBytes(EnvelopeAttributes.VisibleContent);
            var contentsId = container.Attributes.GetString(EnvelopeAttributes.ContentsId);

            if (blob?.Length > 0 && string.IsNullOrEmpty(contentsId))
            {
                using var ms = new MemoryStream(blob);
                using var br = new BinaryReader(ms);
                ItemStack itemstack;
                try
                {
                    itemstack = new ItemStack(br, api.World);
                }
                catch
                {
                    ms.Seek(0, SeekOrigin.Begin);
                    itemstack = new ItemStack(api.World.GetItem(new AssetLocation("game:paper-parchment")));
                    itemstack.Attributes.FromBytes(br);
                }

                if (!opener.InventoryManager.TryGiveItemstack(itemstack, true))
                    api.World.SpawnItemEntity(itemstack, opener.Entity.SidedPos.XYZ);

                container.Attributes.RemoveAttribute(EnvelopeAttributes.VisibleContent);
                api.Event.EnqueueMainThreadTask(() =>
                        api.World.PlaySoundAt(new AssetLocation("game:sounds/held/bookclose*"), opener.Entity, null,
                            true, 16f, 1f),
                    "envelope-sound");
                slot.MarkDirty();
                return;
            }

            base.OpenContainer(slot, opener);
            if (container.Attributes.GetString(EnvelopeAttributes.ContentsId) == null)
            {
                container.Attributes.RemoveAttribute(EnvelopeAttributes.VisibleContent);
                slot.MarkDirty();
            }

            return;
        }

        var globalApi = EnvelopesModSystem.Api;
        if (globalApi == null) return;

        var id = container.Attributes.GetString(EnvelopeAttributes.ContentsId);
        var nextItem = new ItemStack(globalApi.World.GetItem(new AssetLocation(GetEmptyItemCode())));

        if (!string.IsNullOrEmpty(id))
        {
            var dbBlob = EnvelopesModSystem.EnvelopeDatabase?.GetEnvelope(id)?.ItemBlob;
            if (dbBlob != null)
                nextItem.Attributes.SetBytes(EnvelopeAttributes.VisibleContent, dbBlob);
        }

        globalApi.Event.EnqueueMainThreadTask(() =>
                globalApi.World.PlaySoundAt(new AssetLocation("game:sounds/held/bookclose*"), opener.Entity, null, true,
                    16f, 1f),
            "envelope-sound");
        slot.Itemstack = nextItem;
        slot.MarkDirty();
    }

    public override bool ConsumeCraftingIngredients(ItemSlot[] slots, ItemSlot outputSlot, GridRecipe matchingRecipe)
    {
        if (api.Side == EnumAppSide.Server
            && outputSlot.Itemstack?.Collectible?.Code?.Path?.Contains("unsealed") == true)
        {
            var blob = outputSlot.Itemstack.Attributes.GetBytes(EnvelopeAttributes.VisibleContent);
            if (blob?.Length > 0 &&
                string.IsNullOrEmpty(outputSlot.Itemstack.Attributes.GetString(EnvelopeAttributes.ContentsId)))
            {
                var ownerId = slots.FirstOrDefault(s => !s.Empty)?.Inventory?.openedByPlayerGUIds?.FirstOrDefault();
                var id = EnvelopesModSystem.EnvelopeDatabase?.InsertEnvelope(new EnvelopeContents
                    { CreatorId = ownerId ?? "unknown", ItemBlob = blob });
                if (id != null)
                {
                    outputSlot.Itemstack.Attributes.SetString(EnvelopeAttributes.ContentsId, id);
                    outputSlot.Itemstack.Attributes.RemoveAttribute(EnvelopeAttributes.VisibleContent);
                }
            }
        }

        return base.ConsumeCraftingIngredients(slots, outputSlot, matchingRecipe);
    }

    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
    {
        var interactions = new List<WorldInteraction>();
        var code = inSlot.Itemstack?.Collectible?.Code?.Path;
        var hasContents = !string.IsNullOrEmpty(inSlot.Itemstack?.Attributes?.GetString(EnvelopeAttributes.ContentsId))
                          || inSlot.Itemstack?.Attributes?.GetBytes(EnvelopeAttributes.VisibleContent)?.Length > 0;

        if (code == "parcel-empty")
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

    public override bool OnContainedInteractStart(BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer,
        BlockSelection blockSel)
    {
        if (slot.Itemstack == null) return false;

        var code = slot.Itemstack.Collectible?.Code?.Path;
        var heldSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        if (heldSlot == null) return false;

        var heldItem = heldSlot.Itemstack;
        var visibleContent = slot.Itemstack.Attributes.GetBytes(EnvelopeAttributes.VisibleContent);
        var contentsId = slot.Itemstack.Attributes.GetString(EnvelopeAttributes.ContentsId);
        var hasItem = visibleContent?.Length > 0 || !string.IsNullOrEmpty(contentsId);

        if (!hasItem && (code == "parcel-empty" || code == "parcel-opened"))
        {
            if (heldItem?.Collectible == null) return false;
            if (heldItem.Collectible is ItemSealableParcel) return false;

            var groundStorable = heldItem.Collectible.HasBehavior<CollectibleBehaviorGroundStorable>();
            if (!byPlayer.WorldData.EntityControls.Sneak) return groundStorable;

            if (be.Api.Side == EnumAppSide.Server)
            {
                if (code != "parcel-empty")
                    slot.Itemstack = new ItemStack(be.Api.World.GetItem(new AssetLocation("envelopes:parcel-empty")));

                using var stream = new MemoryStream();
                using var writer = new BinaryWriter(stream);
                heldItem.ToBytes(writer);
                slot.Itemstack.Attributes.SetBytes(EnvelopeAttributes.VisibleContent, stream.ToArray());
                slot.MarkDirty();
                be.MarkDirty();
                heldSlot.TakeOut(heldItem.StackSize);
                heldSlot.MarkDirty();
                be.Api.Event.EnqueueMainThreadTask(() =>
                        be.Api.World.PlaySoundAt(new AssetLocation("game:sounds/player/collect1"),
                            be.Pos.X + 0.5, be.Pos.Y + 0.5, be.Pos.Z + 0.5, null, true, 16f, 1f),
                    "parcel-sound");
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
                    var database = EnvelopesModSystem.EnvelopeDatabase;
                    var prev = slot.Itemstack;
                    if (visibleContent?.Length > 0 && database != null)
                    {
                        var id = database.InsertEnvelope(new EnvelopeContents
                            { CreatorId = byPlayer.PlayerUID, ItemBlob = visibleContent });
                        slot.Itemstack =
                            new ItemStack(be.Api.World.GetItem(new AssetLocation("envelopes:parcel-unsealed")));
                        slot.Itemstack.Attributes.SetString(EnvelopeAttributes.ContentsId, id);
                    }
                    else if (!string.IsNullOrEmpty(contentsId))
                    {
                        slot.Itemstack =
                            new ItemStack(be.Api.World.GetItem(new AssetLocation("envelopes:parcel-unsealed")));
                        slot.Itemstack.Attributes.SetString(EnvelopeAttributes.ContentsId, contentsId);
                    }
                    else
                    {
                        return true;
                    }

                    CopyContainerAttributes(prev, slot.Itemstack);
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

    public override bool OnContainedInteractStep(float secondsUsed, BlockEntityContainer be, ItemSlot slot,
        IPlayer byPlayer, BlockSelection blockSel) => false;

    public override void OnContainedInteractStop(float secondsUsed, BlockEntityContainer be, ItemSlot slot,
        IPlayer byPlayer, BlockSelection blockSel) { }
}