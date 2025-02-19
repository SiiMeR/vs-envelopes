using Vintagestory.API.Common;

namespace Letters;

public class EnvelopesModSystem : ModSystem
{
    public override void Start(ICoreAPI api)
    {
       api.RegisterItemClass("ItemSealableEnvelope", typeof(ItemSealableEnvelope));
    }
}