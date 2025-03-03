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
}