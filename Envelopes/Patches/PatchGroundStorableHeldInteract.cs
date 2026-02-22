using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Envelopes.Patches;

[HarmonyPatch(typeof(CollectibleBehaviorGroundStorable), "OnHeldInteractStart")]
public static class PatchGroundStorableHeldInteract
{
    [HarmonyPrefix]
    public static bool Prefix(EntityAgent byEntity, BlockSelection blockSel)
    {
        if (blockSel == null || !byEntity.Controls.ShiftKey) return true;

        var be = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityGroundStorage;
        if (be?.StorageProps == null) return true;

        var targetSlot = GetTargetSlot(be, blockSel);
        if (targetSlot == null || targetSlot.Empty) return true;
        if (targetSlot.Itemstack.Collectible is not IContainedInteractable) return true;

        return false;
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
