using System;
using System.IO;
using System.Linq;
using Envelopes.Behaviors;
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
    public static EnvelopeContentsDatabase? EnvelopeDatabase;

    public override void Start(ICoreAPI api)
    {
        Api = api;
        ModId = Mod.Info.ModID;
        api.RegisterCollectibleBehaviorClass("Addressable", typeof(AddressableBehavior));
        api.RegisterCollectibleBehaviorClass("HurtOnConsume", typeof(HurtOnConsume));
        api.RegisterCollectibleBehaviorClass("SealColorFromAttributes", typeof(SetSealColorFromAttributes));
        api.RegisterCollectibleBehaviorClass("RenderStampEmblem", typeof(RenderStampEmblem));
        api.RegisterItemClass("ItemSealableEnvelope", typeof(ItemSealableEnvelope));
        api.RegisterItemClass("ItemWaxSealStamp", typeof(ItemWaxSealStamp));
        api.RegisterItemClass("ItemWaxStick", typeof(ItemWaxStick));

        api.Network.RegisterChannel("envelopes")
            .RegisterMessageType<OpenEnvelopePacket>()
            .RegisterMessageType<RemapSealerIdPacket>()
            .RegisterMessageType<SaveStampDesignPacket>()
            .RegisterMessageType<SetEnvelopeFromToPacket>()
            .RegisterMessageType<AddStampDesignAttributePacket>();
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        ClientNetworkChannel = api.Network.GetChannel("envelopes");
        base.StartClientSide(api);
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        ServerNetworkChannel = api.Network.GetChannel("envelopes")
            .SetMessageHandler<OpenEnvelopePacket>(OnOpenEnvelopePacket)
            .SetMessageHandler<RemapSealerIdPacket>(OnRemapSealerIdPacket)
            .SetMessageHandler<SaveStampDesignPacket>(OnSaveStampDesignPacket)
            .SetMessageHandler<SetEnvelopeFromToPacket>(OnSetEnvelopeFromToPacket)
            .SetMessageHandler<AddStampDesignAttributePacket>(OnSetStampDesignAttributePacket);

        StampDatabase = new StampDatabase();
        EnvelopeDatabase = new EnvelopeContentsDatabase();
        MoveOldEnvelopesToDatabase(EnvelopeDatabase);

        base.StartServerSide(api);
    }

    private void OnSetStampDesignAttributePacket(IServerPlayer fromPlayer, AddStampDesignAttributePacket packet)
    {
        var heldItem = fromPlayer.InventoryManager.ActiveHotbarSlot?.Itemstack;
        if (heldItem == null)
        {
            return;
        }

        if (!heldItem.Attributes.HasAttribute(StampAttributes.StampId))
        {
            return;
        }

        if (heldItem.Attributes.HasAttribute(StampAttributes.StampDesign))
        {
            return;
        }

        var stampId = heldItem.Attributes.GetLong(StampAttributes.StampId);
        var stamp = StampDatabase?.GetStamp(stampId);
        if (stamp == null)
        {
            return;
        }

        var design = BooleanArrayPacker.UnpackFromByteArray(stamp.Design);
        heldItem.Attributes.SetString(StampAttributes.StampDesign,
            string.Join(string.Empty, design.Select(on => on ? 1 : 0)));

        fromPlayer.InventoryManager.ActiveHotbarSlot?.MarkDirty();
    }

    private void OnSetEnvelopeFromToPacket(IServerPlayer fromPlayer, SetEnvelopeFromToPacket packet)
    {
        var itemSlot = fromPlayer.InventoryManager
            ?.OpenedInventories
            ?.FirstOrDefault(inv => inv.InventoryID == packet.InventoryId)?[packet.SlotId];

        if (itemSlot == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(packet.From))
        {
            itemSlot.Itemstack?.Attributes.SetString(EnvelopeAttributes.From, packet.From);
        }

        if (!string.IsNullOrWhiteSpace(packet.To))
        {
            itemSlot.Itemstack?.Attributes.SetString(EnvelopeAttributes.To, packet.To);
        }

        itemSlot.MarkDirty();
    }

    private void MoveOldEnvelopesToDatabase(EnvelopeContentsDatabase envelopeContentsDatabase)
    {
        var modDataPath = Helpers.GetModDataPath();

        Directory.GetFiles(modDataPath, "*")
            .Where(file => Path.GetExtension(file) == string.Empty)
            .ToList()
            .ForEach(path =>
            {
                try
                {
                    var contents = File.ReadAllBytes(path);
                    var fileName = Path.GetFileNameWithoutExtension(path);

                    var envelope = new EnvelopeContents
                    {
                        Id = fileName,
                        CreatorId = "legacy",
                        ItemBlob = contents
                    };

                    envelopeContentsDatabase.InsertEnvelope(envelope);
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
        nextItem.Attributes.SetString(StampAttributes.StampDesign,
            string.Join(string.Empty, packet.Design.Select(on => on ? 1 : 0)));

        if (!fromPlayer.InventoryManager.TryGiveItemstack(nextItem, true))
        {
            Api?.World.SpawnItemEntity(nextItem, fromPlayer.Entity.SidedPos.XYZ);
        }

        itemSlot?.MarkDirty();
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