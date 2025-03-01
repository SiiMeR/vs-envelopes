using ProtoBuf;
using Vintagestory.GameContent;

namespace Envelopes;

[ProtoContract]
public class OpenEnvelopePacket
{
    [ProtoMember(1)]
    public string ContentsId;
}
