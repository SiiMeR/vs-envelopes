using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Envelopes.Database;
using Envelopes.Messages;
using Envelopes.Util;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Envelopes.Items;

public abstract class ItemSealableContainer : Item, IContainedInteractable
{
    protected abstract string GetEmptyItemCode();
    protected abstract string GetUnsealedItemCode();
    protected abstract string GetSealedItemCode();
    protected abstract string GetOpenedItemCode();
    protected abstract bool CanContainItem(ItemSlot itemSlot);
    protected abstract string GetContainerType();

    protected void PutItemIntoContainer(ItemSlot? itemSlot, ItemSlot? containerSlot)
    {
        if (itemSlot == null || containerSlot == null)
        {
            return;
        }

        if (api.Side != EnumAppSide.Server)
        {
            return;
        }

        var envelopeDatabase = EnvelopesModSystem.EnvelopeDatabase;
        if (envelopeDatabase == null)
        {
            throw new InvalidOperationException("The envelopes database has not been initialized yet.");
        }

        itemSlot.MarkDirty();
        var toStore = itemSlot.Itemstack.Clone();
        toStore.StackSize = 1;
        using var stream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(stream);
        toStore.ToBytes(binaryWriter);

        var ownerId = containerSlot.Inventory.openedByPlayerGUIds.FirstOrDefault();

        var envelope = new EnvelopeContents
        {
            CreatorId = ownerId ?? "unknown",
            ItemBlob = stream.ToArray()
        };

        var envelopeId = envelopeDatabase.InsertEnvelope(envelope);

        containerSlot.Itemstack.Attributes.SetString(EnvelopeAttributes.ContentsId, envelopeId);
        containerSlot.MarkDirty();
    }

    protected void SealContainer(ItemSlot? containerSlot, ItemSlot? stampSlot, ItemSlot outputSlot)
    {
        if (containerSlot == null || stampSlot == null)
        {
            return;
        }

        var contentsId = containerSlot.Itemstack?.Attributes?.GetString(EnvelopeAttributes.ContentsId);
        if (string.IsNullOrEmpty(contentsId))
        {
            api.Logger.Debug($"Trying to seal an empty {GetContainerType()}.");
            return;
        }

        var stampId = stampSlot.Itemstack?.Attributes?.TryGetLong(StampAttributes.StampId);
        if (!stampId.HasValue)
        {
            api.Logger.Debug("Trying to seal with an empty stamp.");
            return;
        }

        var stamp = EnvelopesModSystem.StampDatabase?.GetStamp(stampId.Value);
        if (stamp == null)
        {
            api.Logger.Debug($"Unable to seal {GetContainerType()}. Container:{contentsId}, Stamp:{stampId}");
            return;
        }

        outputSlot.Itemstack?.Attributes?.SetLong(StampAttributes.StampId, stamp.Id);
        outputSlot.Itemstack?.Attributes?.SetString(StampAttributes.StampTitle, stamp.Title);

        var stampDesign = stampSlot.Itemstack?.Attributes?.GetString(StampAttributes.StampDesign);
        if (!string.IsNullOrEmpty(stampDesign))
        {
            outputSlot.Itemstack?.Attributes?.SetString(StampAttributes.StampDesign, stampDesign);
        }

        outputSlot.MarkDirty();
    }

