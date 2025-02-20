using Vintagestory.API.Common;

namespace Envelopes;

public class EnvelopesModSystem : ModSystem
{
    public static string ModId;

    public override void Start(ICoreAPI api)
    {
        ModId = Mod.Info.ModID;
        api.RegisterItemClass("ItemSealableEnvelope", typeof(ItemSealableEnvelope));
    }
}