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

public abstract class ItemSealableContainer : Item
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
        using var stream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(stream);
        itemSlot.Itemstack.ToBytes(binaryWriter);

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

    public void OpenContainer(ItemSlot slot, IPlayer opener)
    {
        var globalApi = EnvelopesModSystem.Api;
        if (globalApi == null)
        {
            return;
        }

        if (opener.Entity.World.Side != EnumAppSide.Server)
        {
            return;
        }

        var container = slot.Itemstack;
        if (container == null)
        {
            globalApi.Logger.Debug($"{GetContainerType()} moved from slot before opening.");
            return;
        }

        var contentsId = container.Attributes.GetString(EnvelopeAttributes.ContentsId);
        if (string.IsNullOrEmpty(contentsId))
        {
            globalApi.Logger.Debug($"Trying to open an empty {GetContainerType()}.");
            return;
        }

        var database = EnvelopesModSystem.EnvelopeDatabase;
        if (database == null)
        {
            throw new InvalidOperationException("The envelopes database has not been initialized yet.");
        }

        var envelopeContents = database.GetEnvelope(contentsId);
        if (envelopeContents == null)
        {
            throw new InvalidOperationException($"Failed to retrieve {GetContainerType()} contents.");
        }

        using var memoryStream = new MemoryStream(envelopeContents.ItemBlob);
        using var binaryReader = new BinaryReader(memoryStream);

        ItemStack itemstack;
        try
        {
            itemstack = new ItemStack(binaryReader, globalApi.World);
        }
        catch (Exception _)
        {
            // fallback path for older envelopes which had paper attributes
            memoryStream.Seek(0L, SeekOrigin.Begin);
            itemstack = new ItemStack(globalApi.World.GetItem(new AssetLocation("game:paper-parchment")));
            itemstack.Attributes.FromBytes(binaryReader);
        }

        itemstack.StackSize = 1;

        var codePath = container.Collectible.Code.Path;
        var nextCode = codePath.Contains("opened")
            ? GetEmptyItemCode()
            : codePath.Contains("unsealed")
                ? GetEmptyItemCode()
                : GetOpenedItemCode();

        var nextItem = new ItemStack(globalApi.World.GetItem(new AssetLocation(nextCode)));

        // Copy attributes from the old container to the new one
        var stampId = container?.Attributes?.TryGetLong(StampAttributes.StampId);
        if (stampId.HasValue)
        {
            nextItem.Attributes?.SetLong(StampAttributes.StampId, stampId.Value);
        }

        var stampTitle = container?.Attributes?.GetString(StampAttributes.StampTitle);
        if (!string.IsNullOrEmpty(stampTitle))
        {
            nextItem.Attributes?.SetString(StampAttributes.StampTitle, stampTitle);
        }

        var stampDesign = container?.Attributes?.GetString(StampAttributes.StampDesign);
        if (!string.IsNullOrEmpty(stampDesign))
        {
            nextItem.Attributes?.SetString(StampAttributes.StampDesign, stampDesign);
        }

        var from = container?.Attributes?.GetString(EnvelopeAttributes.From);
        if (!string.IsNullOrEmpty(from))
        {
            nextItem.Attributes?.SetString(EnvelopeAttributes.From, from);
        }

        var to = container?.Attributes?.GetString(EnvelopeAttributes.To);
        if (!string.IsNullOrEmpty(to))
        {
            nextItem.Attributes?.SetString(EnvelopeAttributes.To, to);
        }

        var waxColor = container?.Attributes?.GetString(EnvelopeAttributes.WaxColor);
        if (!string.IsNullOrEmpty(waxColor))
        {
            nextItem.Attributes?.SetString(EnvelopeAttributes.WaxColor, waxColor);
        }

        if (!opener.InventoryManager.TryGiveItemstack(nextItem, true))
        {
            globalApi.World.SpawnItemEntity(nextItem, opener.Entity.SidedPos.XYZ);
        }

        slot.Itemstack = itemstack;
        slot.MarkDirty();
    }

    public override bool ConsumeCraftingIngredients(ItemSlot[] slots, ItemSlot outputSlot, GridRecipe matchingRecipe)
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
                !slot.Empty && slot.Itemstack.Collectible.Code.Path.Contains("sealstamp-engraved"));

            if (code.Contains("unsealed"))
            {
                // When crafting item + empty/opened parcel -> unsealed parcel
                PutItemIntoContainer(itemSlot, outputSlot);
            }
            else if (code.Contains("sealed") && !code.Contains("unsealed"))
            {
                // When sealing an unsealed parcel with wax
                SealContainer(containerSlot, stampSlot, outputSlot);
            }
            else if (code.Contains("opened"))
            {
                // When adding item to an already opened parcel
                PutItemIntoContainer(itemSlot, outputSlot);
            }
        }

        return base.ConsumeCraftingIngredients(slots, outputSlot, matchingRecipe);
    }

    public override void OnCreatedByCrafting(ItemSlot[] allInputslots, ItemSlot outputSlot, GridRecipe byRecipe)
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

        var contentsId = slot.Itemstack.Attributes.GetString(EnvelopeAttributes.ContentsId);
        if (string.IsNullOrEmpty(contentsId))
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            return;
        }

        var code = slot.Itemstack.Collectible.Code.Path;
        if (code.Contains("sealed") && !code.Contains("unsealed"))
        {
            if (api.Side == EnumAppSide.Client)
            {
                GuiDialogConfirm? confirmationDialog = null;
                var dialog = confirmationDialog;
                confirmationDialog = new GuiDialogConfirm(api as ICoreClientAPI, Lang.Get(
                        $"{EnvelopesModSystem.ModId}:open-{GetContainerType()}-confirmation"),
                    ok =>
                    {
                        if (!ok)
                        {
                            return;
                        }

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
                EnvelopesModSystem.ClientNetworkChannel?.SendPacket(new OpenEnvelopePacket
                    { ContentsId = contentsId });
            }

            return;
        }

        base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
    }

    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
    {
        return ObjectCacheUtil.GetOrCreate<WorldInteraction[]>(api, $"{GetContainerType()}Interactions", () =>
        {
            var interactions = new List<WorldInteraction>();
            var code = inSlot.Itemstack.Collectible.Code.Path;
            if (code.Contains("unsealed") || code.Contains("sealed"))
            {
                interactions.Add(new WorldInteraction
                {
                    ActionLangCode = $"{EnvelopesModSystem.ModId}:open-{GetContainerType()}",
                    MouseButton = EnumMouseButton.Right
                });
            }

            return interactions.ToArray().Append(base.GetHeldInteractionHelp(inSlot));
        });
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

        // backwards compatibility for older envelopes
        var sealerName = inSlot.Itemstack?.Attributes?.GetString(EnvelopeAttributes.SealerName);
        if (!string.IsNullOrEmpty(sealerName))
        {
            dsc.AppendLine($"{Lang.Get($"{EnvelopesModSystem.ModId}:{GetContainerType()}-sealedby")}: {sealerName}");
        }
    }
}