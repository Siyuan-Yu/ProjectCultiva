using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Events;
using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.Combat
{
    /// <summary>斗气纱衣：召唤／卸下／射程解析／空灵力与脱战卸下。</summary>
    public sealed class SpiritVeilService
    {
        public static bool IsActive(Entity entity) =>
            entity != null &&
            entity.TryGet<SpiritVeilComponent>(out var veil) &&
            veil.IsActive;

        public float ResolveEngageRange(Entity entity)
        {
            if (!IsActive(entity))
                return SpiritVeilRules.MeleeEngageRange;
            if (!entity.TryGet<CultivationComponent>(out var cult) ||
                !SpiritVeilRules.CanUseRealm(cult.Realm))
                return SpiritVeilRules.MeleeEngageRange;
            return SpiritVeilRules.RangedEngageRange(cult.Realm);
        }

        public Result TryActivate(SimulationWorld world, EntityId entityId)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "World null.");
            if (!world.Entities.TryGet(entityId, out var entity))
                return Result.Failure(ErrorCode.EntityNotFound, "Entity missing.");
            return TryActivateEntity(world, entity);
        }

        /// <summary>
        /// 非玩家（Npc）在交战开始时：筑基+且灵力够则自动召唤纱衣。
        /// 玩家单位不自动开，仍走手动 F2。
        /// </summary>
        public Result TryAutoActivateForNonPlayer(SimulationWorld world, EntityId entityId)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "World null.");
            if (!world.Entities.TryGet(entityId, out var entity))
                return Result.Failure(ErrorCode.EntityNotFound, "Entity missing.");
            if ((entity.Tags & EntityTag.Npc) == 0)
                return Result.Failure(ErrorCode.InvalidOperation, "仅非玩家自动召唤。");
            if (IsActive(entity))
                return Result.Success();
            return TryActivateEntity(world, entity);
        }

        Result TryActivateEntity(SimulationWorld world, Entity entity)
        {
            if (entity == null)
                return Result.Failure(ErrorCode.EntityNotFound, "Entity missing.");
            if (!entity.TryGet<CultivationComponent>(out var cult) ||
                !SpiritVeilRules.CanUseRealm(cult.Realm))
                return Result.Failure(ErrorCode.InvalidOperation, "筑基后方可召唤斗气纱衣。");

            CombatDamageRules.EnsureVitals(entity);
            if (!entity.TryGet<CombatVitalsComponent>(out var vitals))
                return Result.Failure(ErrorCode.ComponentMissing, "CombatVitals missing.");

            if (IsActive(entity))
                return Result.Failure(ErrorCode.InvalidOperation, "斗气纱衣已展开。");

            var cost = SpiritVeilRules.ActivateSpiritCost(cult.Realm);
            // 扣完后至少留 1，否则立刻触发「灵力打空卸下」
            if (vitals.CurrentSpiritPower <= cost)
                return Result.Failure(
                    ErrorCode.InvalidOperation,
                    "灵力不足（需 " + (cost + 1) + "，当前 " + vitals.CurrentSpiritPower + "）。");

            if (!EnsureComponent(entity, out var veil))
                return Result.Failure(ErrorCode.InvalidOperation, "无法挂载纱衣组件。");

            vitals.CurrentSpiritPower -= cost;
            veil.IsActive = true;

            world.Events.Publish(
                EventType.ActionCompleted,
                world.Tick,
                actor: entity.Id,
                payload: "spiritVeil:on;cost=" + cost);
            return Result.Success();
        }

        public Result TryDeactivate(SimulationWorld world, EntityId entityId, string reason = null)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "World null.");
            if (!world.Entities.TryGet(entityId, out var entity))
                return Result.Failure(ErrorCode.EntityNotFound, "Entity missing.");
            if (!IsActive(entity))
                return Result.Failure(ErrorCode.InvalidOperation, "未展开斗气纱衣。");

            Deactivate(entity);
            world.Events.Publish(
                EventType.ActionCompleted,
                world.Tick,
                actor: entityId,
                payload: "spiritVeil:off;" + (reason ?? "manual"));
            return Result.Success();
        }

        public bool DeactivateIfSpiritEmpty(Entity entity)
        {
            if (!IsActive(entity))
                return false;
            CombatDamageRules.EnsureVitals(entity);
            if (!entity.TryGet<CombatVitalsComponent>(out var vitals) ||
                vitals.CurrentSpiritPower > 0)
                return false;
            Deactivate(entity);
            return true;
        }

        public void DeactivateOnCombatEnd(Entity a, Entity b)
        {
            Deactivate(a);
            Deactivate(b);
        }

        public static void Deactivate(Entity entity)
        {
            if (entity != null && entity.TryGet<SpiritVeilComponent>(out var veil))
                veil.IsActive = false;
        }

        static bool EnsureComponent(Entity entity, out SpiritVeilComponent veil)
        {
            if (entity.TryGet(out veil))
                return true;
            veil = new SpiritVeilComponent();
            return entity.AddComponent(veil).IsSuccess;
        }
    }
}
