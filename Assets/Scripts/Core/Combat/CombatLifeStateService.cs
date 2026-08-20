using System;
using XianXia.Core.Content;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Events;
using XianXia.Core.Simulation;

namespace XianXia.Core.Combat
{
    /// <summary>
    /// 0 血 → 弥留（Incapacitated）；弥留再受击 → 死亡 + 尸体；
    /// 尸体按修为留存若干游戏日后移除。
    /// </summary>
    public static class CombatLifeStateService
    {
        public const int DefaultCorpseGameDays = 2;

        public static bool CanFight(Entity entity)
        {
            if (entity == null || !entity.TryGet<LifecycleComponent>(out var life))
                return false;
            return life.State == LifecycleState.Alive;
        }

        public static bool CanBeAttacked(Entity entity)
        {
            if (entity == null || !entity.TryGet<LifecycleComponent>(out var life))
                return false;
            if (life.IsRemoved)
                return false;
            return life.State == LifecycleState.Alive || life.IsIncapacitated;
        }

        public static bool IsDown(Entity entity)
        {
            if (entity == null || !entity.TryGet<LifecycleComponent>(out var life))
                return false;
            return life.IsIncapacitated || life.IsDead;
        }

        public static bool HasVisibleCorpse(Entity entity)
        {
            if (entity == null || !entity.TryGet<LifecycleComponent>(out var life) || !life.IsDead)
                return false;
            return entity.TryGet<CorpseComponent>(out _);
        }

        public static bool ShouldHideFromSpawn(Entity entity)
        {
            if (entity == null || !entity.TryGet<LifecycleComponent>(out var life))
                return true;
            return life.IsRemoved;
        }

        /// <summary>状态面板左上角：弥留／尸体；无则 null。</summary>
        public static string ResolveLifeStateLabel(Entity entity)
        {
            if (entity == null || !entity.TryGet<LifecycleComponent>(out var life))
                return null;
            if (life.IsIncapacitated)
                return "弥留";
            if (life.IsDead && entity.TryGet<CorpseComponent>(out _))
                return "尸体";
            return null;
        }

        public static int ResolveCorpseGameDays(Entity entity)
        {
            if (entity == null || !entity.TryGet<CultivationComponent>(out var cult) || cult == null)
                return DefaultCorpseGameDays;
            if (cult.Realm >= RealmStage.Foundation)
                return 5;
            if (cult.Realm >= RealmStage.QiRefining)
                return 3;
            return DefaultCorpseGameDays;
        }

        public static ulong ResolveCorpseLifetimeTicks(Entity entity) =>
            (ulong)Math.Max(1, ResolveCorpseGameDays(entity)) * (ulong)WorldTick.TicksPerDay;

        public static bool TryEnterIncapacitated(SimulationWorld world, Entity entity)
        {
            if (world == null || entity == null || !entity.TryGet<LifecycleComponent>(out var life))
                return false;
            if (life.State != LifecycleState.Alive)
                return false;

            CombatDamageRules.EnsureVitals(entity);
            if (entity.TryGet<CombatVitalsComponent>(out var vitals))
                vitals.CurrentHp = 0;

            life.State = LifecycleState.Incapacitated;
            return true;
        }

        public static bool TryConfirmDeath(
            SimulationWorld world,
            EntityId attackerId,
            Entity target,
            out bool confirmed)
        {
            confirmed = false;
            if (world == null || target == null || !target.TryGet<LifecycleComponent>(out var life))
                return false;
            if (life.IsDead || life.IsRemoved)
                return false;
            if (!life.IsIncapacitated && life.State != LifecycleState.Alive)
                return false;

            // 允许对仍 Alive 但 0 血的边界情况补刀
            if (life.State == LifecycleState.Alive)
            {
                CombatDamageRules.EnsureVitals(target);
                if (target.TryGet<CombatVitalsComponent>(out var vitals) && vitals.CurrentHp > 0)
                    return false;
            }

            life.State = LifecycleState.Dead;
            EnsureCorpse(world, target);

            if ((target.Tags & EntityTag.Npc) != 0 &&
                target.TryGet<EncounterLinkComponent>(out var link) &&
                !string.IsNullOrEmpty(link.EncounterId))
            {
                StoryFlagService.Set(
                    world,
                    ContentConditionEvaluator.EncounterFlag(link.EncounterId),
                    attackerId);
            }

            confirmed = true;
            world.Events.Publish(
                EventType.CombatantDefeated,
                world.Tick,
                actor: attackerId,
                target: target.Id,
                payload: "lethal");
            QuestProgressRefresh.AfterWorldChange(world, attackerId);
            return true;
        }

        static void EnsureCorpse(SimulationWorld world, Entity entity)
        {
            if (entity == null || world == null)
                return;
            if (!entity.TryGet<CorpseComponent>(out var corpse))
            {
                corpse = new CorpseComponent();
                entity.AddComponent(corpse);
            }

            corpse.RemoveAfterTick = world.Tick.Value + ResolveCorpseLifetimeTicks(entity);
        }

        public static void TickCorpseDecay(SimulationWorld world)
        {
            if (world?.Entities == null)
                return;

            var now = world.Tick.Value;
            foreach (var entity in world.Entities.All)
            {
                if (entity == null ||
                    !entity.TryGet<LifecycleComponent>(out var life) ||
                    !life.IsDead ||
                    !entity.TryGet<CorpseComponent>(out var corpse))
                    continue;

                if (corpse.RemoveAfterTick > now)
                    continue;

                life.State = LifecycleState.Removed;
            }
        }
    }
}
