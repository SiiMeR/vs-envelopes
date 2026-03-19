using System.Linq;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Envelopes.Patches;

[HarmonyPatch(typeof(Block), "TryPlaceBlock")]
public static class PatchBlockTryPlaceOnGroundStorage
{
    [HarmonyPrefix]
    public static bool Prefix(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref bool __result)
    {
        if (byPlayer == null || !byPlayer.Entity.Controls.ShiftKey) return true;

        var clickedPos = blockSel.DidOffset
            ? blockSel.Position.AddCopy(blockSel.Face.Opposite)
            : blockSel.Position;

        if (world.BlockAccessor.GetBlockEntity(clickedPos) is not BlockEntityGroundStorage be) return true;

        if (be.Inventory.Any(slot => !slot.Empty && slot.Itemstack?.Collectible is IContainedInteractable))
        {
            __result = false;
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(BlockEntityGroundStorage), "OnPlayerInteractStart")]
public static class PatchGroundStorageInteractStart
{
    [HarmonyPrefix]
    public static bool Prefix(BlockEntityGroundStorage __instance, IPlayer player, BlockSelection bs, ref bool __result)
    {
        if (__instance.StorageProps != null && player.WorldData.EntityControls.Sneak)
        {
            var targetSlot = GetTargetSlot(__instance, bs);
            if (targetSlot != null && !targetSlot.Empty &&
                targetSlot.Itemstack.Collectible is IContainedInteractable interactable)
            {
                __result = interactable.OnContainedInteractStart(__instance, targetSlot, player, bs);
                return false;
            }
        }

        var heldSlot = player.InventoryManager.ActiveHotbarSlot;
        if (heldSlot.Empty) return true;
        return heldSlot.Itemstack.Collectible.HasBehavior<CollectibleBehaviorGroundStorable>();
    }

    private static ItemSlot? GetTargetSlot(BlockEntityGroundStorage be, BlockSelection bs)
    {
        return be.StorageProps.Layout switch
        {
            EnumGroundStorageLayout.SingleCenter => be.Inventory[0],
            EnumGroundStorageLayout.Quadrants => be.Inventory[QuadrantSlotId(be, bs)],
            EnumGroundStorageLayout.Halves or EnumGroundStorageLayout.WallHalves =>
                be.Inventory[bs.HitPosition.X < 0.5 ? 0 : 1],
            _ => null
        };
    }

    private static int QuadrantSlotId(BlockEntityGroundStorage be, BlockSelection bs)
    {
        var m = new Matrixf();
        m.Translate(0.5f, 0.5f, 0.5f).RotateY(-be.MeshAngle).Translate(-0.5f, -0.5f, -0.5f);
        var o = m.TransformVector(new Vec4f(
            (float)bs.HitPosition.X, (float)bs.HitPosition.Y, (float)bs.HitPosition.Z, 1f)).XYZ;
        return (o.X > 0.5f ? 2 : 0) + (o.Z > 0.5f ? 1 : 0);
    }
}