using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;

namespace XianXia.Core.Combat
{
    public sealed class CombatDeathAttributionComponent : IComponent
    {
        public EntityId ResponsibleAttackerId { get; private set; } = EntityId.None;

        public void Set(EntityId attackerId) => ResponsibleAttackerId = attackerId;

        public void Clear() => ResponsibleAttackerId = EntityId.None;
    }
}
