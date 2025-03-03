using System;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace Envelopes.Util;

public static class Helpers
{
    public static bool IsServerSide()
    {
        return GetApi().Side == EnumAppSide.Server;
    }

    public static bool IsClientSide()
    {
        return GetApi().Side == EnumAppSide.Client;
    }

    public static string EnvelopesLangString(string entry)
    {
        return Lang.Get($"{EnvelopesModSystem.ModId}:{entry}");
    }

    public static ICoreAPI GetApi()
    {
        var api = EnvelopesModSystem.Api;
        if (api == null)
        {
            throw new Exception("Tried to access CoreAPI, but it is not available. Weird.");
        }

        return api;
    }

    // Finds the collectible in player inventory, prioritizing the selected hotbar slot
    public static ItemSlot? FindCollectibleOfTypeInInventory<TCollectible>(IServerPlayer player)
        where TCollectible : CollectibleObject
    {
        var activeSlot = player.InventoryManager.ActiveHotbarSlot;
        if (activeSlot?.Itemstack?.Collectible is TCollectible)
        {
            return activeSlot;
        }

        ItemSlot? matchingSlot = null;
        player.Entity.WalkInventory(slot =>
        {
            if (slot.Itemstack?.Collectible is TCollectible)
            {
                matchingSlot = slot;
                return false;
            }

            return true;
        });

        return matchingSlot;
    }
}