using System;
using XianXia.Core.Attributes;
using XianXia.Core.Entities;

namespace XianXia.Core.Combat
{
    /// <summary>战略建筑近战伤害的薄共享公式。</summary>
    public static class StrategicBuildingDamage
    {
        public static int Compute(Entity attacker, int defense)
        {
            var attack = 1;
            if (attacker != null && attacker.TryGet<AttributesComponent>(out var attrs))
                attack = Math.Max(1, attrs.GetFinal(AttributeId.Attack));
            return Math.Max(1, attack - Math.Max(0, defense) / 2);
        }
    }
}
