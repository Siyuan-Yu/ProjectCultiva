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
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.Actions;
using XianXia.Core.Construction;
using XianXia.Core.Npc;

namespace XianXia.Core.Combat
{
    /// <summary>
    /// 战斗击败先昏迷，继续受击才弥留；普通受击不确认死亡。
    /// 显式处决／弥留超时未治 → 阵亡+尸体；
    /// 尸体超时腐烂 → Removed，并从大地图抹掉 WorldPresence。
    /// 暂定：弥留与尸体均不可移动／不可战斗（表现层可区分状态文案）。
    /// </summary>
    public static class CombatLifeStateService
    {
        public const int UnconsciousSeconds = 100;

        public static bool TryEnterUnconscious(SimulationWorld world, Entity entity, EntityId attacker)
        {
            var doses = entity != null && entity.TryGet<LifecycleComponent>(out var prior) ? prior.HerbDoses : 0;
            if (!TryEnterIncapacitated(world, entity, attacker)) return false;
            entity.TryGet<LifecycleComponent>(out var life);
            life.ResetMedicalProgress(); life.ClearBleedOut();
            life.HerbDoses = doses;
            life.UnconsciousSecondsRemaining = UnconsciousSeconds;
            if (entity.TryGet<CombatVitalsComponent>(out var vitals)) vitals.CurrentHp = 1;
            life.RecordTransition(LifecycleState.Alive, LifecycleState.Incapacitated, "Unconscious");
            return true;
        }

        public static bool TryEnterDyingFromUnconscious(SimulationWorld world, Entity entity, EntityId attacker)
        {
            if (world == null || entity == null || !entity.TryGet<LifecycleComponent>(out var life) ||
                !life.IsUnconscious && !(life.IsIncapacitated && life.Convalescing))
                return false;
            var doses = life.HerbDoses;
            life.ResetMedicalProgress(); life.HasMedicalProgress = true;
            life.HerbDoses = doses;
            life.DyingReserveMilli = BleedOutRealSeconds * 1000;
            life.BleedOutAfterTick = world.Tick.Value + BleedOutDurationTicks;
            if (entity.TryGet<CombatVitalsComponent>(out var vitals)) vitals.CurrentHp = 0;
            if (!attacker.IsNone)
            {
                if (!entity.TryGet<CombatDeathAttributionComponent>(out var attribution))
                { attribution = new CombatDeathAttributionComponent(); entity.AddComponent(attribution); }
                attribution.Set(attacker);
            }
            life.RecordTransition(LifecycleState.Incapacitated, LifecycleState.Incapacitated, "UnconsciousToDying");
            return true;
        }

        public static void AdvanceUnconscious(SimulationWorld world, Entity entity, float seconds)
        {
            if (seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds) ||
                !entity.TryGet<LifecycleComponent>(out var life) || !life.IsUnconscious) return;
            if (life.UnconsciousSecondsRemaining > seconds)
            { life.UnconsciousSecondsRemaining -= seconds; return; }
            var hp = entity.TryGet<CombatVitalsComponent>(out var vitals) ? Math.Max(1, vitals.CurrentHp) : 1;
            var doses = life.HerbDoses; var fraction = life.RecoveryHpMilli;
            if (TryRecoverFromIncapacitated(world, entity, hp))
            { life.HerbDoses = doses; life.RecoveryHpMilli = fraction; life.HasMedicalProgress = doses > 0 || fraction > 0; }
        }
        /// <summary>弥留未治疗 → 阵亡（现实秒；1x 下 1 tick≈1 秒）。</summary>
        public const int BleedOutRealSeconds = 200;

