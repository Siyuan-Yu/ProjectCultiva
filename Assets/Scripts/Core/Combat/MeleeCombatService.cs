using System;
using XianXia.Core.Attributes;
using XianXia.Core.Content;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Events;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.Npc;
using XianXia.Core.World.Strategic;

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

        internal Result ApplyDispositionRefusal(SimulationWorld world, EntityId attackerId, EntityId defenderId,
            out int damageApplied, out bool defenderDefeated) =>
            ApplyDamageStrike(world, attackerId, defenderId, -1, out damageApplied, out defenderDefeated, dispositionRefusal: true);

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

            var perHit = Math.Max(1, (int)Math.Round(
                attack * SkillMasteryLookup.ResolveDamageAttackMult(art, MasteryTier(arts, equipped.Value))
                ) - defense / 2);

            for (var i = 0; i < hits; i++)
            {
                var hit = ApplyDamageStrike(
                    world, casterId, targetId, perHit,
                    out var dmg, out var defeated);
                if (hit.IsFailure)
                {
                    if (hit.Error.Message == "闪避") continue;
                    if (hitsLanded > 0)
                    {
                        new SkillMasteryService().AddArtMasteryProgress(
                            world, casterId, equipped.Value, SkillMasteryRules.UseArtProgressGain);
                        return Result.Success();
                    }

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

            new SkillMasteryService().AddArtMasteryProgress(
                world, casterId, equipped.Value, SkillMasteryRules.UseArtProgressGain);

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
            out bool defenderDefeated,
            bool dispositionRefusal = false)
        {
            damageApplied = 0;
            defenderDefeated = false;
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "World null.");
            var downedTarget = world.Entities.TryGet(defenderId, out var downed) &&
                downed.TryGet<LifecycleComponent>(out var downedLife) && downedLife.IsIncapacitated;
            if (!downedTarget && CharacterEncounterService.RequiresEntry(world, attackerId, defenderId))
                return Result.Failure(ErrorCode.InvalidOperation, "Independent encounter confirmation required before damage.");
            var continuousCombat = world.Strategic?.ContinuousManualCombat;
            if (continuousCombat != null && continuousCombat.IsActive &&
                !continuousCombat.AreOpposing(attackerId, defenderId))
                return Result.Failure(
                    ErrorCode.InvalidOperation,
                    "Target is not an opposing participant in the active manual battle.");
            if (!world.Entities.TryGet(attackerId, out var attacker))
                return Result.Failure(ErrorCode.EntityNotFound, "Attacker missing.");
            var transportPermission = MortalTransportConstraintService.ValidateAttack(world, attackerId);
            if (transportPermission.IsFailure) return transportPermission;
            if (!world.Entities.TryGet(defenderId, out var defender))
                return Result.Failure(ErrorCode.EntityNotFound, "Defender missing.");
            if (attacker.TryGet<XianXia.Core.Social.FactionMembershipComponent>(out var af) &&
                defender.TryGet<XianXia.Core.Social.FactionMembershipComponent>(out var df) &&
                PeaceNegotiationService.HasPeace(world, af.FactionId, df.FactionId))
                return Result.Failure(ErrorCode.InvalidOperation, "双方已签订停战；重新宣战后才能攻击。");
            if (PartyRestService.BlocksMovement(attacker))
                return Result.Failure(ErrorCode.InvalidOperation, "精力耗尽，无法攻击。");
            if (!CombatLifeStateService.CanFight(attacker))
                return Result.Failure(ErrorCode.InvalidOperation, "Attacker cannot fight.");
            if (!CombatLifeStateService.CanBeAttacked(defender))
                return Result.Failure(ErrorCode.InvalidOperation, "Defender already down.");

            CombatDamageRules.EnsureVitals(attacker);
            CombatDamageRules.EnsureVitals(defender);
            if (!defender.TryGet<CombatVitalsComponent>(out var vitals))
                return Result.Failure(ErrorCode.InvalidOperation, "Defender has no HP.");

            if (defender.TryGet<LifecycleComponent>(out var defLife) && defLife.IsIncapacitated)
            {
                if (!dispositionRefusal && DefeatedMercyService.IsEncounterOpponent(world, attackerId, defenderId) &&
                    (defLife.IsUnconscious || defLife.Convalescing))
                    return Result.Failure(ErrorCode.InvalidOperation, "请先右键进行战败处置；不能绕过求饶选择直接补击。");
                if ((defLife.IsUnconscious || defLife.Convalescing) && !Social.SupervisorAngerDriver.IsNonlethalAttacker(world, attackerId))
                {
                    defenderDefeated = CombatLifeStateService.TryEnterDyingFromUnconscious(world, defender, attackerId);
                    if (defenderDefeated) damageApplied = 1;
                }
                return Result.Success();
            }

            if (FlightService.IsAirborne(attacker) != FlightService.IsAirborne(defender) &&
                !SpiritVeilService.IsActive(attacker))
                return Result.Failure(ErrorCode.InvalidOperation, "高度不同，需远程攻击。");
            if (FlightService.TryEvade(world, attacker, defender))
                return Result.Failure(ErrorCode.InvalidOperation, "闪避");
            var damage = damageOverride >= 0
                ? Math.Max(1, damageOverride)
                : ComputeStrikeDamage(world, attacker, defender);
            var hpBefore = vitals.CurrentHp;
            damageApplied = CombatDamageRules.ApplyIncoming(defender, damage);
            CombatInjuryService.RecordBodyHit(world, defender, hpBefore);
            if (damageApplied > 0) XianXia.Core.Npc.PrisonManagementService.RecordAttack(world, defenderId);
            if (vitals.CurrentHp > CombatLifeStateService.DefeatHpFloor(defender) || vitals.CurrentHp >= hpBefore)
                return Result.Success();

            defenderDefeated = true;
            CombatLifeStateService.TryEnterUnconscious(world, defender, attackerId);

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
                var tier = MasteryTier(arts, id.Value);
                bonusPct += SkillMasteryLookup.ResolveAttackBonusPercent(art, tier);
                flat += SkillMasteryLookup.ResolveDamageFlat(art, tier);
            }

            if (bonusPct <= 0.0 && flat == 0)
                return raw;
            var scaled = raw * (1.0 + bonusPct) + flat;
            return Math.Max(1, (int)Math.Round(scaled));
        }

        static SkillMasteryTier MasteryTier(CombatArtsComponent arts, DefinitionId artId)
        {
            var m = arts?.GetMastery(artId);
            return m != null ? m.Tier : SkillMasteryTier.Entry;
        }
    }
}