    public virtual void OpenContainer(ItemSlot slot, IPlayer opener)
    {
        var globalApi = EnvelopesModSystem.Api;
        if (globalApi == null) return;
        if (opener.Entity.World.Side != EnumAppSide.Server) return;

        var container = slot.Itemstack;
        if (container == null) return;

        var contentsId = container.Attributes.GetString(EnvelopeAttributes.ContentsId);
        if (string.IsNullOrEmpty(contentsId)) return;

        var envelopeContents = EnvelopesModSystem.EnvelopeDatabase?.GetEnvelope(contentsId);
        if (envelopeContents == null) return;

        using var ms = new MemoryStream(envelopeContents.ItemBlob);
        using var br = new BinaryReader(ms);
        ItemStack itemstack;
        try
        {
            itemstack = new ItemStack(br, globalApi.World);
        }
        catch (Exception _)
        {
            ms.Seek(0L, SeekOrigin.Begin);
            itemstack = new ItemStack(globalApi.World.GetItem(new AssetLocation("game:paper-parchment")));
            itemstack.Attributes.FromBytes(br);
        }

        if (!opener.InventoryManager.TryGiveItemstack(itemstack, true))
            globalApi.World.SpawnItemEntity(itemstack, opener.Entity.SidedPos.XYZ);

        var codePath = container.Collectible.Code.Path;
        if (codePath == new AssetLocation(GetSealedItemCode()).Path)
        {
            var nextItem = new ItemStack(globalApi.World.GetItem(new AssetLocation(GetOpenedItemCode())));
            CopyContainerAttributes(container, nextItem);
            slot.Itemstack = nextItem;
        }
        else if (codePath == new AssetLocation(GetUnsealedItemCode()).Path)
        {
            slot.Itemstack = new ItemStack(globalApi.World.GetItem(new AssetLocation(GetEmptyItemCode())));
        }
        else
        {
            container.Attributes.RemoveAttribute(EnvelopeAttributes.ContentsId);
        }

        globalApi.Event.EnqueueMainThreadTask(() =>
                globalApi.World.PlaySoundAt(new AssetLocation("game:sounds/held/bookclose*"), opener.Entity, null, true,
                    16f, 1f),
            "envelope-sound");
        slot.MarkDirty();
    }

    public void CopyContainerAttributes(ItemStack from, ItemStack to)
    {
        var stampId = from.Attributes.TryGetLong(StampAttributes.StampId);
        if (stampId.HasValue) to.Attributes.SetLong(StampAttributes.StampId, stampId.Value);

        var stampTitle = from.Attributes.GetString(StampAttributes.StampTitle);
        if (!string.IsNullOrEmpty(stampTitle)) to.Attributes.SetString(StampAttributes.StampTitle, stampTitle);

        var stampDesign = from.Attributes.GetString(StampAttributes.StampDesign);
        if (!string.IsNullOrEmpty(stampDesign)) to.Attributes.SetString(StampAttributes.StampDesign, stampDesign);

        var fromAttr = from.Attributes.GetString(EnvelopeAttributes.From);
        if (!string.IsNullOrEmpty(fromAttr)) to.Attributes.SetString(EnvelopeAttributes.From, fromAttr);

        var toAttr = from.Attributes.GetString(EnvelopeAttributes.To);
        if (!string.IsNullOrEmpty(toAttr)) to.Attributes.SetString(EnvelopeAttributes.To, toAttr);

        var waxColor = from.Attributes.GetString(EnvelopeAttributes.WaxColor);
        if (!string.IsNullOrEmpty(waxColor)) to.Attributes.SetString(EnvelopeAttributes.WaxColor, waxColor);
    }

    public WorldInteraction[] GetContainedInteractionHelp(BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel) => null;

    public override bool ConsumeCraftingIngredients(ItemSlot[] slots, ItemSlot outputSlot, IRecipeBase matchingRecipe)
    {
        if (api.Side == EnumAppSide.Server)
        {
            var code = outputSlot.Itemstack.Collectible.Code.Path;

            // Find the container slot first (envelope or parcel)
            var containerSlot = slots.FirstOrDefault(slot =>
                !slot.Empty && slot.Itemstack.Collectible is ItemSealableContainer);

            // Find the item slot - should be the slot that's not the container and not a stamp
            var itemSlot = slots.FirstOrDefault(slot =>
                !slot.Empty &&
                slot.Itemstack.Collectible is not ItemSealableContainer &&
                !slot.Itemstack.Collectible.Code.Path.Contains("sealstamp"));

            var stampSlot = slots.FirstOrDefault(slot =>
                !slot.Empty && (slot.Itemstack.Collectible.Code.Path.Contains("sealstamp-engraved") ||
                                slot.Itemstack.Collectible is ItemSignetRing));

            if (code.Contains("unsealed"))
            {
                if (string.IsNullOrEmpty(outputSlot.Itemstack?.Attributes?.GetString(EnvelopeAttributes.ContentsId)))
                    PutItemIntoContainer(itemSlot, outputSlot);
            }
            else if (code.Contains("sealed") && !code.Contains("unsealed"))
            {
                SealContainer(containerSlot, stampSlot, outputSlot);
            }
            else if (code.Contains("opened"))
            {
                PutItemIntoContainer(itemSlot, outputSlot);
            }
        }

        return base.ConsumeCraftingIngredients(slots, outputSlot, matchingRecipe);
    }

