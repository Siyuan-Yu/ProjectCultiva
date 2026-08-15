using System;
using XianXia.Core.Attributes;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Events;
using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.Combat
{
    /// <summary>战斗 Alpha：普攻互砍＋主动斗技释放（连击／倍率）。</summary>
    public sealed class MeleeCombatService
    {
        public const float DefaultMeleeIntervalSeconds = 0.85f;
        public const float DefaultMeleeRange = 1.6f;

        public int ComputeStrikeDamage(SimulationWorld world, Entity attacker, Entity defender)
        {
            if (attacker == null || defender == null)
                return 0;
            if (!attacker.TryGet<AttributesComponent>(out var atkAttrs))
                return 1;
            var attack = Math.Max(1, atkAttrs.GetFinal(AttributeId.Attack));
            var defense = 0;
            if (defender.TryGet<AttributesComponent>(out var defAttrs))
                defense = Math.Max(0, defAttrs.GetFinal(AttributeId.Defense));

            var raw = Math.Max(1, attack - defense / 2);
            raw = ApplyEquippedArtBonus(world, attacker, raw);
            return Math.Max(1, raw);
        }

        public Result ApplyStrike(
            SimulationWorld world,
            EntityId attackerId,
            EntityId defenderId,
            out int damageApplied,
            out bool defenderDefeated)
        {
            return ApplyDamageStrike(
                world, attackerId, defenderId,
                damageOverride: -1,
                out damageApplied, out defenderDefeated);
        }

        /// <summary>
        /// 释放装备栏主动斗技：按 <see cref="CombatArtSpec.HitCount"/> 段，
        /// 每段伤害＝攻击力 × <see cref="CombatArtSpec.DamageAttackMult"/>（再减半防）。
        /// </summary>
        public Result CastEquippedArt(
            SimulationWorld world,
            EntityId casterId,
            EntityId targetId,
            int slotIndex,
            out int totalDamage,
            out int hitsLanded,
            out bool targetDefeated)
        {
            totalDamage = 0;
            hitsLanded = 0;
            targetDefeated = false;
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "World null.");
            if (!world.Entities.TryGet(casterId, out var caster))
                return Result.Failure(ErrorCode.EntityNotFound, "Caster missing.");
            if (!caster.TryGet<CombatArtsComponent>(out var arts))
                return Result.Failure(ErrorCode.ComponentMissing, "CombatArts missing.");
            var equipped = arts.GetEquipped(slotIndex);
            if (!equipped.HasValue)
                return Result.Failure(ErrorCode.InvalidOperation, "Slot empty.", slotIndex.ToString());
            if (!world.TryGetCombatArt(equipped.Value, out var art) || art == null)
                return Result.Failure(ErrorCode.NotFound, "Art missing.", equipped.Value.ToString());
            if (!art.IsActiveSkill)
                return Result.Failure(ErrorCode.InvalidOperation, "Art is not an active skill.", art.Name);

            var hits = art.HitCount < 1 ? 1 : art.HitCount;
            if (!caster.TryGet<AttributesComponent>(out var atkAttrs))
                return Result.Failure(ErrorCode.ComponentMissing, "Attributes missing.");
            var attack = Math.Max(1, atkAttrs.GetFinal(AttributeId.Attack));
            var defense = 0;
            if (world.Entities.TryGet(targetId, out var target) &&
                target.TryGet<AttributesComponent>(out var defAttrs))
                defense = Math.Max(0, defAttrs.GetFinal(AttributeId.Defense));

            var perHit = Math.Max(1, (int)Math.Round(attack * art.DamageAttackMult) - defense / 2);

            for (var i = 0; i < hits; i++)
            {
                var hit = ApplyDamageStrike(
                    world, casterId, targetId, perHit,
                    out var dmg, out var defeated);
                if (hit.IsFailure)
                {
                    if (hitsLanded > 0)
                        return Result.Success();
                    return hit;
                }

                totalDamage += dmg;
                hitsLanded++;
                if (defeated)
                {
                    targetDefeated = true;
                    break;
                }
            }

            world.Events.Publish(
                EventType.ActionCompleted,
                world.Tick,
                actor: casterId,
                target: targetId,
                payload: "castArt:" + art.Id + ";hits=" + hitsLanded + ";dmg=" + totalDamage);
            return Result.Success();
        }

        Result ApplyDamageStrike(
            SimulationWorld world,
            EntityId attackerId,
            EntityId defenderId,
            int damageOverride,
            out int damageApplied,
            out bool defenderDefeated)
        {
            damageApplied = 0;
            defenderDefeated = false;
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "World null.");
            if (!world.Entities.TryGet(attackerId, out var attacker))
                return Result.Failure(ErrorCode.EntityNotFound, "Attacker missing.");
            if (!world.Entities.TryGet(defenderId, out var defender))
                return Result.Failure(ErrorCode.EntityNotFound, "Defender missing.");
            if (!attacker.TryGet<LifecycleComponent>(out var atkLife) || atkLife.IsDead || atkLife.IsRemoved)
                return Result.Failure(ErrorCode.InvalidOperation, "Attacker cannot fight.");
            if (!defender.TryGet<LifecycleComponent>(out var defLife) || defLife.IsDead || defLife.IsRemoved)
                return Result.Failure(ErrorCode.InvalidOperation, "Defender already down.");

            CombatDamageRules.EnsureVitals(attacker);
            CombatDamageRules.EnsureVitals(defender);
            if (!defender.TryGet<CombatVitalsComponent>(out var vitals) || vitals.CurrentHp <= 0)
                return Result.Failure(ErrorCode.InvalidOperation, "Defender has no HP.");

            var damage = damageOverride >= 0
                ? Math.Max(1, damageOverride)
                : ComputeStrikeDamage(world, attacker, defender);
            damageApplied = CombatDamageRules.ApplyIncoming(defender, damage);
            if (vitals.CurrentHp > 0)
                return Result.Success();

            defenderDefeated = true;
            if ((defender.Tags & EntityTag.Npc) != 0)
            {
                defLife.State = LifecycleState.Dead;
                if (defender.TryGet<EncounterLinkComponent>(out var link) &&
                    !string.IsNullOrEmpty(link.EncounterId))
                {
                    StoryFlagService.Set(
                        world,
                        ContentConditionEvaluator.EncounterFlag(link.EncounterId),
                        attackerId);
                }
            }

            world.Events.Publish(
                EventType.CombatantDefeated,
                world.Tick,
                actor: attackerId,
                target: defenderId,
                payload: damageApplied.ToString());
            QuestProgressRefresh.AfterWorldChange(world, attackerId);
            return Result.Success();
        }

        static int ApplyEquippedArtBonus(SimulationWorld world, Entity attacker, int raw)
        {
            if (world == null || attacker == null || raw <= 0)
                return raw;
            if (!attacker.TryGet<CombatArtsComponent>(out var arts))
                return raw;

            var bonusPct = 0.0;
            var flat = 0;
            for (var i = 0; i < CombatArtsComponent.MaxEquippedSlots; i++)
            {
                var id = arts.GetEquipped(i);
                if (!id.HasValue)
                    continue;
                if (!world.TryGetCombatArt(id.Value, out var art) || art == null)
                    continue;
                if (art.IsActiveSkill)
                    continue;
                bonusPct += art.AttackBonusPercent;
                flat += art.DamageFlat;
            }

            if (bonusPct <= 0.0 && flat == 0)
                return raw;
            var scaled = raw * (1.0 + bonusPct) + flat;
            return Math.Max(1, (int)Math.Round(scaled));
        }
    }
}
