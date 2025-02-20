using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Envelopes;

public class ItemSealableEnvelope : Item
{
    private GuiDialogConfirm _currentConfirmationDialog;
    
    private string GenerateEnvelopeId() => Guid.NewGuid().ToString("n");

    public static string GetModDataPath()
    {
        var globalApi = EnvelopesModSystem.Api;

        string localPath = Path.Combine("ModData", globalApi.World.SavegameIdentifier, EnvelopesModSystem.ModId);
        return globalApi.GetOrCreateDataPath(localPath);
    }
    
    
    private void PutLetterIntoEnvelope(ItemSlot letterSlot, ItemSlot outputSlot)
    {
        letterSlot.MarkDirty();
        
        if (api.World.Side != EnumAppSide.Server)
            return;

        var id = GenerateEnvelopeId();
        var modDataPath = GetModDataPath();

        using var stream = File.OpenWrite(Path.Combine(modDataPath, id));
        using var binaryWriter = new BinaryWriter(stream);
        letterSlot.Itemstack.Attributes.ToBytes(binaryWriter);
        
        outputSlot.Itemstack.Attributes.SetString("ContentsId", id);
        outputSlot.MarkDirty();
    }

    private void SealEnvelope()
    {
        Console.WriteLine("Sealed");
    }

    public static void OpenEnvelope(ItemSlot slot, IPlayer opener, string nextCode)
    {
        var globalApi = EnvelopesModSystem.Api;
        var contentsId = slot.Itemstack.Attributes.GetString("ContentsId");
        if (string.IsNullOrEmpty(contentsId))
        {
            globalApi.Logger.Error("No ContentsId on closed envelope.");
            return;
        }

        if (globalApi.World.Side == EnumAppSide.Client)
        {
            EnvelopesModSystem.ClientNetworkChannel.SendPacket(new OpenEnvelopePacket{ ContentsId = contentsId});
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
        var letterSlot = slots.FirstOrDefault(slot => !slot.Empty && slot.Itemstack.Collectible.Code.Path.Contains("parchment"));

        switch (code)
        {
            case "envelope-unsealed":
                PutLetterIntoEnvelope(letterSlot, outputSlot);
                break;
            case "envelope-sealed":
                SealEnvelope();
                break;
            case "envelope-opened":
                PutLetterIntoEnvelope(letterSlot, outputSlot);
                break;
        }

        return base.ConsumeCraftingIngredients(slots, outputSlot, matchingRecipe);
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel,
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
                    _currentConfirmationDialog = new GuiDialogConfirm(api as ICoreClientAPI, Lang.Get(
                            $"{EnvelopesModSystem.ModId}:open-envelope-confirmation"),
                        (ok) =>
                        {
                            if (!ok)
                                return;
                            _currentConfirmationDialog.TryClose();
                            OpenEnvelope(slot, player.Player, nextCode);

                            var preventSubsequent = EnumHandHandling.Handled;
                            (slot.Itemstack.Collectible as ItemBook)?.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, true, ref preventSubsequent);
                        });
                    _currentConfirmationDialog.TryOpen();
                }
            
                handling = EnumHandHandling.Handled;
                return;
            }
            case "envelope-unsealed":
                OpenEnvelope(slot, player.Player, nextCode);
                handling = EnumHandHandling.Handled;
                (slot.Itemstack.Collectible as ItemBook)?.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, true, ref handling);

                return;
            default:
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                break;
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
                MouseButton = EnumMouseButton.Right,
            });
        }

        return worldInteractions.ToArray().Append(base.GetHeldInteractionHelp(inSlot));
    }
}