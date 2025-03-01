using Envelopes.Gui;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace Envelopes.Items;

public class ItemWaxSealStamp : Item
{
    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel,
        EntitySelection entitySel,
        bool firstEvent, ref EnumHandHandling handling)
    {
        if (Helpers.IsServerSide())
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            return;
        }

        var dialog = new GuiSealStampDesigner(EnvelopesModSystem.Api as ICoreClientAPI);

        dialog.TryOpen(true);
        handling = EnumHandHandling.PreventDefault;

        base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
    }
}