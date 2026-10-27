using System;
using System.Collections.Generic;
using XianXia.Core.Attributes;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;

namespace XianXia.Core.Combat
{
    public static class CombatInjuryService
    {
        public const int CutChancePercent = 20, SevereChancePercent = 25;
        public const int LostArmChancePercent = 10, LostLegChancePercent = 5;
        public const int MeridianKnockoutChancePercent = 20, MeridianDyingChancePercent = 95;
        public const int CutRecoveryLoad = 250000, SevereRecoveryLoad = 750000;
        static readonly DefinitionId InjuryDefinition = new DefinitionId("base", "combat_injury");

        static SourceRef Source(Entity entity) => new SourceRef(SourceKind.StatusEffect, InjuryDefinition, entity.Id);
        public static CombatInjuryComponent Ensure(Entity entity)
        {
            if (!entity.TryGet<CombatInjuryComponent>(out var injury))
            { injury = new CombatInjuryComponent(); entity.AddComponent(injury); }
            return injury;
        }
        public static bool HasRecoverable(Entity entity) => entity != null &&
            entity.TryGet<CombatInjuryComponent>(out var injury) && injury.HasRecoverableInjury;
        public static bool HasBrokenMeridians(Entity entity) => entity != null &&
            entity.TryGet<CombatInjuryComponent>(out var injury) && injury.MeridiansBroken;
        public static bool CanWalk(Entity entity) => entity != null &&
            (!entity.TryGet<CombatInjuryComponent>(out var injury) || injury.MissingLegs < 2);

        public static void RecordBodyHit(SimulationWorld world, Entity entity, int hpBefore)
        {
            if (world == null || entity == null || !entity.TryGet<CombatVitalsComponent>(out var hp) ||
                hp.CurrentHp >= hpBefore || !entity.TryGet<AttributesComponent>(out var attrs)) return;
            var injury = Ensure(entity);
            var maxHp = Math.Max(1, attrs.GetFinal(AttributeId.MaxHp));
            var changed = false;
            if (injury.CutRecovery == 0 && (long)hp.CurrentHp * 2 < maxHp &&
                world.Random.NextInt(0, 100) < CutChancePercent)
            { injury.CutRecovery = CutRecoveryLoad; changed = true; }
            if (injury.SevereRecovery == 0 && (long)hp.CurrentHp * 4 < maxHp &&
                world.Random.NextInt(0, 100) < SevereChancePercent)
            { injury.SevereRecovery = SevereRecoveryLoad; changed = true; }
            if (changed) RefreshModifiers(entity);
        }

        public static void RecordKnockout(SimulationWorld world, Entity entity)
        {
            var injury = Ensure(entity);
            injury.SevereRecovery = Math.Max(injury.SevereRecovery, SevereRecoveryLoad);
            if ((injury.MissingLimbs & 3) != 3 && world.Random.NextInt(0, 100) < LostArmChancePercent)
                RemoveOneLimb(world, injury, 1, 2);
            if ((injury.MissingLimbs & 12) != 12 && world.Random.NextInt(0, 100) < LostLegChancePercent)
                RemoveOneLimb(world, injury, 4, 8);
            if (!injury.MeridiansBroken && world.Random.NextInt(0, 100) < MeridianKnockoutChancePercent)
                injury.MeridiansBroken = true;
            RefreshModifiers(entity);
        }

        static void RemoveOneLimb(SimulationWorld world, CombatInjuryComponent injury, int left, int right)
        {
            var available = ((injury.MissingLimbs & left) == 0 ? left : 0) |
                ((injury.MissingLimbs & right) == 0 ? right : 0);
            injury.MissingLimbs |= available == (left | right)
                ? (world.Random.NextInt(0, 2) == 0 ? left : right) : available;
        }
        public static void RecordDying(SimulationWorld world, Entity entity)
        {
            var injury = Ensure(entity);
            injury.SevereRecovery = Math.Max(injury.SevereRecovery, SevereRecoveryLoad);
            if (!injury.MeridiansBroken && world.Random.NextInt(0, 100) < MeridianDyingChancePercent)
                injury.MeridiansBroken = true;
            RefreshModifiers(entity);
        }

