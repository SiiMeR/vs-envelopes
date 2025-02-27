﻿using ProtoBuf;

namespace Envelopes;

[ProtoContract]
public class RemapSealerIdPacket
{
    [ProtoMember(1)]
    public string InventoryId;

    [ProtoMember(2)] public int SlotId;
}