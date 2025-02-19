using Vintagestory.API.Common;

namespace Envelopes;

public class EnvelopesModSystem : ModSystem
{
    public override void Start(ICoreAPI api)
    {
       api.RegisterItemClass("ItemSealableEnvelope", typeof(ItemSealableEnvelope));
    }
}