        public static void AdvanceHealing(Entity entity, int positiveRateMilli)
        {
            if (positiveRateMilli <= 0 || !entity.TryGet<CombatInjuryComponent>(out var injury) ||
                !injury.HasRecoverableInjury || !entity.TryGet<AttributesComponent>(out var attrs) ||
                !entity.TryGet<CombatVitalsComponent>(out var vitals)) return;
            var maxHp = Math.Max(1, attrs.GetFinal(AttributeId.MaxHp));
            var amount = (int)Math.Min(int.MaxValue, Math.Max(1L, (long)positiveRateMilli * 1000 / maxHp));
            // Retain one unit until the body has also recovered enough HP to shed the condition.
            if (injury.CutRecovery > 0)
                injury.CutRecovery = Math.Max((long)vitals.CurrentHp * 2 >= maxHp ? 0 : 1, injury.CutRecovery - amount);
            if (injury.SevereRecovery > 0)
                injury.SevereRecovery = Math.Max((long)vitals.CurrentHp * 4 >= (long)maxHp * 3 ? 0 : 1, injury.SevereRecovery - amount);
            RefreshModifiersIfChanged(entity, injury);
        }

        static int Signature(CombatInjuryComponent injury) => (injury.CutRecovery > 0 ? 16 : 0) |
            (injury.SevereRecovery > 0 ? 32 : 0) | injury.MissingLimbs | (injury.MeridiansBroken ? 64 : 0);
        public static void RefreshModifiersIfChanged(Entity entity, CombatInjuryComponent injury)
        {
            if (injury.AppliedSignature != Signature(injury)) RefreshModifiers(entity);
        }
        public static void RefreshModifiers(Entity entity)
        {
            if (!entity.TryGet<AttributesComponent>(out var attrs)) return;
            var source = Source(entity);
            attrs.RemoveBySource(source);
            if (!entity.TryGet<CombatInjuryComponent>(out var injury)) return;
            injury.AppliedSignature = Signature(injury);
            var acute = injury.SevereRecovery > 0 ? .25 : injury.CutRecovery > 0 ? .05 : 0;
            var arms = injury.MissingArms == 2 ? .9 : injury.MissingArms == 1 ? .6 : 0;
            var attackLoss = Math.Min(.95, acute + arms + (injury.MeridiansBroken ? .35 : 0));
            if (attackLoss > 0) attrs.AddModifier(AttributeId.Attack, ModifierOperation.Percentage, -attackLoss, source);
            var speedLoss = injury.MissingLegs == 2 ? 1 : injury.MissingLegs == 1 ? .6 :
                injury.SevereRecovery > 0 ? .35 : injury.CutRecovery > 0 ? .1 : 0;
            if (speedLoss > 0) attrs.AddModifier(AttributeId.Speed, ModifierOperation.Percentage, -speedLoss, source);
            if (arms > 0) attrs.AddModifier(AttributeId.Physique, ModifierOperation.Percentage, -arms, source);
            if (injury.MeridiansBroken) attrs.AddModifier(AttributeId.SpiritPower, ModifierOperation.Percentage, -.8, source);
            if (entity.TryGet<CombatVitalsComponent>(out var hp) && hp.PoolsInitialized)
                hp.CurrentSpiritPower = Math.Min(hp.CurrentSpiritPower, Math.Max(0, attrs.GetFinal(AttributeId.SpiritPower)));
        }

        public static float GroundMovementMultiplier(Entity entity)
        {
            if (entity == null || !entity.TryGet<CombatInjuryComponent>(out var injury)) return 1f;
            if (injury.MissingLegs == 2) return 0;
            return (injury.MissingLegs == 1 ? .4f : 1f) *
                (injury.SevereRecovery > 0 ? .65f : injury.CutRecovery > 0 ? .9f : 1f);
        }
        public static string Describe(Entity entity)
        {
            if (entity == null || !entity.TryGet<CombatInjuryComponent>(out var injury) || !injury.HasAny) return "无持续伤势";
            var labels = new List<string>();
            if (injury.CutRecovery > 0) labels.Add("割伤（恢复" + (100 - injury.CutRecovery * 100L / CutRecoveryLoad) + "%）");
            if (injury.SevereRecovery > 0) labels.Add("重伤（恢复" + (100 - injury.SevereRecovery * 100L / SevereRecoveryLoad) + "%）");
            if ((injury.MissingLimbs & 1) != 0) labels.Add("左臂缺失");
            if ((injury.MissingLimbs & 2) != 0) labels.Add("右臂缺失");
            if ((injury.MissingLimbs & 4) != 0) labels.Add("左腿缺失");
            if ((injury.MissingLimbs & 8) != 0) labels.Add("右腿缺失");
            if (injury.MeridiansBroken) labels.Add("经脉断裂（无法修炼／突破）");
            return string.Join(" · ", labels);
        }
    }
}
