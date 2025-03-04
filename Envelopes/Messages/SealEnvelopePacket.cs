using ProtoBuf;

namespace Envelopes.Messages;

[ProtoContract]
public class SealEnvelopePacket
{
    [ProtoMember(1)] public required string EnvelopeId;
    [ProtoMember(2)] public required long StampId;
}