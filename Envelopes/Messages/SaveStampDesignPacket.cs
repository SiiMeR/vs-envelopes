using ProtoBuf;

namespace Envelopes.Messages;

[ProtoContract]
public class SaveStampDesignPacket
{
    [ProtoMember(2)] public required bool[] Design;
    [ProtoMember(3)] public required int Dimensions;
    [ProtoMember(1)] public required string Title;
}