    public override bool MatchesForCrafting(ItemStack inputStack, IRecipeBase recipe, IRecipeIngredient ingredient)
    {
        if (recipe.RecipeOutput.ResolvedItemStack?.Collectible?.Code?.Path?.Contains("empty") == true
            && (!string.IsNullOrEmpty(inputStack.Attributes.GetString(EnvelopeAttributes.ContentsId))
                || inputStack.Attributes.GetBytes(EnvelopeAttributes.VisibleContent)?.Length > 0))
        {
            return false;
        }

        return base.MatchesForCrafting(inputStack, recipe, ingredient);
    }

    public override void OnCreatedByCrafting(ItemSlot[] allInputslots, ItemSlot outputSlot, IRecipeBase byRecipe)
    {
        if (!outputSlot.Itemstack.Collectible.Code.Path.Contains("sealed") ||
            outputSlot.Itemstack.Collectible.Code.Path.Contains("unsealed"))
        {
            base.OnCreatedByCrafting(allInputslots, outputSlot, byRecipe);
            return;
        }

        var waxstickSlot = allInputslots.Where(slot => !slot.Empty)
            .FirstOrDefault(slot => slot.Itemstack.Collectible.Code.Path.Contains("waxstick"));
        if (waxstickSlot != null)
        {
            var waxColor = waxstickSlot.Itemstack.Collectible.Attributes["color"].AsString();
            outputSlot.Itemstack.Attributes.SetString(EnvelopeAttributes.WaxColor, waxColor);
            outputSlot.MarkDirty();
        }

        var stampSlot = allInputslots.Where(slot => !slot.Empty)
            .FirstOrDefault(slot => (slot.Itemstack.Collectible.Code.Path.Contains("sealstamp-engraved") ||
                                     slot.Itemstack.Collectible is ItemSignetRing));
        if (stampSlot != null)
        {
            var attrs = stampSlot.Itemstack.Attributes;
            var stampId = attrs.TryGetLong(StampAttributes.StampId);
            if (stampId.HasValue) outputSlot.Itemstack.Attributes.SetLong(StampAttributes.StampId, stampId.Value);
            var stampTitle = attrs.GetString(StampAttributes.StampTitle);
            if (!string.IsNullOrEmpty(stampTitle))
                outputSlot.Itemstack.Attributes.SetString(StampAttributes.StampTitle, stampTitle);
            var stampDesign = attrs.GetString(StampAttributes.StampDesign);
            if (!string.IsNullOrEmpty(stampDesign))
                outputSlot.Itemstack.Attributes.SetString(StampAttributes.StampDesign, stampDesign);
        }

        base.OnCreatedByCrafting(allInputslots, outputSlot, byRecipe);
    }


    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel,
        EntitySelection entitySel,
        bool firstEvent, ref EnumHandHandling handling)
    {
        if (byEntity.Controls.ShiftKey || byEntity.Controls.CtrlKey)
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            return;
        }

        var code = slot.Itemstack.Collectible.Code.Path;
        if (code.Contains("sealed") && !code.Contains("unsealed"))
        {
            if (api.Side == EnumAppSide.Client)
            {
                var contentsId = slot.Itemstack.Attributes.GetString(EnvelopeAttributes.ContentsId) ?? "";
                GuiDialogConfirm? confirmationDialog = null;
                var dialog = confirmationDialog;
                confirmationDialog = new GuiDialogConfirm(api as ICoreClientAPI, Lang.Get(
                        $"{EnvelopesModSystem.ModId}:open-{GetContainerType()}-confirmation"),
                    ok =>
                    {
                        if (!ok) return;
                        dialog?.TryClose();
                        EnvelopesModSystem.ClientNetworkChannel?.SendPacket(new OpenEnvelopePacket
                            { ContentsId = contentsId });
                        api.Event.EnqueueMainThreadTask((() =>
                            {
                                var sealedHandled = EnumHandHandling.Handled;
                                (slot.Itemstack.Collectible as ItemBook)?.OnHeldInteractStart(slot, byEntity, blockSel,
                                    entitySel, true, ref sealedHandled);
                            }), $"open-{GetContainerType()}");
                    });
                confirmationDialog.TryOpen();
            }

            handling = EnumHandHandling.Handled;
            return;
        }

        if (code.Contains("unsealed") || code.Contains("opened"))
        {
            handling = EnumHandHandling.Handled;
            if (api.Side == EnumAppSide.Client)
            {
                var contentsId = slot.Itemstack.Attributes.GetString(EnvelopeAttributes.ContentsId) ?? "";
                EnvelopesModSystem.ClientNetworkChannel?.SendPacket(new OpenEnvelopePacket
                    { ContentsId = contentsId });
            }

            return;
        }

        base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
    }

    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
    {
        var code = inSlot.Itemstack.Collectible.Code.Path;
        var interactions = new List<WorldInteraction>();

        if (code.Contains("unsealed") || code.Contains("sealed"))
        {
            interactions.Add(new WorldInteraction
            {
                ActionLangCode = $"{EnvelopesModSystem.ModId}:open-{GetContainerType()}",
                MouseButton = EnumMouseButton.Right
            });
        }

        if (code.Contains("unsealed"))
        {
            var stamp = api.World.GetItem(new AssetLocation("envelopes:sealstamp-engraved"));
            if (stamp != null)
            {
                interactions.Add(new WorldInteraction
                {
                    ActionLangCode = $"{EnvelopesModSystem.ModId}:heldhelp-sealwithstamp",
                    MouseButton = EnumMouseButton.Right,
                    HotKeyCodes = new[] { "shift" },
                    Itemstacks = new[] { new ItemStack(stamp) }
                });
            }
        }

        return interactions.ToArray().Append(base.GetHeldInteractionHelp(inSlot));
    }

    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
    {
        base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

        var contentsId = inSlot.Itemstack?.Attributes?.GetString(EnvelopeAttributes.ContentsId);
        if (!string.IsNullOrEmpty(contentsId))
        {
            dsc.AppendLine(Lang.Get($"{EnvelopesModSystem.ModId}:{GetContainerType()}-hascontents"));
        }

        if (inSlot.Itemstack?.Item?.Code?.Path?.Contains("opened") ?? false)
        {
            dsc.AppendLine(Lang.Get($"{EnvelopesModSystem.ModId}:{GetContainerType()}-sealbroken"));
        }

        dsc.AppendLine("");

        var stampName = inSlot.Itemstack?.Attributes?.GetString(StampAttributes.StampTitle);
        var stampId = inSlot.Itemstack?.Attributes?.TryGetLong(StampAttributes.StampId);
        if (!string.IsNullOrEmpty(stampName) && stampId.HasValue)
        {
            var id = $"<font color=\"gray\">(ID:{stampId})</font>";
            var name = $"<font color=\"orange\">\"{stampName}\" </font>";
            dsc.AppendLine(Helpers.EnvelopesLangString($"{GetContainerType()}-emblem") + ":");
            dsc.AppendLine(
                $"{name} {id}");
        }

        // backwards compatibility mapping for older envelopes
        var sealerId = inSlot.Itemstack?.Attributes?.GetString(EnvelopeAttributes.SealerId);
        if (!string.IsNullOrEmpty(sealerId))
        {
            EnvelopesModSystem.ClientNetworkChannel.SendPacket(new RemapSealerIdPacket
            {
                InventoryId = inSlot.Inventory.InventoryID,
                SlotId = inSlot.Inventory.GetSlotId(inSlot)
            });
            inSlot.MarkDirty();
        }

        var sealerName = inSlot.Itemstack?.Attributes?.GetString(EnvelopeAttributes.SealerName);
        if (!string.IsNullOrEmpty(sealerName))
        {
            dsc.AppendLine($"{Lang.Get($"{EnvelopesModSystem.ModId}:{GetContainerType()}-sealedby")}: {sealerName}");
        }
    }

    public virtual bool OnContainedInteractStart(BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer,
        BlockSelection blockSel) => false;

    public virtual bool OnContainedInteractStep(float secondsUsed, BlockEntityContainer be, ItemSlot slot,
        IPlayer byPlayer, BlockSelection blockSel) => false;

    public virtual void OnContainedInteractStop(float secondsUsed, BlockEntityContainer be, ItemSlot slot,
        IPlayer byPlayer, BlockSelection blockSel)
    {
    }
}