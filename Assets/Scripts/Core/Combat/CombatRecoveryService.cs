using XianXia.Core.Attributes;
using XianXia.Core.Entities;
using XianXia.Core.Results;

namespace XianXia.Core.Combat
{
    public static class CombatRecoveryService
    {
        public static Result CanRecover(Entity entity)
        {
            if (entity == null)
                return Result.Failure(ErrorCode.EntityNotFound, "恢复角色不存在。");
            if (!entity.TryGet<LifecycleComponent>(out var life))
                return Result.Failure(ErrorCode.ComponentMissing, "恢复角色缺少生命周期状态。");
            if (life.IsDead || life.IsRemoved || life.IsIncapacitated)
                return Result.Failure(ErrorCode.ActionCannotStart, "当前生命状态不能恢复。", life.State.ToString());
            if (!entity.TryGet<AttributesComponent>(out var attrs))
                return Result.Failure(ErrorCode.ComponentMissing, "恢复角色缺少属性。");
            CombatDamageRules.EnsureVitals(entity);
            if (!entity.TryGet<CombatVitalsComponent>(out var vitals))
                return Result.Failure(ErrorCode.ComponentMissing, "恢复角色缺少战斗池。");
            var maxHp = System.Math.Max(1, attrs.GetFinal(AttributeId.MaxHp));
            var maxSpirit = System.Math.Max(0, attrs.GetFinal(AttributeId.SpiritPower));
            if (vitals.CurrentHp >= maxHp && vitals.CurrentSpiritPower >= maxSpirit)
                return Result.Failure(ErrorCode.InvalidOperation, "生命与灵力已满，无需恢复。");
            return Result.Success();
        }

        public static Result RestoreToMaximum(Entity entity)
        {
            var can = CanRecover(entity);
            if (can.IsFailure) return can;
            entity.TryGet<AttributesComponent>(out var attrs);
            entity.TryGet<CombatVitalsComponent>(out var vitals);
            vitals.CurrentHp = System.Math.Max(1, attrs.GetFinal(AttributeId.MaxHp));
            vitals.CurrentSpiritPower = System.Math.Max(0, attrs.GetFinal(AttributeId.SpiritPower));
            vitals.PoolsInitialized = true;
            return Result.Success();
        }
    }
}
