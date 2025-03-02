using ProtoBuf;

namespace Envelopes.Messages;

[ProtoContract]
public class RemapSealerIdPacket
{
    [ProtoMember(1)]
    public required string InventoryId;

    [ProtoMember(2)] public int SlotId;
}