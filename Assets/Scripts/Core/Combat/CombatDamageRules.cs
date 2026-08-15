using XianXia.Core.Attributes;
using XianXia.Core.Cultivation;
using XianXia.Core.Entities;

namespace XianXia.Core.Combat
{
    /// <summary>
    /// Incoming damage: 炼气+ 时先扣灵力护盾，再扣当前生命。
    /// </summary>
    public static class CombatDamageRules
    {
        public static int ApplyIncoming(Entity entity, int damage)
        {
            if (entity == null || damage <= 0)
                return 0;

            EnsureVitals(entity);
            if (!entity.TryGet<CombatVitalsComponent>(out var vitals))
                return 0;

            var remaining = damage;
            var canShield = entity.TryGet<CultivationComponent>(out var cult) &&
                            cult.Realm >= RealmStage.QiRefining &&
                            vitals.CurrentSpiritPower > 0;
            if (canShield)
            {
                var absorbed = vitals.CurrentSpiritPower < remaining
                    ? vitals.CurrentSpiritPower
                    : remaining;
                vitals.CurrentSpiritPower -= absorbed;
                remaining -= absorbed;
            }

            if (remaining <= 0)
                return damage;

            var hpBefore = vitals.CurrentHp;
            vitals.CurrentHp -= remaining;
            if (vitals.CurrentHp < 0)
                vitals.CurrentHp = 0;
            return hpBefore - vitals.CurrentHp + (damage - remaining);
        }

        public static void EnsureVitals(Entity entity)
        {
            if (entity == null)
                return;
            if (!entity.TryGet<CombatVitalsComponent>(out var vitals))
            {
                vitals = new CombatVitalsComponent();
                var added = entity.AddComponent(vitals);
                if (added.IsFailure)
                    return;
            }

            if (entity.TryGet<AttributesComponent>(out var attrs))
            {
                // 仅首次灌满灵力护盾；勿在 EnsureVitals 里把空盾回满（否则互砍打不死炼气单位）。
                var fillSpirit = !vitals.PoolsInitialized &&
                                 entity.TryGet<CultivationComponent>(out var cult) &&
                                 cult.Realm >= RealmStage.QiRefining;
                vitals.SyncMaxFromAttributes(attrs, fillSpirit);
            }
        }
    }
}
