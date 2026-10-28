using XianXia.Core.Actions;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Labor;
using XianXia.Core.Orders;
using XianXia.Core.Schedule;
using XianXia.Core.Simulation;
using System;
using XianXia.Core.Combat;
using XianXia.Core.Construction;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Npc;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Social
{
    /// <summary>
    /// ADR-0065: exact-range scheduled observation, persistent anger and physical pursuit.
    /// </summary>
    public sealed class SupervisorAngerDriver
    {
        public const int ObservationRangeCells = 50;
        public const int PrisonTrespassBufferCells = 3;
        public const string SupervisionIntent = "huangcun:supervision";
        const string SupervisionModeKey = "base:huangcun_discipline_supervision";

        public static bool IsSupervising(SimulationWorld world) =>
            world?.HuangcunSurvival?.Enabled == true && !world.HuangcunSurvival.PunishmentBattleActive &&
            world.HuangcunSurvival.PunisherId != 0 && world.HuangcunSurvival.PunishmentTargetId != 0 &&
            world.ContentCounters.Get(SupervisionModeKey) == 1;

        public static string DescribeSupervision(SimulationWorld world)
        {
            if (!IsSupervising(world) || !HuangcunOpeningAccess.IsLocked(world) || world.Strategic.CharacterEncounter != null ||
                !world.Entities.TryGet(new EntityId(world.HuangcunSurvival.PunisherId), out var observer) ||
                !world.Entities.TryGet(new EntityId(world.HuangcunSurvival.PunishmentTargetId), out var subject)) return null;
            if (!IsViolating(world, subject)) return subject.DisplayName + "已开始履行日程，督工即将结束。";
            var activity = RequiredActivity(world, subject) == ScheduleActivity.Rest ? "休息" : "劳动";
            return subject.DisplayName + "被" + observer.DisplayName + "发现没有按时" + activity +
                "；现在应当" + activity + "。" + observer.DisplayName + "正在跟随督工，实际开始后解除。";
        }

        public static string DescribeSupervisingObserver(SimulationWorld world, EntityId id) =>
            IsSupervising(world) && world.Strategic.CharacterEncounter == null &&
            world.HuangcunSurvival.PunisherId == id.Value &&
            world.Entities.TryGet(new EntityId(world.HuangcunSurvival.PunishmentTargetId), out var target)
                ? "督工：跟随" + target.DisplayName : null;
        public static bool IsOnWatch(SimulationWorld world, Entity candidate) =>
            world != null && candidate != null && HuangcunOpeningAccess.IsOpeningStaff(world, candidate.Id) &&
            CanObserveOutdoors(world, candidate);

        /// <summary>Awake outdoor awareness is independent of the current patrol timetable.</summary>
        public static bool CanObserveOutdoors(SimulationWorld world, Entity candidate)
        {
            if (world == null || candidate == null || world.LocalMap.IsInInterior ||
                !CombatLifeStateService.CanFight(candidate) || PartyRestService.IsResting(world, candidate) ||
                MortalTransportConstraintService.ValidateAttack(world, candidate.Id).IsFailure ||
                CharacterEncounterService.OwnsParticipantSpatialState(world, candidate.Id) ||
                SeparateSpaceTransitionService.IsOwnedByActiveSeparateSpace(world, candidate.Id) ||
                !CharacterWorldPresenceQuery.TryResolve(world, candidate.Id, out var at) || !at.HasWorldPosition) return false;
            if (world.Civilians.TryGet(candidate.Id, out var state) &&
                (state.SleepPhase == CivilianSleepPhase.SleepingAtResidence || state.SleepPhase == CivilianSleepPhase.SleepingOnGround ||
                 state.Disposition != CivilianDisposition.Normal || state.Activity == MortalActivity.Rescue || !state.RescueTargetId.IsNone)) return false;
            if (candidate.TryGet<ActionStateComponent>(out var actionState) && actionState.HasActiveAction &&
                world.ActiveActions.TryGetValue(actionState.ActiveActionId, out var action) &&
                (action is CultivateAction || action is WorkAction work && work.Activity == ScheduleActivity.Cultivate)) return false;
            var epsilon = world.SurfaceSpatial.TryGet(at.SurfaceId, out var metric) ? metric.CellSize * .01f : .00001f;
            foreach (var room in world.OutdoorConstructedAssets.QueryOverlapping(at.SurfaceId,
                at.WorldPosition.X - epsilon, at.WorldPosition.Y - epsilon, epsilon * 2, epsilon * 2))
                if (RoomBedGeometry.IsRoom(room) && at.WorldPosition.X > room.WorldX &&
                    at.WorldPosition.X < room.WorldX + room.WorldWidth && at.WorldPosition.Y > room.WorldY &&
                    at.WorldPosition.Y < room.WorldY + room.WorldHeight) return false;
            return true;
        }
        public static bool OwnsPursuit(SimulationWorld world, EntityId id) =>
            PrivateCampRaidService.Owns(world,id) || world?.HuangcunSurvival?.Enabled == true && !world.HuangcunSurvival.PunishmentBattleActive &&
            world.HuangcunSurvival.PunisherId == id.Value && world.HuangcunSurvival.PunishmentTargetId != 0 && !id.IsNone;

        public void Tick(SimulationWorld world, SimulationLoop loop = null)
        {
            if (world == null || !world.HuangcunSurvival.Enabled) return;
            var s = world.HuangcunSurvival;
            if (!HuangcunOpeningAccess.IsLocked(world)) { FinishPunishment(world); return; }
            if (s.LastObservationTick == world.Tick.Value) return;
            s.LastObservationTick = world.Tick.Value;
            world.SupervisorAnger.Value = s.SupervisorAnger;
            s.SupervisorAnger = world.SupervisorAnger.Value;
            if (s.PunishmentBattleActive || world.Strategic.CharacterEncounter != null) return;
            if (s.PunisherId != 0 && !IsSupervising(world))
            {
                FindObservedViolation(world,out var pursuingObserver,out _);
                ChangePressure(world,pursuingObserver==null?-1:1);
                // Also handles a v20 save made before pursuit preempted schedule actions.
                if (loop != null && world.Entities.TryGet(new EntityId(s.PunisherId), out var pursuing) &&
                    pursuing.TryGet<ActionStateComponent>(out var pendingAction) && pendingAction.HasActiveAction)
                    loop.StopSubject(pursuing.Id);
                Pursue(world); return;
            }
            if (PrivateCampRaidService.Observe(world,loop,out var secretObserver,out var secretSubject))
            { BeginPursuit(world,loop,secretObserver,secretSubject.Id,"秘密修炼或私人营地被发现，巡卫立即追赶开战！"); return; }
            PrivateCampRaidService.AdvanceOutdoor(world,loop);
            foreach (var member in world.Entities.All)
                if (TryBeginPrisonTrespass(world, loop, member.Id)) return;
            FindObservedViolation(world,out var observer,out var subject);
            ChangePressure(world,observer==null?-1:1);
            if (IsSupervising(world))
            {
                var guardId = new EntityId(s.PunisherId);
                var targetId = new EntityId(s.PunishmentTargetId);
                world.Entities.TryGet(guardId, out var guard);
                world.Entities.TryGet(targetId, out var target);
                var valid = guard != null && target != null &&
                    HuangcunOpeningAccess.IsOpeningStaff(world, guardId) &&
                    world.Strategic.PlayerPartyContext?.IsMember(guardId) != true &&
                    world.Strategic.PlayerPartyContext?.IsMember(targetId) == true &&
                    HuangcunSurvivalService.Applies(world, target) && !HuangcunSurvivalService.IsPrisoner(world, targetId) &&
                    CombatLifeStateService.CanFight(guard) && CombatLifeStateService.CanFight(target) &&
                    !PartyRestService.IsResting(world, guard) &&
                    MortalTransportConstraintService.ValidateAttack(world, guardId).IsSuccess &&
                    !SeparateSpaceTransitionService.IsOwnedByActiveSeparateSpace(world, guardId) &&
                    !world.BackgroundCharacterTravel.IsTraveling(guardId) &&
                    !world.GetOrCreateOrderQueue(guardId).HasSource(OrderSource.Player) &&
                    !world.GetOrCreateOrderQueue(guardId).HasSource(OrderSource.Event) &&
                    !(guard.TryGet<ActionStateComponent>(out var control) && control.HasActiveAction &&
                        control.ActiveOrderSource != OrderSource.Schedule) &&
                    (!world.Civilians.TryGet(guardId, out var custody) ||
                        custody.Disposition == CivilianDisposition.Normal && custody.SleepPhase == CivilianSleepPhase.None &&
                        custody.RescueTargetId.IsNone) &&
                    CharacterWorldPresenceQuery.TryResolve(world, guardId, out var guardAt) && guardAt.HasWorldPosition &&
                    CharacterWorldPresenceQuery.TryResolve(world, targetId, out var targetAt) && targetAt.HasWorldPosition &&
                    guardAt.SurfaceId == targetAt.SurfaceId;
                if (!valid) EndSupervision(world, "督工已结束：人物或所在空间发生变化。");
                else if (!IsViolating(world, target))
                    EndSupervision(world, target.DisplayName + "已履行当前日程，巡视者停止督工并返回原安排。");
                else if (observer == null || s.SupervisorAnger < SupervisorAngerBoard.MaxValue)
                {
                    FollowForSupervision(world, guard, target);
                    return;
                }
            }
            if (observer == null) return;
            if (s.SupervisorAnger < SupervisorAngerBoard.MaxValue)
            {
                BeginSupervision(world, loop, observer, subject);
                return;
            }
            BeginPursuit(world, loop, observer, subject.Id, "违规被巡查者发现，主管怒气已满，正前来惩戒。");
        }

        static void ChangePressure(SimulationWorld world,int delta)
        {
            world.HuangcunSurvival.ObservationSeconds=0;
            world.SupervisorAnger.Add(delta);
            world.HuangcunSurvival.SupervisorAnger=world.SupervisorAnger.Value;
        }

        static void FindObservedViolation(SimulationWorld world,out Entity observer,out Entity subject)
        {
            observer = null; subject = null; var best = double.MaxValue;
            foreach (var member in world.Entities.All)
            {
                if (world.Strategic.PlayerPartyContext?.IsMember(member.Id) != true ||
                    !HuangcunSurvivalService.Applies(world, member) || HuangcunSurvivalService.IsPrisoner(world,member.Id) ||
                    !CombatLifeStateService.CanFight(member) || !IsViolating(world,member) ||
                    !CharacterWorldPresenceQuery.TryResolve(world,member.Id,out var at) || !at.HasWorldPosition) continue;
                foreach (var candidate in world.Entities.All)
                {
                    if (!IsOnWatch(world,candidate) ||
                        !CharacterWorldPresenceQuery.TryResolve(world,candidate.Id,out var there) || !there.HasWorldPosition ||
                        there.SurfaceId != at.SurfaceId) continue;
                    var distance = WorldVec2.Distance(at.WorldPosition,there.WorldPosition);
                    if(distance > world.OpeningArea.CellSize*ObservationRangeCells || distance > best ||
                        distance == best && subject != null && (member.Id.Value > subject.Id.Value ||
                        member.Id == subject.Id && candidate.Id.Value >= observer.Id.Value) ||
                        !WorldObservation.HasLineOfSight(world,at.SurfaceId,there.WorldPosition,at.WorldPosition)) continue;
                    observer=candidate; subject=member; best=distance;
                }
            }
        }

        public static bool TryFindPrisonObserver(SimulationWorld world, EntityId id, out Entity observer)
        {
            observer = null;
            if (world?.HuangcunSurvival.Enabled != true || !HuangcunOpeningAccess.IsLocked(world) ||
                world.LocalMap.IsInInterior || world.Strategic.CharacterEncounter != null ||
                world.Strategic.PlayerPartyContext?.IsMember(id) != true ||
                !world.Entities.TryGet(id, out var intruder) || !HuangcunSurvivalService.Applies(world, intruder) ||
                HuangcunSurvivalService.IsPrisoner(world, id) || !CombatLifeStateService.CanFight(intruder) ||
                !world.OutdoorConstructedAssets.Assets.TryGetValue(world.HuangcunSurvival.PrisonRoomId, out var room) ||
                room.RoomUsage != XianXia.Core.Construction.RoomUsage.Prison ||
                !CharacterWorldPresenceQuery.TryResolve(world, id, out var at) || !at.HasWorldPosition ||
                at.SurfaceId != room.SurfaceId) return false;
            var dx = Math.Max(room.WorldX - at.WorldPosition.X, Math.Max(0, at.WorldPosition.X - room.WorldX - room.WorldWidth));
            var dy = Math.Max(room.WorldY - at.WorldPosition.Y, Math.Max(0, at.WorldPosition.Y - room.WorldY - room.WorldHeight));
            var buffer = world.OpeningArea.CellSize * PrisonTrespassBufferCells;
            if (dx * dx + dy * dy > buffer * buffer) return false;
            var best = double.MaxValue;
            foreach (var candidate in world.Entities.All)
            {
                if (!IsOnWatch(world, candidate) || CharacterEncounterService.OwnsParticipantSpatialState(world, candidate.Id) ||
                    !CharacterWorldPresenceQuery.TryResolve(world, candidate.Id, out var there) || !there.HasWorldPosition ||
                    there.SurfaceId != at.SurfaceId) continue;
                var distance = WorldVec2.Distance(at.WorldPosition, there.WorldPosition);
                if (distance > world.OpeningArea.CellSize * ObservationRangeCells || distance > best ||
                    distance == best && observer != null && candidate.Id.Value > observer.Id.Value ||
                    !WorldObservation.HasLineOfSight(world, at.SurfaceId, there.WorldPosition, at.WorldPosition)) continue;
                observer = candidate; best = distance;
            }
            return observer != null;
        }

        public static bool TryBeginPrisonTrespass(SimulationWorld world, SimulationLoop loop, EntityId id)
        {
            if (world?.HuangcunSurvival.Enabled != true || !HuangcunOpeningAccess.IsLocked(world)) return false;
            PrivateCampRaidService.CancelDefeated(world);
            var s = world.HuangcunSurvival;
            if (s.PunisherId != 0 && !IsSupervising(world)) return s.PunishmentTargetId == id.Value;
            if (!TryFindPrisonObserver(world, id, out var observer)) return false;
            BeginPursuit(world, loop, observer, id, "靠近牢房禁区被" + observer.DisplayName + "发现，正在追赶你！");
            return true;
        }

        static void BeginPursuit(SimulationWorld world, SimulationLoop loop, Entity observer, EntityId id, string message)
        {
            if (IsSupervising(world)) EndSupervision(world, null);
            world.ContentCounters.Set(SupervisionModeKey, 0);
            var s = world.HuangcunSurvival;
            s.ObservationSeconds = 0;
            s.PunisherId = observer.Id.Value; s.PunishmentTargetId = id.Value;
            loop?.StopSubject(observer.Id);
            world.WorkAreaOccupancy.Release(observer.Id);
            world.Strategic.SuppressedCharacterContacts.Remove(CharacterEncounterService.ContactKey(observer.Id, id));
            s.Message = message;
            Pursue(world);
        }

        static void BeginSupervision(SimulationWorld world, SimulationLoop loop, Entity observer, Entity subject)
        {
            var s = world.HuangcunSurvival;
            s.PunisherId = observer.Id.Value;
            s.PunishmentTargetId = subject.Id.Value;
            world.ContentCounters.Set(SupervisionModeKey, 1);
            loop?.StopSubject(observer.Id);
            world.WorkAreaOccupancy.Release(observer.Id);
            s.Message = DescribeSupervision(world);
            FollowForSupervision(world, observer, subject);
        }

        static void EndSupervision(SimulationWorld world, string message)
        {
            var s = world.HuangcunSurvival;
            if (world.Entities.TryGet(new EntityId(s.PunisherId), out var actor) &&
                actor.TryGet<MovementIntentComponent>(out var movement) && movement.DestinationKey == SupervisionIntent)
                movement.Clear();
            s.PunisherId = s.PunishmentTargetId = 0;
            world.ContentCounters.Set(SupervisionModeKey, 0);
            if (!string.IsNullOrEmpty(message)) s.Message = message;
        }

        static void FollowForSupervision(SimulationWorld world, Entity observer, Entity subject)
        {
            if (!CharacterWorldPresenceQuery.TryResolve(world, observer.Id, out var from) || !from.HasWorldPosition ||
                !CharacterWorldPresenceQuery.TryResolve(world, subject.Id, out var to) || !to.HasWorldPosition ||
                from.SurfaceId != to.SurfaceId) return;
            var radius = world.OpeningArea.CellSize * 3f;
            var visible = WorldObservation.HasLineOfSight(world, from.SurfaceId, from.WorldPosition, to.WorldPosition);
            if (WorldVec2.Distance(from.WorldPosition, to.WorldPosition) <= radius &&
                visible)
            {
                if (observer.TryGet<MovementIntentComponent>(out var arrived) && arrived.DestinationKey == SupervisionIntent)
                    arrived.Clear();
                return;
            }
            if (!world.ContinuousOutdoorMaterialization.IsMaterialized(observer.Id))
            {
                NpcColdMovement.Advance(world, observer, to.SurfaceId, to.WorldPosition,
                    visible ? radius : world.OpeningArea.CellSize * .5f, out _);
                return;
            }
            if (!observer.TryGet<MovementIntentComponent>(out var intent))
            { intent = new MovementIntentComponent(); observer.AddComponent(intent); }
            if (!intent.Active || intent.HostArrived || intent.DestinationKey != SupervisionIntent ||
                WorldVec2.Distance(new WorldVec2(intent.TargetWorldX, intent.TargetWorldY), to.WorldPosition) > radius * .5f)
                intent.BeginWorldPoint(to.SurfaceId, to.WorldPosition.X, to.WorldPosition.Y,
                    destinationKey: SupervisionIntent, arrivalRadiusWorld: radius);
        }

        static ScheduleActivity RequiredActivity(SimulationWorld world, Entity subject)
        {
            return subject.TryGet<ScheduleComponent>(out var schedule) &&
                world.TryGetSchedule(schedule.DefinitionId, out var definition) && definition.TryResolve(world.Tick, out var block)
                ? block.Activity : ScheduleActivity.Idle;
        }

        static bool IsViolating(SimulationWorld world, Entity subject)
        {
            if (!subject.TryGet<ScheduleComponent>(out var schedule) || !world.TryGetSchedule(schedule.DefinitionId, out var definition) ||
                !definition.TryResolve(world.Tick, out var block)) return false;
            if (block.Activity != ScheduleActivity.Rest && block.Activity != ScheduleActivity.Labor) return false;
            if (PartyRestService.IsResting(world, subject)) return block.Activity != ScheduleActivity.Rest;
            if (subject.TryGet<ObservedLaborComponent>(out var farm) && farm.ValidUntilTick >= world.Tick.Value && block.Activity == ScheduleActivity.Labor)
                return false;
            if (subject.TryGet<ActionStateComponent>(out var st) && st.HasActiveAction && world.ActiveActions.TryGetValue(st.ActiveActionId, out var action))
            {
                if (block.Activity == ScheduleActivity.Rest && (action is RestAction || action is BedRestAction)) return false;
                if (action is WorkAction work && work.Activity == block.Activity) return false;
                if (block.Activity == ScheduleActivity.Labor && (action is LaborAction ||
                    action is HuangcunSurvivalAction shortWork && shortWork.Task == "work")) return false;
            }
            return true;
        }

        static void Pursue(SimulationWorld world)
        {
            var s = world.HuangcunSurvival; var actorId = new EntityId(s.PunisherId); var targetId = new EntityId(s.PunishmentTargetId);
            if (!world.Entities.TryGet(actorId, out var actor) || !world.Entities.TryGet(targetId, out var target) ||
                !CombatLifeStateService.CanFight(actor) || !CombatLifeStateService.CanFight(target) ||
                !CharacterWorldPresenceQuery.TryResolve(world, actorId, out var from) || !from.HasWorldPosition ||
                !CharacterWorldPresenceQuery.TryResolve(world, targetId, out var to) || !to.HasWorldPosition || from.SurfaceId != to.SurfaceId)
            { FinishPunishment(world); return; }
            var radius = world.OpeningArea.CellSize*2;
            if (WorldVec2.Distance(from.WorldPosition, to.WorldPosition) <= radius)
            {
                if (actor.TryGet<MovementIntentComponent>(out var moving)) moving.Clear();
                world.Strategic.PendingCharacterAttacker = actorId; world.Strategic.PendingCharacterTarget = targetId;
                return;
            }
            if (!actor.TryGet<MovementIntentComponent>(out var intent)) { intent = new MovementIntentComponent(); actor.AddComponent(intent); }
            if (!intent.Active || intent.HostArrived || intent.DestinationKey != "huangcun:discipline" ||
                WorldVec2.Distance(new WorldVec2(intent.TargetWorldX,intent.TargetWorldY), to.WorldPosition) > radius)
                intent.BeginWorldPoint(to.SurfaceId,to.WorldPosition.X,to.WorldPosition.Y, destinationKey:"huangcun:discipline",arrivalRadiusWorld:radius);
        }

        public static bool IsPunishmentPair(SimulationWorld world, EntityId attacker, EntityId target) =>
            world?.HuangcunSurvival?.Enabled == true && !IsSupervising(world) && world.HuangcunSurvival.PunisherId == attacker.Value &&
            world.HuangcunSurvival.PunishmentTargetId == target.Value && !attacker.IsNone && !target.IsNone &&
            !IsDeclaredWarPair(world, attacker, target);
        static bool IsDeclaredWarPair(SimulationWorld world, EntityId attacker, EntityId target) =>
            world.Entities.TryGet(attacker, out var a) && world.Entities.TryGet(target, out var b) &&
            a.TryGet<FactionMembershipComponent>(out var af) && b.TryGet<FactionMembershipComponent>(out var bf) &&
            WarGateService.CanAttack(world, af.FactionId, bf.FactionId);
        public static bool IsNonlethalAttacker(SimulationWorld world, EntityId actor) =>
            world?.HuangcunSurvival?.PunishmentBattleActive == true && world.HuangcunSurvival.PunisherId == actor.Value;
        public static void FinishPunishment(SimulationWorld world)
        {
            var s = world.HuangcunSurvival;
            if (s.PunisherId != 0 && world.Entities.TryGet(new EntityId(s.PunisherId),out var actor) &&
                actor.TryGet<MovementIntentComponent>(out var intent) &&
                (intent.DestinationKey == "huangcun:discipline" || intent.DestinationKey == SupervisionIntent)) intent.Clear();
            world.ContentCounters.Set(SupervisionModeKey, 0);
            s.SupervisorAnger = s.ObservationSeconds = 0; s.PunisherId = s.PunishmentTargetId = 0; s.PunishmentBattleActive = false;
            world.SupervisorAnger.Value = 0;
        }
    }

    /// <summary>Transient input acknowledgement; reload must resume the real farm job to renew it.</summary>
    public sealed class ObservedLaborComponent : IComponent { public ulong ValidUntilTick; }
}
