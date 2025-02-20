using ProtoBuf;

namespace Envelopes;

[ProtoContract]
public class SealEnvelopePacket
{
    [ProtoMember(1)]
    public string ContentsId;
}