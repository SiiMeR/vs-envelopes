﻿using System;
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

public class ItemSealableEnvelope : Item // , IContainedMeshSource
{
    private void PutLetterIntoEnvelope(ItemSlot? letterSlot, ItemSlot? envelopeSlot)
    {
        if (letterSlot == null || envelopeSlot == null)
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

        letterSlot.MarkDirty();
        using var stream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(stream);
        letterSlot.Itemstack.ToBytes(binaryWriter);

        var ownerId = envelopeSlot.Inventory.openedByPlayerGUIds.FirstOrDefault();

        var envelope = new EnvelopeContents
        {
            CreatorId = ownerId ?? "unknown",
            ItemBlob = stream.ToArray()
        };

        var envelopeId = envelopeDatabase.InsertEnvelope(envelope);

        envelopeSlot.Itemstack.Attributes.SetString(EnvelopeAttributes.ContentsId, envelopeId);
        envelopeSlot.MarkDirty();
    }


    private void SealEnvelope(ItemSlot? envelopeSlot, ItemSlot? stampSlot, ItemSlot outputSlot)
    {
        if (envelopeSlot == null || stampSlot == null)
        {
            return;
        }

        var contentsId = envelopeSlot.Itemstack?.Attributes?.GetString(EnvelopeAttributes.ContentsId);
        if (string.IsNullOrEmpty(contentsId))
        {
            api.Logger.Debug("Trying to seal an empty envelope.");
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
            api.Logger.Debug($"Unable to seal envelope. Envelope:{contentsId}, Stamp:{stampId}");
            return;
        }

        outputSlot.Itemstack?.Attributes?.SetLong(StampAttributes.StampId, stamp.Id);
        outputSlot.Itemstack?.Attributes?.SetString(StampAttributes.StampTitle, stamp.Title);
        outputSlot.MarkDirty();
    }

    public static void OpenEnvelope(ItemSlot slot, IPlayer opener)
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

        var envelope = slot.Itemstack;
        if (envelope == null)
        {
            globalApi.Logger.Debug("Envelope moved from slot before opening.");
            return;
        }

        var contentsId = envelope.Attributes.GetString(EnvelopeAttributes.ContentsId);
        if (string.IsNullOrEmpty(contentsId))
        {
            globalApi.Logger.Debug("Trying to open an empty envelope.");
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
            throw new InvalidOperationException("Failed to retrieve envelope contents.");
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

        var codePath = envelope.Collectible.Code.Path;
        var nextCode = codePath.Contains("opened")
            ? "envelopes:envelope-opened"
            : codePath.Contains("unsealed")
                ? "envelopes:envelope-empty"
                : "envelopes:envelope-opened";


        var nextItem = new ItemStack(globalApi.World.GetItem(new AssetLocation(nextCode)));

        // TODO code to handle copying attributes from the old envelope to the new one
        var stampId = envelope?.Attributes?.TryGetLong(StampAttributes.StampId);
        if (stampId.HasValue)
        {
            nextItem.Attributes?.SetLong(StampAttributes.StampId, stampId.Value);
        }

        var stampTitle = envelope?.Attributes?.GetString(StampAttributes.StampTitle);
        if (!string.IsNullOrEmpty(stampTitle))
        {
            nextItem.Attributes?.SetString(StampAttributes.StampTitle, stampTitle);
        }

        var from = envelope?.Attributes?.GetString(EnvelopeAttributes.From);
        if (!string.IsNullOrEmpty(from))
        {
            nextItem.Attributes?.SetString(EnvelopeAttributes.From, from);
        }

        var to = envelope?.Attributes?.GetString(EnvelopeAttributes.To);
        if (!string.IsNullOrEmpty(to))
        {
            nextItem.Attributes?.SetString(EnvelopeAttributes.To, to);
        }

        if (nextCode == "envelopes:envelope-opened")
        {
            nextItem.Attributes?.SetString(EnvelopeAttributes.WaxColor,
                envelope?.Attributes?.GetString(EnvelopeAttributes.WaxColor));
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
            var letterSlot = slots.FirstOrDefault(slot =>
                !slot.Empty && (slot.Itemstack.Collectible.Code.Path.Contains("parchment") ||
                                slot.Itemstack.Collectible.Code.Path.Contains("book")));
            var envelopeSlot = slots.FirstOrDefault(slot =>
                !slot.Empty && slot.Itemstack.Collectible.Code.Path.Contains("envelope-unsealed"));
            var stampSlot = slots.FirstOrDefault(slot =>
                !slot.Empty && slot.Itemstack.Collectible.Code.Path.Contains("sealstamp-engraved"));

            switch (code)
            {
                case "envelope-unsealed":
                    PutLetterIntoEnvelope(letterSlot, outputSlot);
                    break;
                case "envelope-sealed":
                    SealEnvelope(envelopeSlot, stampSlot, outputSlot);
                    break;
                case "envelope-opened":
                    PutLetterIntoEnvelope(letterSlot, outputSlot);
                    break;
            }
        }

        return base.ConsumeCraftingIngredients(slots, outputSlot, matchingRecipe);
    }

