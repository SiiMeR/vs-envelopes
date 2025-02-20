using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Envelopes;

public class EnvelopesModSystem : ModSystem
{
    public static string ModId;
    public static ICoreAPI Api;
    public static IClientNetworkChannel ClientNetworkChannel;
    public static IServerNetworkChannel ServerNetworkChannel;
    
    public override void Start(ICoreAPI api)
    {
        Api = api;
        ModId = Mod.Info.ModID;
        api.RegisterItemClass("ItemSealableEnvelope", typeof(ItemSealableEnvelope));
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        ClientNetworkChannel = api.Network.RegisterChannel("envelopes")
            .RegisterMessageType<SealEnvelopePacket>()
            .RegisterMessageType<OpenEnvelopePacket>();

        base.StartClientSide(api);
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        ServerNetworkChannel = api.Network.RegisterChannel("envelopes")
            .RegisterMessageType<SealEnvelopePacket>()
            .RegisterMessageType<OpenEnvelopePacket>()
            .SetMessageHandler<SealEnvelopePacket>(OnSealEnvelopePacket)
            .SetMessageHandler<OpenEnvelopePacket>(OnOpenEnvelopePacket);

        base.StartServerSide(api);
    }

    private void OnSealEnvelopePacket(IServerPlayer fromplayer, SealEnvelopePacket packet)
    {
        fromplayer.Entity.WalkInventory((slot =>
        {
            var contentsId = slot?.Itemstack?.Attributes?.GetString("ContentsId");
            if (contentsId != null && contentsId == packet.ContentsId)
            {
                slot.Itemstack.Attributes.SetString("SealerId", fromplayer.PlayerUID);
                slot.MarkDirty();
                return false;
            }

            return true;
        }));
    }

    private void OnOpenEnvelopePacket(IServerPlayer fromplayer, OpenEnvelopePacket packet)
    {
        fromplayer.Entity.WalkInventory((slot =>
        {
            var contentsId = slot?.Itemstack?.Attributes?.GetString("ContentsId");
            if (contentsId != null && contentsId == packet.ContentsId)
            {
                Api.Event.EnqueueMainThreadTask(() =>
                {
                    ItemSealableEnvelope.OpenEnvelope(slot, fromplayer, "envelopes:envelope-opened");
                }, "openenvelope");
                return false;
            }

            return true;
        }));
    }
}