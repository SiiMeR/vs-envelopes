using System;
using System.IO;
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
    public static string ModId = "envelopes";
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
        MoveOldEnvelopesToDatabase(EnvelopeDatabase);

        base.StartServerSide(api);
    }

    private void MoveOldEnvelopesToDatabase(EnvelopeDatabase envelopeDatabase)
    {
        var modDataPath = Helpers.GetModDataPath();

        Directory.GetFiles(modDataPath, "*")
            .Where(file => Path.GetExtension(file) == string.Empty)
            .ToList()
            .ForEach(
                path =>
                {
                    try
                    {
                        var contents = File.ReadAllBytes(path);
                        var fileName = Path.GetFileNameWithoutExtension(path);

                        var envelope = new Envelope
                        {
                            Id = fileName,
                            CreatorId = "legacy",
                            ItemBlob = contents
                        };

                        envelopeDatabase.InsertEnvelope(envelope);
                        File.Delete(path);
                    }
                    catch (Exception e)
                    {
                        Api?.Logger.Error("Failed to move old envelope to database", e.Message);
                    }
                });
    }

    private void OnSaveStampDesignPacket(IServerPlayer fromPlayer, SaveStampDesignPacket packet)
    {
        if (StampDatabase == null)
        {
            throw new InvalidOperationException("The stamp database has not yet been initialized");
        }

        var nextItem = new ItemStack(Api?.World.GetItem(new AssetLocation("envelopes:sealstamp-engraved")));


        var stampId = StampDatabase.InsertStamp(new Stamp
        {
            Title = packet.Title,
            Design = BooleanArrayPacker.PackToByteArray(packet.Design),
            Dimensions = packet.Dimensions,
            CreatorId = fromPlayer.PlayerUID
        });

        var itemSlot = Helpers.FindCollectibleOfTypeInInventory<ItemWaxSealStamp>(fromPlayer);
        itemSlot?.TakeOut(1);

        nextItem.Attributes.SetLong(StampAttributes.StampId, stampId);
        nextItem.Attributes.SetString(StampAttributes.StampTitle, packet.Title);

        if (!fromPlayer.InventoryManager.TryGiveItemstack(nextItem, true))
        {
            Api?.World.SpawnItemEntity(nextItem, fromPlayer.Entity.SidedPos.XYZ);
        }

        itemSlot?.MarkDirty();
    }

    private void OnSealEnvelopePacket(IServerPlayer fromplayer, SealEnvelopePacket packet)
    {
        var stamp = StampDatabase?.GetStamp(packet.StampId);
        if (stamp == null)
        {
            Api.Logger.Debug($"Unable to seal envelope. Envelope:{packet.EnvelopeId}, Stamp{packet.StampId}");
            return;
        }

        fromplayer.Entity.WalkInventory(slot =>
        {
            var contentsId = slot?.Itemstack?.Attributes?.GetString(EnvelopeAttributes.ContentsId);
            if (!string.IsNullOrEmpty(contentsId) && contentsId == packet.EnvelopeId)
            {
                slot?.Itemstack?.Attributes?.SetLong(StampAttributes.StampId, stamp.Id);
                slot?.Itemstack?.Attributes?.SetString(StampAttributes.StampTitle, stamp.Title);
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
            var contentsId = slot.Itemstack?.Attributes?.GetString(EnvelopeAttributes.ContentsId);
            if (contentsId != null && contentsId == packet.ContentsId)
            {
                ItemSealableEnvelope.OpenEnvelope(slot, fromplayer);
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