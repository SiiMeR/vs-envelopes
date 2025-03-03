using System;
using System.Linq;
using Envelopes.Database;
using Envelopes.Items;
using Envelopes.Messages;
using Envelopes.Util;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Envelopes;

public class EnvelopesModSystem : ModSystem
{
    public static string? ModId;
    public static ICoreAPI? Api;
    public static IClientNetworkChannel? ClientNetworkChannel;
    public static IServerNetworkChannel? ServerNetworkChannel;
    public static StampDatabase? StampDatabase;
    public static EnvelopeDatabase? EnvelopeDatabase;

    public override void Start(ICoreAPI api)
    {
        Api = api;
        ModId = Mod.Info.ModID;
        api.RegisterItemClass("ItemSealableEnvelope", typeof(ItemSealableEnvelope));
        api.RegisterItemClass("ItemWaxSealStamp", typeof(ItemWaxSealStamp));
        api.RegisterItemClass("ItemWaxStick", typeof(ItemWaxStick));
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        ClientNetworkChannel = api.Network.RegisterChannel("envelopes")
            .RegisterMessageType<SealEnvelopePacket>()
            .RegisterMessageType<OpenEnvelopePacket>()
            .RegisterMessageType<RemapSealerIdPacket>()
            .RegisterMessageType<SaveStampDesignPacket>();


        base.StartClientSide(api);
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        ServerNetworkChannel = api.Network.RegisterChannel("envelopes")
            .RegisterMessageType<SealEnvelopePacket>()
            .RegisterMessageType<OpenEnvelopePacket>()
            .RegisterMessageType<RemapSealerIdPacket>()
            .RegisterMessageType<SaveStampDesignPacket>()
            .SetMessageHandler<SealEnvelopePacket>(OnSealEnvelopePacket)
            .SetMessageHandler<OpenEnvelopePacket>(OnOpenEnvelopePacket)
            .SetMessageHandler<RemapSealerIdPacket>(OnRemapSealerIdPacket)
            .SetMessageHandler<SaveStampDesignPacket>(OnSaveStampDesignPacket);

        StampDatabase = new StampDatabase();
        EnvelopeDatabase = new EnvelopeDatabase();

        base.StartServerSide(api);
    }

    private void OnSaveStampDesignPacket(IServerPlayer fromPlayer, SaveStampDesignPacket packet)
    {
        if (StampDatabase == null)
        {
            throw new InvalidOperationException("The stamp database has not yet been initialized");
        }

        var stampId = StampDatabase.InsertStamp(new Stamp
        {
            Title = packet.Title,
            Design = BooleanArrayPacker.PackToByteArray(packet.Design),
            Dimensions = packet.Dimensions,
            CreatorId = fromPlayer.PlayerUID
        });

        var itemSlot = Helpers.FindCollectibleOfTypeInInventory<ItemWaxSealStamp>(fromPlayer);
        if (itemSlot != null && !itemSlot.Itemstack.Attributes.HasAttribute(StampAttributes.StampId))
        {
            itemSlot.Itemstack.Attributes.SetLong(StampAttributes.StampId, stampId);
            itemSlot.Itemstack.Attributes.SetString(StampAttributes.StampTitle, packet.Title);
        }

        itemSlot?.MarkDirty();
    }

    private void OnSealEnvelopePacket(IServerPlayer fromplayer, SealEnvelopePacket packet)
    {
        fromplayer.Entity.WalkInventory(slot =>
        {
            var contentsId = slot?.Itemstack?.Attributes?.GetString(EnvelopeAttributes.ContentsId);
            if (!string.IsNullOrEmpty(contentsId) && contentsId == packet.ContentsId)
            {
                slot?.Itemstack?.Attributes?.SetString(EnvelopeAttributes.SealerName, fromplayer.PlayerName);
                slot?.MarkDirty();
                return false;
            }

            return true;
        });
    }

    private void OnOpenEnvelopePacket(IServerPlayer fromplayer, OpenEnvelopePacket packet)
    {
        fromplayer.Entity.WalkInventory(slot =>
        {
            var contentsId = slot?.Itemstack?.Attributes?.GetString(EnvelopeAttributes.ContentsId);
            if (contentsId != null && contentsId == packet.ContentsId)
            {
                Api?.Event.EnqueueMainThreadTask(
                    () => { ItemSealableEnvelope.OpenEnvelope(slot, fromplayer, "envelopes:envelope-opened"); },
                    "openenvelope");
                return false;
            }

            return true;
        });
    }

    private void OnRemapSealerIdPacket(IServerPlayer fromplayer, RemapSealerIdPacket packet)
    {
        var itemSlot = fromplayer
            .InventoryManager
            ?.OpenedInventories
            ?.FirstOrDefault(inv => inv.InventoryID == packet.InventoryId)?[packet.SlotId];

        var itemStack = itemSlot?.Itemstack;

        var sealerId = itemStack?.Attributes.GetString(EnvelopeAttributes.SealerId);
        if (!string.IsNullOrEmpty(sealerId))
        {
            var name = Api.World.PlayerByUid(fromplayer.PlayerUID)?.PlayerName;
            itemStack?.Attributes.SetString(EnvelopeAttributes.SealerName, name);
            itemStack?.Attributes.RemoveAttribute(EnvelopeAttributes.SealerId);

            itemSlot?.MarkDirty();
        }
    }
}