    public override void OnCreatedByCrafting(ItemSlot[] allInputslots, ItemSlot outputSlot, GridRecipe byRecipe)
    {
        if (outputSlot.Itemstack.Collectible.Code.Path != "envelope-sealed")
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
        switch (code)
        {
            case "envelope-sealed":
            {
                if (api.Side == EnumAppSide.Client)
                {
                    GuiDialogConfirm? confirmationDialog = null;
                    var dialog = confirmationDialog;
                    confirmationDialog = new GuiDialogConfirm(api as ICoreClientAPI, Lang.Get(
                            $"{EnvelopesModSystem.ModId}:open-envelope-confirmation"),
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
                            }), "open-envelope");
                        });
                    confirmationDialog.TryOpen();
                }

                handling = EnumHandHandling.Handled;
                return;
            }
            case "envelope-unsealed":
                handling = EnumHandHandling.Handled;
                if (api.Side == EnumAppSide.Client)
                {
                    EnvelopesModSystem.ClientNetworkChannel?.SendPacket(new OpenEnvelopePacket
                        { ContentsId = contentsId });
                }

                return;
            case "envelope-opened":
                handling = EnumHandHandling.Handled;
                if (api.Side == EnumAppSide.Client)
                {
                    EnvelopesModSystem.ClientNetworkChannel?.SendPacket(new OpenEnvelopePacket
                        { ContentsId = contentsId });
                }

                return;
            default:
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                return;
        }
    }

    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
    {
        return ObjectCacheUtil.GetOrCreate<WorldInteraction[]>(api, "envelopeInteractions", () =>
        {
            var interactions = new List<WorldInteraction>();
            var code = inSlot.Itemstack.Collectible.Code.Path;
            if (code is "envelope-unsealed" or "envelope-sealed")
            {
                interactions.Add(new WorldInteraction
                {
                    ActionLangCode = $"{EnvelopesModSystem.ModId}:open-envelope",
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
            dsc.AppendLine(Lang.Get($"{EnvelopesModSystem.ModId}:envelope-hascontents"));
        }

        if (inSlot.Itemstack?.Item?.Code?.Path?.Contains("envelope-opened") ?? false)
        {
            dsc.AppendLine(Lang.Get($"{EnvelopesModSystem.ModId}:envelope-sealbroken"));
        }

        dsc.AppendLine("");

        var stampName = inSlot.Itemstack?.Attributes?.GetString(StampAttributes.StampTitle);
        var stampId = inSlot.Itemstack?.Attributes?.TryGetLong(StampAttributes.StampId);
        if (!string.IsNullOrEmpty(stampName) && stampId.HasValue)
        {
            var id = $"<font color=\"gray\">(ID:{stampId})</font>";
            var name = $"<font color=\"orange\">“{stampName}” </font>";
            dsc.AppendLine(Helpers.EnvelopesLangString("envelope-emblem") + ":");
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
            dsc.AppendLine($"{Lang.Get($"{EnvelopesModSystem.ModId}:envelope-sealedby")}: {sealerName}");
        }
    }
}