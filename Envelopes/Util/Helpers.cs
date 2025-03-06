using System;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace Envelopes.Util;

public static class Helpers
{
    public static string EnvelopesLangString(string entry)
    {
        return Lang.Get($"{EnvelopesModSystem.ModId}:{entry}");
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

    public static string GetModDataPath()
    {
        var globalApi = EnvelopesModSystem.Api;
        if (globalApi == null)
        {
            throw new InvalidOperationException("The EnvelopesModSystem has not been initialized yet.");
        }

        var localPath = Path.Combine("ModData", globalApi.World.SavegameIdentifier, EnvelopesModSystem.ModId);
        return globalApi.GetOrCreateDataPath(localPath);
    }
}