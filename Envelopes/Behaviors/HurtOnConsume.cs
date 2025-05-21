using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace Envelopes.Behaviors;

public class HurtOnConsume : CollectibleBehavior
{
    public float DamageAmount = 1;

    public HurtOnConsume(CollectibleObject collObj) : base(collObj)
    {
    }

    public override void Initialize(JsonObject properties)
    {
        DamageAmount = properties["damageAmount"].AsFloat();

        base.Initialize(properties);
    }

    public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity,
        BlockSelection blockSel,
        EntitySelection entitySel, ref EnumHandling handling)
    {
        if (secondsUsed < 1)
        {
            base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel, ref handling);
            return;
        }

        if (byEntity is EntityPlayer player)
        {
            player?.GetBehavior<EntityBehaviorHealth>()?.OnEntityReceiveDamage(new DamageSource
            {
                DamageTier = 5,
                Source = EnumDamageSource.Internal,
                Type = EnumDamageType.Injury,
            }, ref DamageAmount);
        }

        base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel, ref handling);
    }
}