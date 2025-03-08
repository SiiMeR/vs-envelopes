using ProtoBuf;

namespace Envelopes.Messages;

[ProtoContract]
public class SetEnvelopeFromToPacket
{
    [ProtoMember(1)] public string? From;

    [ProtoMember(2)] public string? To;

    [ProtoMember(3)] public required string InventoryId;

    [ProtoMember(4)] public int SlotId;
}