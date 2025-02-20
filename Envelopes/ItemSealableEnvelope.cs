using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace Envelopes;

public class ItemSealableEnvelope : Item
{
    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
    {
        var worldInteractions = new List<WorldInteraction>();

        var code = inSlot.Itemstack.Collectible.Code.Path;
        if (code is "envelope-unsealed" or "envelope-sealed")
        {
            worldInteractions.Add(new WorldInteraction
            {
                ActionLangCode = "heldhelp-open",
                MouseButton = EnumMouseButton.Right
            });
        }

        return worldInteractions.ToArray().Append(base.GetHeldInteractionHelp(inSlot));
    }
}