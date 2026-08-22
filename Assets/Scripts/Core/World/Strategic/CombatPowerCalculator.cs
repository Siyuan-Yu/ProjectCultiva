using System;
using System.Collections.Generic;
using XianXia.Core.Attributes;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    public static class CombatPowerCalculator
    {
        public static int SumPartyPower(SimulationWorld world, IReadOnlyList<EntityId> party)
        {
            if (world == null || party == null || party.Count == 0)
                return 1;
            var sum = 0;
            for (var i = 0; i < party.Count; i++)
                sum += ForEntity(world, party[i]);
            return Math.Max(1, sum);
        }

        public static int ForEntity(SimulationWorld world, EntityId id)
        {
            if (world == null || id.IsNone || !world.Entities.TryGet(id, out var entity) || entity == null)
                return 1;

            var power = RealmWeight(entity);
            if (entity.TryGet<AttributesComponent>(out var attrs) && attrs != null)
            {
                var speed = attrs.GetFinal(AttributeId.Speed);
                if (speed > 10)
                    power = (int)Math.Round(power * 1.1f);
            }

            return Math.Max(1, power);
        }

        public static int ForArmyStack(ArmyStack stack)
        {
            if (stack == null)
                return 1;
            var basePower = stack.CombatPower > 0 ? stack.CombatPower : 1;
            var count = stack.MemberCount > 0 ? stack.MemberCount : 1;
            return Math.Max(1, basePower * count);
        }

        static int RealmWeight(Entity entity)
        {
            if (entity == null || !entity.TryGet<CultivationComponent>(out var cult) || cult == null)
                return 1;
            switch (cult.Realm)
            {
                case RealmStage.Foundation:
                    return 10;
                case RealmStage.QiRefining:
                    return 3;
                default:
                    return 1;
            }
        }

        public static int EstimateAutoWinPercent(int playerPower, int enemyPower)
        {
            var p = Math.Max(1, playerPower);
            var e = Math.Max(1, enemyPower);
            // Logistic 曲线 + 略向 50% 收束：避免 UI 战力比与掷骰结果体感差过大
            var logit = Math.Log(p / (double)e);
            var rate = 1.0 / (1.0 + Math.Exp(-logit * 0.9));
            rate = 0.5 + (rate - 0.5) * 0.85;
            return (int)Math.Round(Math.Clamp(rate, 0.08, 0.92) * 100.0);
        }
    }
}