        /// <summary>尸体留存 → 腐烂消失（现实秒）。</summary>
        public const int CorpseLifetimeRealSeconds = 200;

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
            // 昏迷可受击转弥留；弥留受普通攻击不确认死亡。
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
                return life.IsUnconscious ? "昏迷" : life.Convalescing ? "恢复中·昏迷" : "弥留";
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
            if (life.IsUnconscious)
            { label = "昏迷"; remainingSeconds = (int)Math.Ceiling(life.UnconsciousSecondsRemaining); return true; }
            if (life.IsDying && life.BleedOutAfterTick > 0)
            {
                label = "弥留";
                var rate = MedicalRecoveryService.ResolveRateMilli(world, entity);
                if (life.HasMedicalProgress && rate >= 0) return false;
                remainingSeconds = life.HasMedicalProgress ? (life.DyingReserveMilli - rate - 1) / -rate
                    : (int)Math.Max(0L, (long)life.BleedOutAfterTick - (long)now);
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
            if (entity != null && entity.TryGet<LifecycleComponent>(out var progress) && progress.IsDying && progress.HasMedicalProgress)
                return "弥留 " + (progress.DyingReserveMilli / 1000f).ToString("0.0") + "/200 · " +
                    (MedicalRecoveryService.ResolveRateMilli(world, entity) / 1000f).ToString("+0.0;-0.0;0.0") + "/s";
            if (TryGetLifeStateCountdown(world, entity, out var label, out var sec))
                return label + " " + sec + "s";
            if (entity != null && entity.TryGet<LifecycleComponent>(out var medicalLife) && medicalLife.IsIncapacitated)
                return ResolveLifeStateLabel(entity) + " " +
                    (MedicalRecoveryService.ResolveRateMilli(world, entity) / 1000f).ToString("+0.0;-0.0;0.0") + "/s";
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

        public static bool TryEnterIncapacitated(SimulationWorld world, Entity entity) =>
            TryEnterIncapacitated(world, entity, EntityId.None);

        public static bool TryEnterIncapacitated(
            SimulationWorld world,
            Entity entity,
            EntityId responsibleAttackerId)
        {
            if (world == null || entity == null || !entity.TryGet<LifecycleComponent>(out var life))
                return false;
            if (life.State != LifecycleState.Alive)
                return false;

            CaptureSquadWorldMotionHandoff(world, entity.Id);

            ReleaseRecoveryBed(world, entity.Id);

            CombatDamageRules.EnsureVitals(entity);
            if (entity.TryGet<CombatVitalsComponent>(out var vitals))
                vitals.CurrentHp = 0;

            var previous = life.State;
            life.State = LifecycleState.Incapacitated;
            life.ResetMedicalProgress();
            life.HasMedicalProgress = true;
            life.DyingReserveMilli = BleedOutRealSeconds * 1000;
            life.BleedOutAfterTick = world.Tick.Value + BleedOutDurationTicks;
            life.IncapacitatedAtTick = world.Tick.Value;
            life.LastDeathConfirmationReason = DeathConfirmationReason.Unknown;
            life.RecordTransition(previous, LifecycleState.Incapacitated, "Damage");
            AutonomousActionContinuationService.CancelInvalid(world, entity.Id);
            if (entity.TryGet<MovementIntentComponent>(out var interruptedMove)) interruptedMove.Clear();
            if (!responsibleAttackerId.IsNone)
            {
                if (!entity.TryGet<CombatDeathAttributionComponent>(out var attribution))
                {
                    attribution = new CombatDeathAttributionComponent();
                    entity.AddComponent(attribution);
                }
                attribution.Set(responsibleAttackerId);
            }
            PlayerPartyLifeStateMembershipService.ReconcilePlayerPartyAfterLifeStateChange(world);
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

            CaptureSquadWorldMotionHandoff(world, entity.Id);

            ReleaseRecoveryBed(world, entity.Id);
            life.State = LifecycleState.Captured;
            // Capture changes custody, never heals the wound or cancels dying progression.
            if (!life.HasMedicalProgress) life.ClearBleedOut();
            ClearDeathAttribution(entity);
            return true;
        }

        /// <summary>治疗／救回：清弥留计时并回到 Alive（后续正式治疗入口可复用）。</summary>
        public static bool TryRecoverFromIncapacitated(
            SimulationWorld world,
            Entity entity,
            int restoreHp = 1) =>
            TryRecoverFromIncapacitated(world, EntityId.None, entity, restoreHp);

        public static bool TryRecoverFromIncapacitated(
            SimulationWorld world,
            EntityId rescuerId,
            Entity entity,
            int restoreHp = 1)
        {
            if (world == null || entity == null || !entity.TryGet<LifecycleComponent>(out var life))
                return false;
            if (!life.IsIncapacitated)
                return false;

            if (!entity.TryGet<AttributesComponent>(out var attributes) ||
                !life.IsUnconscious && (long)restoreHp * 2 <= Math.Max(1, attributes.GetFinal(XianXia.Core.Attributes.AttributeId.MaxHp)))
                return false;

            var recoveredState = life.State == LifecycleState.Captured ? LifecycleState.Captured : LifecycleState.Alive;
            // Carried patients were placed at the bed centre, which is a furniture blocker.
            // Stand up at the bed's legal access point before releasing its physical occupancy.
            if (recoveredState == LifecycleState.Alive &&
                MedicalRecoveryService.TryGetPatientBed(world, entity, out var recoveryBed) &&
                RoomBedFacilityService.TryResolveAccessPoint(world, recoveryBed, out var standingPoint) &&
                (!world.SurfaceGround.TryGet(recoveryBed.SurfaceId, out var ground) ||
                 ground.IsWalkable(standingPoint.X, standingPoint.Y)))
                world.WorldPresence.SetAtWorldPosition(entity.Id, standingPoint, recoveryBed.SurfaceId);
            ReleaseRecoveryBed(world, entity.Id);
            life.State = recoveredState;
            life.ClearBleedOut();
            life.ResetMedicalProgress();
            life.RecordTransition(LifecycleState.Incapacitated, recoveredState, "Recovery");
            CombatDamageRules.EnsureVitals(entity);
            if (entity.TryGet<CombatVitalsComponent>(out var vitals))
                vitals.CurrentHp = Math.Min(Math.Max(1, restoreHp), Math.Max(1,
                    attributes.GetFinal(XianXia.Core.Attributes.AttributeId.MaxHp)));
            ClearDeathAttribution(entity);
            MortalCivilianService.ResumeAfterMedicalRecovery(world, entity);
            if (!rescuerId.IsNone && rescuerId != entity.Id)
                new SocialEventService().RecordCharacterRescued(world, rescuerId, entity.Id);
            return true;
        }

        public static bool TryConfirmDeath(
            SimulationWorld world,
            EntityId attackerId,
            Entity target,
            out bool confirmed)
            => TryConfirmDeath(world, attackerId, target, DeathConfirmationReason.Unknown, out confirmed);

        public static bool TryConfirmDeath(
            SimulationWorld world,
            EntityId attackerId,
            Entity target,
            DeathConfirmationReason reason,
            out bool confirmed)
        {
            confirmed = false;
            if (world == null || target == null || !target.TryGet<LifecycleComponent>(out var life))
                return false;
            if (life.IsDead || life.IsRemoved)
                return false;
            // First lethal hit is always Alive -> Incapacitated. Death confirmation is a
            // separate operation and can never collapse the same strike into Alive -> Dead.
            if (!life.IsIncapacitated || life.IsUnconscious)
                return false;

            CaptureSquadWorldMotionHandoff(world, target.Id);

            ReleaseRecoveryBed(world, target.Id);

            var responsibleAttackerId = attackerId;
            if (responsibleAttackerId.IsNone &&
                target.TryGet<CombatDeathAttributionComponent>(out var attribution))
                responsibleAttackerId = attribution.ResponsibleAttackerId;

            life.State = LifecycleState.Dead;
            if (world.Civilians.TryGet(target.Id, out var medicState))
                MortalCivilianService.SettleCarriedMedicine(world, target, medicState);
            life.ClearBleedOut();
            life.ResetMedicalProgress();
            life.LastDeathConfirmationReason = reason;
            life.RecordTransition(LifecycleState.Incapacitated, LifecycleState.Dead, reason.ToString());
            if (world.Civilians.TryGet(target.Id, out var custody) &&
                (custody.Disposition == CivilianDisposition.CapturedEscorted || custody.Disposition == CivilianDisposition.Detained))
            {
                custody.CarrierId = EntityId.None;
                custody.Disposition = CivilianDisposition.Normal;
                custody.DetainedResidenceWorkAreaId = string.Empty;
            }
            AutonomousActionContinuationService.CancelInvalid(world, target.Id);
            EnsureCorpse(world, target);

            if ((target.Tags & EntityTag.Npc) != 0 &&
                target.TryGet<EncounterLinkComponent>(out var link) &&
                !string.IsNullOrEmpty(link.EncounterId))
            {
                StoryFlagService.Set(
                    world,
                    ContentConditionEvaluator.EncounterFlag(link.EncounterId),
                    responsibleAttackerId);
            }

            confirmed = true;
            world.Events.Publish(
                EventType.CombatantDefeated,
                world.Tick,
                actor: responsibleAttackerId,
                target: target.Id,
                payload: "lethal");
            QuestProgressRefresh.AfterWorldChange(world, responsibleAttackerId);
            if (!responsibleAttackerId.IsNone)
                new SocialEventService().RecordCharacterKilled(world, responsibleAttackerId, target.Id);
            ClearDeathAttribution(target);
            PlayerPartyLifeStateMembershipService.ReconcilePlayerPartyAfterLifeStateChange(world);
            return true;
        }

        static void ClearDeathAttribution(Entity entity)
        {
            if (entity != null && entity.TryGet<CombatDeathAttributionComponent>(out var attribution))
                attribution.Clear();
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

            // Moving away, changing room usage or destroying the bed ends stabilization
            // immediately and releases stale reservation/occupancy before this tick decays.
            MortalCivilianService.ValidateRecoverySpotPlacements(world);

            var now = world.Tick.Value;
            // 先收集再改，避免枚举中途改集合
            var rotAway = new System.Collections.Generic.List<Entity>(8);

            foreach (var entity in world.Entities.All)
            {
                if (entity == null || !entity.TryGet<LifecycleComponent>(out var life))
                    continue;
                if (!World.Strategic.HuangcunOpeningAccess.IsSimulated(world, entity)) continue;

                AdvanceUnconscious(world, entity, 1f);
                MedicalRecoveryService.AdvanceSecond(world, entity);

                if (life.IsDead &&
                    entity.TryGet<CorpseComponent>(out var corpse) &&
                    corpse.RemoveAfterTick <= now)
                {
                    rotAway.Add(entity);
                }
            }

            for (var i = 0; i < rotAway.Count; i++)
                FinalizeRemoval(world, rotAway[i]);
        }

        /// <summary>尸体腐烂：标记 Removed，从宏观图抹掉，之后不再演算该角色位置。</summary>
        public static void TickEncounterLifeDecay(SimulationWorld world,
            System.Collections.Generic.IReadOnlyList<XianXia.Core.World.Strategic.EncounterCharacter> participants)
        {
            foreach (var p in participants)
            {
                if (!world.Entities.TryGet(new EntityId(p.CharacterId), out var entity) ||
                    !entity.TryGet<LifecycleComponent>(out var life)) continue;
                MedicalRecoveryService.AdvanceSecond(world, entity);
                if (life.IsDead && entity.TryGet<CorpseComponent>(out var corpse))
                {
                    if (corpse.RemoveAfterTick <= world.Tick.Value + 1) FinalizeRemoval(world, entity);
                    else corpse.RemoveAfterTick--;
                }
            }
        }

        public static void FinalizeRemoval(SimulationWorld world, Entity entity)
        {
            if (world == null || entity == null)
                return;
            if (!entity.TryGet<LifecycleComponent>(out var life))
                return;
            if (life.IsRemoved)
                return;
#if DEBUG || UNITY_EDITOR || DEVELOPMENT_BUILD
            var removalSpatialBefore = FormatRemovalSpatial(world, entity.Id);
#endif
            life.State = LifecycleState.Removed;
            life.ResetMedicalProgress();
            ReleaseRecoveryBed(world, entity.Id);
            life.ClearBleedOut();
            AutonomousActionContinuationService.CancelInvalid(world, entity.Id);
            PlayerPartyLifeStateMembershipService.ReconcilePlayerPartyAfterLifeStateChange(world);

            // 大地图
            world.WorldPresence?.Remove(entity.Id);
            // LocalMap 占位／表现坐标
            if (entity.TryGet<EntityLocationComponent>(out var loc) && loc != null)
                loc.ClearPresence();
            // 遭遇刷怪追踪／敌军栈人数（无存活／弥留／可见尸体时从大地图抹栈）
#if DEBUG || UNITY_EDITOR || DEVELOPMENT_BUILD
            var squadId = world.Strategic?.Squads != null &&
                          world.Strategic.Squads.TryGetForCharacter(entity.Id, out var squad) && squad != null
                ? squad.SquadId
                : string.Empty;
            var groupId = world.Strategic?.Squads != null &&
                          world.Strategic.Squads.TryGetForCharacter(entity.Id, out var currentSquad) && currentSquad != null
                ? currentSquad.SquadId : string.Empty;
            System.Diagnostics.Debug.WriteLine(
                "[ResidualLifecycleSpatial] CharacterId=" + entity.Id.Value +
                " Name=" + (entity.DisplayName ?? string.Empty) +
                " Transition=DeadToRemoved" +
                " SquadId=" + squadId +
                " GroupId=" + groupId +
                " EncounterId=" + (world.Strategic?.CharacterEncounter?.EncounterId ?? string.Empty) +
                " Before=" + removalSpatialBefore +
                " After=Mode:Missing,SiteId:,PersonalSurfaceId:,HasPrecise:false,WorldPosition:None" +
                " ViewPosition=None" +
                " SpatialOwner=Removed" +
                " HandoffAction=Remove");
#endif
        }

        public static bool IsMedicallyStabilized(
            SimulationWorld world,
            Entity entity,
            out OutdoorConstructedAssetState bed)
        {
            bed = null;
            if (world == null || entity == null ||
                !entity.TryGet<LifecycleComponent>(out var life) || !life.IsIncapacitated)
                return false;
            return MedicalRecoveryService.TryGetPatientBed(world, entity, out bed) &&
                MedicalRecoveryService.ResolveRateMilli(world, entity) >= 0;
        }

        static void ReleaseRecoveryBed(SimulationWorld world, EntityId characterId)
        {
            if (world == null || characterId.IsNone) return;
            var spots = new System.Collections.Generic.List<string>();
            foreach (var pair in world.RecoverySpotDeliveries.Placements)
                if (pair.Value == characterId) spots.Add(pair.Key);
            for (var i = 0; i < spots.Count; i++)
                world.RecoverySpotDeliveries.RemoveSpot(spots[i]);
            RoomBedFacilityService.Release(world, characterId);
        }

        static void CaptureSquadWorldMotionHandoff(SimulationWorld world, EntityId id)
        {
            // Active/ReadyToEnd CharacterEncounter is the sole spatial owner. Lifecycle changes
            // remain in-place on its Tactical position until CommitAndReturn restores Return.
            if (CharacterEncounterService.OwnsParticipantSpatialState(world, id))
                return;
            if (SeparateSpaceTransitionService.IsOwnedByActiveSeparateSpace(world, id))
                return;
            if (world?.Strategic?.Squads == null ||
                !SquadWorldMotionService.OwnsCharacter(world, id) ||
                !world.Strategic.Squads.TryGetForCharacter(id, out var squad) ||
                squad.CommandKind != SquadCommandKind.SquadWorldMotion ||
                !world.Strategic.SquadWorldMotions.TryGet(squad.SquadId, out var motion) ||
                !motion.HasPosition ||
                !ContinuousCharacterSpatialAuthorityResolver.TryResolveWorldPosition(
                    world, id, motion.SurfaceId, out var point, out var owner, out _, out _) ||
                owner != ContinuousSpatialOwnerKind.Squad)
                return;
            var presence = world.WorldPresence.GetOrCreate(id);
            presence.Mode = PartyWorldPresenceMode.AtWorldPosition;
            presence.SiteId = string.Empty;
            presence.PersonalSurfaceId = motion.SurfaceId;
            presence.WorldPosX = point.X;
            presence.WorldPosY = point.Y;
            presence.HasContinuousWorldPosition = true;
        }

#if DEBUG || UNITY_EDITOR || DEVELOPMENT_BUILD
        static string FormatRemovalSpatial(SimulationWorld world, EntityId id)
        {
            if (world?.WorldPresence == null ||
                !world.WorldPresence.TryGet(id, out var presence) || presence == null)
                return "Mode:Missing,SiteId:,PersonalSurfaceId:,HasPrecise:false,WorldPosition:None";
            var worldPosition = presence.HasContinuousWorldPosition
                ? "(" + presence.WorldPosX.ToString("0.###") + "," + presence.WorldPosY.ToString("0.###") + ")"
                : "None";
            return "Mode:" + presence.Mode +
                   ",SiteId:" + (presence.SiteId ?? string.Empty) +
                   ",PersonalSurfaceId:" + (presence.PersonalSurfaceId ?? string.Empty) +
                   ",HasPrecise:" + presence.HasContinuousWorldPosition +
                   ",WorldPosition:" + worldPosition;
        }
#endif
    }
}
