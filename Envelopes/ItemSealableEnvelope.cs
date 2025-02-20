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

    public string GetModDataPath()
    {
        string localPath = Path.Combine("ModData", api.World.SavegameIdentifier, EnvelopesModSystem.ModId);
        return api.GetOrCreateDataPath(localPath);
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

    private void OpenEnvelope(ItemSlot slot, IPlayer opener, string nextCode)
    {
        var contentsId = slot.Itemstack.Attributes.GetString("ContentsId");
        if (string.IsNullOrEmpty(contentsId))
        {
            api.Logger.Error("No ContentsId on closed envelope.");
            return;
        }
        
        var modDataPath = GetModDataPath();
        var filePath = Path.Combine(modDataPath, contentsId);
        if (!File.Exists(filePath))
        {
            api.Logger.Error("Envelope contents don't exist on disk.");
            return;
        }
        
        using var stream = File.OpenRead(filePath);
        using var binaryReader = new BinaryReader(stream);
        
        var paper = new ItemStack(api.World.GetItem(new AssetLocation("game:paper-parchment")));
        paper.Attributes.FromBytes(binaryReader);

        
        var nextItem = new ItemStack(api.World.GetItem(new AssetLocation(nextCode)));
        if (!opener.InventoryManager.TryGiveItemstack(nextItem, true))
        {
            api.World.SpawnItemEntity(nextItem, opener.Entity.SidedPos.XYZ);
        }
        
        slot.Itemstack = paper;
        slot.MarkDirty();
    }

    public override bool ConsumeCraftingIngredients(ItemSlot[] slots, ItemSlot outputSlot, GridRecipe matchingRecipe)
    {
        var code = outputSlot.Itemstack.Collectible.Code.Path;

        switch (code)
        {
            case "envelope-unsealed":
                var letterSlot = slots.First(slot => !slot.Empty && slot.Itemstack.Collectible.Code.Path.Contains("parchment"));
                PutLetterIntoEnvelope(letterSlot, outputSlot);
                break;
            case "envelope-sealed":
                SealEnvelope();
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

        if (code is "envelope-sealed")
        {
            if (api.Side == EnumAppSide.Client)
            {
                _currentConfirmationDialog = new GuiDialogConfirm(api as ICoreClientAPI, Lang.Get("open-envelope-confirmation"),
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
        
        if (code is "envelope-unsealed")
        {
            OpenEnvelope(slot, player.Player, nextCode);
            handling = EnumHandHandling.Handled;
            (slot.Itemstack.Collectible as ItemBook)?.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, true, ref handling);

            return;
        }

        base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
    }
    
    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
    {
        var worldInteractions = new List<WorldInteraction>();
        var code = inSlot.Itemstack.Collectible.Code.Path;
        if (code is "envelope-unsealed" or "envelope-sealed")
        {
            worldInteractions.Add(new WorldInteraction
            {
                ActionLangCode = "open-envelope",
                MouseButton = EnumMouseButton.Right,
            });
        }

        return worldInteractions.ToArray().Append(base.GetHeldInteractionHelp(inSlot));
    }
}