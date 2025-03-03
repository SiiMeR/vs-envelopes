using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Envelopes.Messages;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Envelopes.Items;

public class ItemSealableEnvelope : Item
{
    private string GenerateEnvelopeId()
    {
        return Guid.NewGuid().ToString("n");
    }

    public static string GetModDataPath()
    {
        var globalApi = EnvelopesModSystem.Api;

        var localPath = Path.Combine("ModData", globalApi.World.SavegameIdentifier, EnvelopesModSystem.ModId);
        return globalApi.GetOrCreateDataPath(localPath);
    }

    private void PutLetterIntoEnvelope(ItemSlot letterSlot, ItemSlot outputSlot)
    {
        letterSlot.MarkDirty();

        if (api.World.Side != EnumAppSide.Server)
        {
            return;
        }

        var id = GenerateEnvelopeId();
        var modDataPath = GetModDataPath();

        using var stream = File.OpenWrite(Path.Combine(modDataPath, id));
        using var binaryWriter = new BinaryWriter(stream);
        letterSlot.Itemstack.Attributes.ToBytes(binaryWriter);

        outputSlot.Itemstack.Attributes.SetString("ContentsId", id);
        outputSlot.MarkDirty();
    }

    private void SealEnvelope(ItemSlot inputSlot)
    {
        var contentsId = inputSlot?.Itemstack?.Attributes?.GetString("ContentsId");
        if (string.IsNullOrEmpty(contentsId))
        {
            api.Logger.Debug("Trying to seal an empty envelope.");
            return;
        }

        EnvelopesModSystem.ClientNetworkChannel.SendPacket(new SealEnvelopePacket { ContentsId = contentsId });
    }

    public static void OpenEnvelope(ItemSlot slot, IPlayer opener, string nextCode)
    {
        var globalApi = EnvelopesModSystem.Api;
        var contentsId = slot.Itemstack.Attributes.GetString("ContentsId");
        if (string.IsNullOrEmpty(contentsId))
        {
            globalApi.Logger.Debug("Trying to open empty envelope.");
            return;
        }

        var modDataPath = GetModDataPath();
        var filePath = Path.Combine(modDataPath, contentsId);
        if (!File.Exists(filePath))
        {
            globalApi.Logger.Error("Envelope contents don't exist on disk.");
            return;
        }

        using var stream = File.OpenRead(filePath);
        using var binaryReader = new BinaryReader(stream);

        var paper = new ItemStack(globalApi.World.GetItem(new AssetLocation("game:paper-parchment")));
        paper.Attributes.FromBytes(binaryReader);


        var nextItem = new ItemStack(globalApi.World.GetItem(new AssetLocation(nextCode)));

        var sealerName = slot?.Itemstack?.Attributes?.GetString("SealerName");
        if (!string.IsNullOrEmpty(sealerName))
        {
            nextItem.Attributes?.SetString("SealerName", sealerName);
        }

        if (!opener.InventoryManager.TryGiveItemstack(nextItem, true))
        {
            globalApi.World.SpawnItemEntity(nextItem, opener.Entity.SidedPos.XYZ);
        }

        slot.Itemstack = paper;
        slot.MarkDirty();
    }

    public override bool ConsumeCraftingIngredients(ItemSlot[] slots, ItemSlot outputSlot, GridRecipe matchingRecipe)
    {
        var code = outputSlot.Itemstack.Collectible.Code.Path;
        var letterSlot = slots.FirstOrDefault(slot =>
            !slot.Empty && slot.Itemstack.Collectible.Code.Path.Contains("parchment"));

        switch (code)
        {
            case "envelope-unsealed":
                PutLetterIntoEnvelope(letterSlot, outputSlot);
                break;
            case "envelope-sealed":
                if (api.Side == EnumAppSide.Client)
                {
                    // TODO: Call all of this server side to immediately seal without any packet magic
                    var envelopeSlot = slots.FirstOrDefault(slot =>
                        !slot.Empty && slot.Itemstack.Collectible.Code.Path.Contains("envelope-unsealed"));
                    SealEnvelope(envelopeSlot);
                }

                break;
            case "envelope-opened":
                PutLetterIntoEnvelope(letterSlot, outputSlot);
                break;
        }

        return base.ConsumeCraftingIngredients(slots, outputSlot, matchingRecipe);
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel,
        EntitySelection entitySel,
        bool firstEvent, ref EnumHandHandling handling)
    {
        if (byEntity.Controls.ShiftKey)
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            return;
        }

