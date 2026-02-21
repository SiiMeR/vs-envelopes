using System.IO;
using Envelopes.Database;
using Envelopes.Util;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace Envelopes.Items;

public class ItemSealableParcel : ItemSealableContainer, IContainedInteractable
{
    protected override string GetEmptyItemCode() => "envelopes:parcel-empty";
    protected override string GetUnsealedItemCode() => "envelopes:parcel-unsealed";
    protected override string GetSealedItemCode() => "envelopes:parcel-sealed";
    protected override string GetOpenedItemCode() => "envelopes:parcel-opened";
    protected override string GetContainerType() => "parcel";

    protected override bool CanContainItem(ItemSlot itemSlot)
    {
        return itemSlot?.Itemstack?.Collectible != null;
    }

    public bool OnContainedInteractStart(BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer,
        BlockSelection blockSel)
    {
        if (slot.Itemstack == null)
        {
            return false;
        }

        var code = slot.Itemstack?.Collectible?.Code?.Path;
        var heldSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        if (heldSlot == null)
        {
            return false;
        }

        var heldItem = heldSlot.Itemstack;

        if (code == "parcel-empty")
        {
            if (heldItem?.Collectible == null) return false;
            if (heldItem.Collectible is ItemSealableParcel) return true;

            if (be.Api.Side == EnumAppSide.Server)
            {
                var database = EnvelopesModSystem.EnvelopeDatabase;
                if (database != null)
                {
                    using var stream = new MemoryStream();
                    using var writer = new BinaryWriter(stream);
                    heldItem.ToBytes(writer);
                    var id = database.InsertEnvelope(new EnvelopeContents
                        { CreatorId = byPlayer.PlayerUID, ItemBlob = stream.ToArray() });
                    slot.Itemstack =
                        new ItemStack(be.Api.World.GetItem(new AssetLocation("envelopes:parcel-unsealed")));
                    slot.Itemstack.Attributes.SetString(EnvelopeAttributes.ContentsId, id);
                    slot.MarkDirty();
                    be.MarkDirty();
                    heldSlot.TakeOut(1);
                    heldSlot.MarkDirty();
                }
            }

            return true;
        }

        if (code == "parcel-unsealed")
        {
            if (heldItem.Collectible == null) return false;

            var controls = byPlayer.WorldData.EntityControls;
            if (controls.Sneak && controls.Sprint && heldItem.Collectible.Code.Path == "paper-parchment")
            {
                if (be.Api.Side == EnumAppSide.Server)
                {
                    var contentsId = slot.Itemstack?.Attributes.GetString(EnvelopeAttributes.ContentsId);
                    if (!string.IsNullOrEmpty(contentsId))
                    {
                        slot.Itemstack =
                            new ItemStack(be.Api.World.GetItem(new AssetLocation("envelopes:parcel-sealed")));
                        slot.Itemstack.Attributes.SetString(EnvelopeAttributes.ContentsId, contentsId);
                        slot.MarkDirty();
                        be.MarkDirty();
                        heldSlot.TakeOut(1);
                        heldSlot.MarkDirty();
                    }
                }
            }

            return true;
        }

        return false;
    }

    public bool OnContainedInteractStep(float secondsUsed, BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer,
        BlockSelection blockSel) => false;

    public void OnContainedInteractStop(float secondsUsed, BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer,
        BlockSelection blockSel)
    {
    }
}