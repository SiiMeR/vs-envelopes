using System.Collections.Generic;
using System.Text;
using Envelopes.Gui;
using Envelopes.Messages;
using Envelopes.Util;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace Envelopes.Behaviors;

public class AddressableBehavior : CollectibleBehavior
{
    public AddressableBehavior(CollectibleObject collObj) : base(collObj)
    {
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel,
        EntitySelection entitySel,
        bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
    {
        if (byEntity?.Controls?.Sprint == false)
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling, ref handling);
            return;
        }


        if (slot.Itemstack.Attributes.HasAttribute(EnvelopeAttributes.From) ||
            slot.Itemstack.Attributes.HasAttribute(EnvelopeAttributes.To))
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling,
                ref handling);
            return;
        }

        var hasWritingTool = byEntity?.LeftHandItemSlot?.Itemstack?.Collectible.Code.Path.Contains("inkandquill") ??
                             false;
        if (!hasWritingTool)
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling,
                ref handling);
            return;
        }

        handling = EnumHandling.Handled;

        if (EnvelopesModSystem.Api is not ICoreClientAPI capi)
        {
            return;
        }


        var gui = new GuiDialogEnvelopeHeadersEditor(capi, (from, to) =>
        {
            if (string.IsNullOrWhiteSpace(from) && string.IsNullOrWhiteSpace(to))
            {
                return;
            }

            if (slot == null)
            {
                return;
            }

            EnvelopesModSystem.ClientNetworkChannel?.SendPacket(new SetEnvelopeFromToPacket
            {
                From = from,
                To = to,
                InventoryId = slot.Inventory.InventoryID,
                SlotId = slot.Inventory.GetSlotId(slot)
            });
        });
        gui.TryOpen();
    }


    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot, ref EnumHandling handling)
    {
        var heldInteractionHelp = base.GetHeldInteractionHelp(inSlot, ref handling);

        return ObjectCacheUtil.GetOrCreate(EnvelopesModSystem.Api, "writableBehaviorInteractions", () =>
        {
            var interactions = new List<WorldInteraction>
            {
                new()
                {
                    ActionLangCode = Helpers.EnvelopesLangString("headereditor-action"),
                    MouseButton = EnumMouseButton.Right,
                    HotKeyCode = "ctrl",
                    Itemstacks = new[]
                        { new ItemStack(EnvelopesModSystem.Api?.World.GetItem(new AssetLocation("game:inkandquill"))) },
                    GetMatchingStacks = (wi, _, _) =>
                    {
                        var activeHotbarSlot = (EnvelopesModSystem.Api as ICoreClientAPI)?.World.Player.InventoryManager
                            .ActiveHotbarSlot;

                        if (activeHotbarSlot == null)
                        {
                            return wi.Itemstacks;
                        }

                        return (!activeHotbarSlot.Itemstack.Attributes.HasAttribute(EnvelopeAttributes.From) &&
                                !activeHotbarSlot.Itemstack.Attributes.HasAttribute(EnvelopeAttributes.To))
                            ? wi.Itemstacks
                            : null;
                    },
                }
            };
            return interactions.ToArray().Append(heldInteractionHelp);
        });
    }

    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
    {
        var from = inSlot.Itemstack.Attributes.GetString(EnvelopeAttributes.From);
        if (!string.IsNullOrWhiteSpace(from))
        {
            dsc.AppendLine(Helpers.EnvelopesLangString("headereditor-from") + ": " + from);
        }

        var to = inSlot.Itemstack.Attributes.GetString(EnvelopeAttributes.To);
        if (!string.IsNullOrWhiteSpace(to))
        {
            dsc.AppendLine(Helpers.EnvelopesLangString("headereditor-to") + ": " + to);
        }
    }
}