        if (byEntity is not EntityPlayer player)
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            return;
        }


        var code = slot.Itemstack.Collectible.Code.Path;
        var nextCode = code == "envelope-sealed" ? "envelopes:envelope-opened" : "envelopes:envelope-empty";


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
                            var contentsId = slot.Itemstack.Attributes.GetString("ContentsId");
                            EnvelopesModSystem.ClientNetworkChannel.SendPacket(new OpenEnvelopePacket
                                { ContentsId = contentsId });
                            var sealedHandled = EnumHandHandling.Handled;
                            (slot.Itemstack.Collectible as ItemBook)?.OnHeldInteractStart(slot, byEntity, blockSel,
                                entitySel, true, ref sealedHandled);
                        });
                    confirmationDialog.TryOpen();
                }

                handling = EnumHandHandling.Handled;
                return;
            }
            case "envelope-unsealed":
                OpenEnvelope(slot, player.Player, nextCode);
                handling = EnumHandHandling.Handled;
                if (api.Side == EnumAppSide.Client)
                {
                    var unsealedHandled = EnumHandHandling.Handled;
                    (slot.Itemstack.Collectible as ItemBook)?.OnHeldInteractStart(slot, byEntity, blockSel, entitySel,
                        true, ref unsealedHandled);
                }

                return;
            case "envelope-opened":
                OpenEnvelope(slot, player.Player, "envelopes:envelope-opened");
                handling = EnumHandHandling.Handled;
                if (api.Side == EnumAppSide.Client)
                {
                    var openedHandled = EnumHandHandling.Handled;
                    (slot.Itemstack.Collectible as ItemBook)?.OnHeldInteractStart(slot, byEntity, blockSel, entitySel,
                        true, ref openedHandled);
                }

                return;
            default:
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                return;
        }
    }

    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
    {
        var worldInteractions = new List<WorldInteraction>();
        var code = inSlot.Itemstack.Collectible.Code.Path;
        if (code is "envelope-unsealed" or "envelope-sealed")
        {
            worldInteractions.Add(new WorldInteraction
            {
                ActionLangCode = $"{EnvelopesModSystem.ModId}:open-envelope",
                MouseButton = EnumMouseButton.Right
            });
        }

        return worldInteractions.ToArray().Append(base.GetHeldInteractionHelp(inSlot));
    }

    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
    {
        var contentsId = inSlot.Itemstack?.Attributes?.GetString("ContentsId");
        if (!string.IsNullOrEmpty(contentsId))
        {
            dsc.AppendLine(Lang.Get($"{EnvelopesModSystem.ModId}:envelope-hascontents"));
        }

        if (inSlot.Itemstack?.Item?.Code?.Path?.Contains("envelope-opened") ?? false)
        {
            dsc.AppendLine(Lang.Get($"{EnvelopesModSystem.ModId}:envelope-sealbroken"));
        }


        // backwards compatibility mapping for older envelopes
        var sealerId = inSlot.Itemstack?.Attributes?.GetString("SealerId");
        if (!string.IsNullOrEmpty(sealerId))
        {
            EnvelopesModSystem.ClientNetworkChannel.SendPacket(new RemapSealerIdPacket
            {
                InventoryId = inSlot.Inventory.InventoryID,
                SlotId = inSlot.Inventory.GetSlotId(inSlot)
            });
            inSlot.MarkDirty();
        }


        var sealerName = inSlot.Itemstack?.Attributes?.GetString("SealerName");
        if (!string.IsNullOrEmpty(sealerName))
        {
            dsc.AppendLine($"{Lang.Get($"{EnvelopesModSystem.ModId}:envelope-sealedby")}: {sealerName}");
        }

        base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
    }
}