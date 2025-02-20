﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Envelopes;

public class ItemSealableEnvelope : Item
{
    private string GenerateEnvelopeId() => Guid.NewGuid().ToString("n");

    public string GetModDataPath()
    {
        string localPath = Path.Combine("ModData", api.World.SavegameIdentifier, EnvelopesModSystem.ModId);
        return api.GetOrCreateDataPath(localPath);
    }
    
    
    private void PutLetterIntoEnvelope(ItemStack letter, ItemSlot outputSlot)
    {
        if (api.World.Side != EnumAppSide.Server)
            return;

        if (api is not ICoreServerAPI serverApi)
            return;

        var id = GenerateEnvelopeId();
        var modDataPath = GetModDataPath();

        using var stream = File.OpenWrite(Path.Combine(modDataPath, id));
        using var binaryWriter = new BinaryWriter(stream);
        letter.Attributes.ToBytes(binaryWriter);
        
        outputSlot.Itemstack.Attributes.SetString("ContentsId", id);
        outputSlot.MarkDirty();
    }

    private void SealEnvelope()
    {
        Console.WriteLine("Sealed");
    }

    private void OpenEnvelope(ItemStack envelope, IPlayer opener, string nextCode)
    {
        var contentsId = envelope.Attributes.GetString("ContentsId");
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
        if (!opener.InventoryManager.TryGiveItemstack(paper, true))
        {
            api.World.SpawnItemEntity(paper, opener.Entity.SidedPos.XYZ);
        }
        
        var nextItem = new ItemStack(api.World.GetItem(new AssetLocation(nextCode)));
        if (!opener.InventoryManager.TryGiveItemstack(nextItem, true))
        {
            api.World.SpawnItemEntity(nextItem, opener.Entity.SidedPos.XYZ);
        }
    }

    public override bool ConsumeCraftingIngredients(ItemSlot[] slots, ItemSlot outputSlot, GridRecipe matchingRecipe)
    {
        var code = outputSlot.Itemstack.Collectible.Code.Path;

        switch (code)
        {
            case "envelope-unsealed":
                var letterSlot = slots.First(slot => !slot.Empty && slot.Itemstack.Collectible.Code.Path.Contains("parchment"));
                PutLetterIntoEnvelope(letterSlot.Itemstack, outputSlot);
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
        // if (api.World.Side != EnumAppSide.Server)
        // {
        //     return;
        // }
        //
        if (byEntity is not EntityPlayer player)
        {
            return;
        }
        
        var code = slot.Itemstack.Collectible.Code.Path;
        if (code is "envelope-sealed" or "envelope-unsealed")
        {
            var envelope = slot.TakeOut(1);
            var nextCode = code == "envelope-sealed" ? "envelopes:envelope-opened" : "envelopes:envelope-empty";
            OpenEnvelope(envelope, player.Player, nextCode);
            slot.MarkDirty();
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
                ActionLangCode = "heldhelp-open",
                MouseButton = EnumMouseButton.Right,
            });
        }

        return worldInteractions.ToArray().Append(base.GetHeldInteractionHelp(inSlot));
    }
}