using XianXia.Core.Actions;
using XianXia.Core.Concealment;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Labor;
using XianXia.Core.Orders;
using XianXia.Core.Schedule;
using XianXia.Core.Simulation;
using System;
using XianXia.Core.Combat;
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
        public const int ObservationRangeCells = 20;
        public static bool IsOnWatch(SimulationWorld world, Entity candidate) =>
            HuangcunOpeningAccess.IsOpeningStaff(world, candidate.Id) && CombatLifeStateService.CanFight(candidate) &&
            MortalTransportConstraintService.ValidateAttack(world, candidate.Id).IsSuccess &&
            !PartyRestService.IsResting(world, candidate) &&
            (!world.Civilians.TryGet(candidate.Id, out var state) ||
                state.Activity != MortalActivity.SleepAtResidence && state.Activity != MortalActivity.SleepOnGround &&
                state.Activity != MortalActivity.Rescue && state.RescueTargetId.IsNone) &&
            candidate.TryGet<ScheduleComponent>(out var schedule) && world.TryGetSchedule(schedule.DefinitionId, out var definition) &&
            definition.TryResolve(world.Tick, out var block) &&
            (block.Activity == ScheduleActivity.Patrol || block.Activity == ScheduleActivity.Inspect);
        public static bool OwnsPursuit(SimulationWorld world, EntityId id) =>
            world?.HuangcunSurvival?.Enabled == true && !world.HuangcunSurvival.PunishmentBattleActive &&
            world.HuangcunSurvival.PunisherId == id.Value && world.HuangcunSurvival.PunishmentTargetId != 0 && !id.IsNone;

        public void Tick(SimulationWorld world, SimulationLoop loop = null)
        {
            if (world == null || !world.HuangcunSurvival.Enabled) return;
            var s = world.HuangcunSurvival;
            if (!HuangcunOpeningAccess.IsLocked(world)) { FinishPunishment(world); return; }
            if (s.LastObservationTick == world.Tick.Value) return;
            s.LastObservationTick = world.Tick.Value;
            world.SupervisorAnger.Value = s.SupervisorAnger;
            if (s.PunishmentBattleActive || world.Strategic.CharacterEncounter != null) return;
            if (s.PunisherId != 0)
            {
                // Also handles a v20 save made before pursuit preempted schedule actions.
                if (loop != null && world.Entities.TryGet(new EntityId(s.PunisherId), out var pursuing) &&
                    pursuing.TryGet<ActionStateComponent>(out var pendingAction) && pendingAction.HasActiveAction)
                    loop.StopSubject(pursuing.Id);
                Pursue(world); return;
            }
            var id = world.Strategic.PlayerPartyContext?.ActiveCharacterId ?? EntityId.None;
            if (!world.Entities.TryGet(id, out var subject) || !HuangcunSurvivalService.Applies(world, subject) ||
                HuangcunSurvivalService.IsPrisoner(world, id) || !CombatLifeStateService.CanFight(subject) ||
                !IsViolating(world, subject) || !CharacterWorldPresenceQuery.TryResolve(world, id, out var at) || !at.HasWorldPosition)
            { s.ObservationSeconds = 0; return; }
            Entity observer = null; var best = double.MaxValue;
            foreach (var candidate in world.Entities.All)
            {
                if (!IsOnWatch(world, candidate) ||
                    !CharacterWorldPresenceQuery.TryResolve(world, candidate.Id, out var there) || !there.HasWorldPosition ||
                    there.SurfaceId != at.SurfaceId) continue;
                var distance = WorldVec2.Distance(at.WorldPosition, there.WorldPosition);
                if (distance > world.OpeningArea.CellSize*ObservationRangeCells || distance > best ||
                    distance == best && observer != null && candidate.Id.Value > observer.Id.Value) continue;
                observer = candidate; best = distance;
            }
            if (observer == null) { s.ObservationSeconds = 0; return; }
            if (++s.ObservationSeconds < 5) return;
            s.ObservationSeconds = 0; world.SupervisorAnger.Add(1); s.SupervisorAnger = world.SupervisorAnger.Value;
            if (s.SupervisorAnger < 100) return;
            s.PunisherId = observer.Id.Value; s.PunishmentTargetId = id.Value;
            loop?.StopSubject(observer.Id);
            world.WorkAreaOccupancy.Release(observer.Id);
            world.Strategic.SuppressedCharacterContacts.Remove(CharacterEncounterService.ContactKey(observer.Id, id));
            s.Message = "违规被巡查者发现，主管怒气已满，正前来惩戒。";
            Pursue(world);
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
            world?.HuangcunSurvival?.Enabled == true && world.HuangcunSurvival.PunisherId == attacker.Value &&
            world.HuangcunSurvival.PunishmentTargetId == target.Value && !attacker.IsNone && !target.IsNone;
        public static bool IsNonlethalAttacker(SimulationWorld world, EntityId actor) =>
            world?.HuangcunSurvival?.PunishmentBattleActive == true && world.HuangcunSurvival.PunisherId == actor.Value;
        public static void FinishPunishment(SimulationWorld world)
        {
            var s = world.HuangcunSurvival;
            if (s.PunisherId != 0 && world.Entities.TryGet(new EntityId(s.PunisherId),out var actor) &&
                actor.TryGet<MovementIntentComponent>(out var intent) && intent.DestinationKey == "huangcun:discipline") intent.Clear();
            s.SupervisorAnger = s.ObservationSeconds = 0; s.PunisherId = s.PunishmentTargetId = 0; s.PunishmentBattleActive = false;
            world.SupervisorAnger.Value = 0;
        }
    }

    /// <summary>Transient input acknowledgement; reload must resume the real farm job to renew it.</summary>
    public sealed class ObservedLaborComponent : IComponent { public ulong ValidUntilTick; }
}
