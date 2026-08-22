using System;
using XianXia.Core.Content;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.World.Strategic;
using XianXia.Core.Events;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.Combat
{
    /// <summary>
    /// 0 血 → 弥留；弥留超时未治／再受主动攻击 → 阵亡+尸体；
    /// 尸体超时腐烂 → Removed，并从大地图抹掉 WorldPresence。
    /// 暂定：弥留与尸体均不可移动／不可战斗（表现层可区分状态文案）。
    /// </summary>
    public static class CombatLifeStateService
    {
        /// <summary>弥留未治疗 → 阵亡（现实秒；1x 下 1 tick≈1 秒）。</summary>
        public const int BleedOutRealSeconds = 50;

        /// <summary>尸体留存 → 腐烂消失（现实秒）。</summary>
        public const int CorpseLifetimeRealSeconds = 50;

        /// <summary>旧修为日数接口保留给内容表；运行时尸体寿命改走现实秒。</summary>
        public const int DefaultCorpseGameDays = 2;

        public static ulong BleedOutDurationTicks =>
            (ulong)Math.Max(1, BleedOutRealSeconds);

        public static ulong CorpseLifetimeTicks =>
            (ulong)Math.Max(1, CorpseLifetimeRealSeconds);

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
            // 弥留可被补刀；尸体／已移除不可再打
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
            if (life.State == LifecycleState.Captured)
                return "被俘";
            if (life.IsDead && entity.TryGet<CorpseComponent>(out _))
                return "尸体";
            if (life.IsDead)
                return "阵亡";
            return null;
        }

        /// <summary>
        /// 弥留→阵亡／尸体→腐烂剩余秒（1x 下 1 tick≈1 秒）。无倒计时返回 false。
        /// </summary>
        public static bool TryGetLifeStateCountdown(
            SimulationWorld world,
            Entity entity,
            out string label,
            out int remainingSeconds)
        {
            label = null;
            remainingSeconds = 0;
            if (world == null || entity == null ||
                !entity.TryGet<LifecycleComponent>(out var life))
                return false;

            var now = world.Tick.Value;
            if (life.IsIncapacitated && life.BleedOutAfterTick > 0)
            {
                label = "弥留";
                remainingSeconds = (int)Math.Max(0L, (long)life.BleedOutAfterTick - (long)now);
                return true;
            }

            if (life.IsDead &&
                entity.TryGet<CorpseComponent>(out var corpse) &&
                corpse != null &&
                corpse.RemoveAfterTick > 0)
            {
                label = "尸体";
                remainingSeconds = (int)Math.Max(0L, (long)corpse.RemoveAfterTick - (long)now);
                return true;
            }

            return false;
        }

        /// <summary>如「弥留 42s」「尸体 12s」；无倒计时则退回纯标签。</summary>
        public static string FormatLifeStateWithCountdown(SimulationWorld world, Entity entity)
        {
            if (TryGetLifeStateCountdown(world, entity, out var label, out var sec))
                return label + " " + sec + "s";
            return ResolveLifeStateLabel(entity);
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

        /// <summary>尸体腐烂 tick 数（暂按现实秒；不再乘游戏日）。</summary>
        public static ulong ResolveCorpseLifetimeTicks(Entity entity) =>
            CorpseLifetimeTicks;

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
            life.BleedOutAfterTick = world.Tick.Value + BleedOutDurationTicks;
            return true;
        }

        /// <summary>Phase J：战俘状态（不可战斗；概率公式 DEFER）。</summary>
        public static bool TryEnterCaptured(
            SimulationWorld world,
            Entity entity,
            string captorFactionId = null)
        {
            if (world == null || entity == null || !entity.TryGet<LifecycleComponent>(out var life))
                return false;
            if (life.State != LifecycleState.Alive && !life.IsIncapacitated)
                return false;

            life.State = LifecycleState.Captured;
            life.ClearBleedOut();
            return true;
        }

        /// <summary>治疗／救回：清弥留计时并回到 Alive（后续正式治疗入口可复用）。</summary>
        public static bool TryRecoverFromIncapacitated(
            SimulationWorld world,
            Entity entity,
            int restoreHp = 1)
        {
            if (world == null || entity == null || !entity.TryGet<LifecycleComponent>(out var life))
                return false;
            if (!life.IsIncapacitated)
                return false;

            life.State = LifecycleState.Alive;
            life.ClearBleedOut();
            CombatDamageRules.EnsureVitals(entity);
            if (entity.TryGet<CombatVitalsComponent>(out var vitals))
                vitals.CurrentHp = Math.Max(1, restoreHp);
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
            life.ClearBleedOut();
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

        /// <summary>每 tick：弥留超时→阵亡；尸体超时→腐烂移除并清大地图位置。</summary>
        public static void TickCorpseDecay(SimulationWorld world) =>
            TickLifeStateDecay(world);

        public static void TickLifeStateDecay(SimulationWorld world)
        {
            if (world?.Entities == null)
                return;

            var now = world.Tick.Value;
            // 先收集再改，避免枚举中途改集合
            var bleedOut = new System.Collections.Generic.List<Entity>(8);
            var rotAway = new System.Collections.Generic.List<Entity>(8);

            foreach (var entity in world.Entities.All)
            {
                if (entity == null || !entity.TryGet<LifecycleComponent>(out var life))
                    continue;

                if (life.IsIncapacitated &&
                    life.BleedOutAfterTick > 0 &&
                    life.BleedOutAfterTick <= now)
                {
                    bleedOut.Add(entity);
                    continue;
                }

                if (life.IsDead &&
                    entity.TryGet<CorpseComponent>(out var corpse) &&
                    corpse.RemoveAfterTick <= now)
                {
                    rotAway.Add(entity);
                }
            }

            for (var i = 0; i < bleedOut.Count; i++)
                TryConfirmDeath(world, EntityId.None, bleedOut[i], out _);

            for (var i = 0; i < rotAway.Count; i++)
                FinalizeRemoval(world, rotAway[i]);
        }

        /// <summary>尸体腐烂：标记 Removed，从宏观图抹掉，之后不再演算该角色位置。</summary>
        public static void FinalizeRemoval(SimulationWorld world, Entity entity)
        {
            if (world == null || entity == null)
                return;
            if (!entity.TryGet<LifecycleComponent>(out var life))
                return;
            if (!life.IsRemoved)
            {
                life.State = LifecycleState.Removed;
                life.ClearBleedOut();
            }

            // 大地图
            world.WorldPresence?.Remove(entity.Id);
            // LocalMap 占位／表现坐标
            if (entity.TryGet<EntityLocationComponent>(out var loc) && loc != null)
                loc.ClearPresence();
            // 遭遇刷怪追踪／敌军栈人数（无存活／弥留／可见尸体时从大地图抹栈）
            StrategicEncounterSpawner.ReconcileAfterLifeDecay(world, entity.Id);
        }
    }
}
