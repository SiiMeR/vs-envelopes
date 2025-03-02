using ProtoBuf;

namespace Envelopes.Messages;

[ProtoContract]
public class OpenEnvelopePacket
{
    [ProtoMember(1)] public required string ContentsId;
}