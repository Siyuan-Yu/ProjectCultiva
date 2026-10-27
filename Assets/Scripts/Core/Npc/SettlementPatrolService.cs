using System;
using XianXia.Core.Actions;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Orders;
using XianXia.Core.Results;
using XianXia.Core.Schedule;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Npc
{
    /// <summary>Settlement duty uses the existing persistent schedule binding, not a second roster.</summary>
    public static class SettlementPatrolService
    {
        public const string DaySchedule = "base:schedule_patrol_day";
        public const string NightSchedule = "base:schedule_patrol_night";
        public static bool HasIndividualDuty(Entity entity) =>
            Shift(entity) != 0 || (entity != null && SupervisorRoutineDriver.Applies(entity));

        public static int Shift(Entity entity)
        {
            if (entity == null || !entity.TryGet<ScheduleComponent>(out var binding)) return 0;
            if (binding.DefinitionId == DaySchedule || binding.DefinitionId == "base:schedule_huangcun_guard_day") return 1;
            if (binding.DefinitionId == NightSchedule || binding.DefinitionId == "base:schedule_huangcun_guard_night") return 2;
            return 0;
        }

        public static bool CanRunSchedule(SimulationWorld world, Entity entity) =>
            world != null && entity != null && Shift(entity) != 0 &&
            world.Strategic.PlayerPartyContext?.IsMember(entity.Id) != true &&
            !SquadCommandService.OwnsIndividualSchedule(world, entity.Id) &&
            !world.BackgroundCharacterTravel.IsTraveling(entity.Id) &&
            !CharacterEncounterService.OwnsParticipantSpatialState(world, entity.Id) &&
            !SeparateSpaceTransitionService.IsOwnedByActiveSeparateSpace(world, entity.Id) &&
            (!world.Civilians.TryGet(entity.Id, out var state) || state.Disposition == CivilianDisposition.Normal);

        /// <summary>Stationed sentries are a squad for combat, not a moving field formation.</summary>
        public static void ReleaseStationaryDutyFormations(SimulationWorld world)
        {
            foreach (var squad in world.Strategic.Squads.Squads.Values)
            {
                if (squad.CommandKind != SquadCommandKind.SquadWorldMotion || squad.MemberCharacterIds.Count == 0 ||
                    SquadWorldMotionService.IsPlayerPartySquad(world, squad) ||
                    !world.Strategic.SquadWorldMotions.TryGet(squad.SquadId, out var motion) || motion.IsMoving) continue;
                var localDuty = true;
                foreach (var member in squad.MemberCharacterIds)
                {
                    var id = new EntityId(member);
                    if (!world.Entities.TryGet(id, out var actor) || !HasIndividualDuty(actor) ||
                        CharacterEncounterService.OwnsParticipantSpatialState(world, id) ||
                        !XianXia.Core.Construction.RoomBedFacilityService.TryGetAssignedBed(world, id, out var bed) ||
                        string.IsNullOrEmpty(bed.BoundWorldSiteId) ||
                        MortalCivilianQuery.ResolveCurrentSiteId(world, id) != bed.BoundWorldSiteId)
                    { localDuty = false; break; }
                }
                if (!localDuty) continue;
                squad.SetCommand(SquadCommandKind.None);
                world.Strategic.SquadWorldMotions.Remove(squad.SquadId);
            }
        }

        public static bool IsOnDuty(SimulationWorld world, Entity entity) =>
            CanRunSchedule(world, entity) && CombatLifeStateService.CanFight(entity) &&
            !PartyRestService.IsResting(world, entity) &&
            MortalTransportConstraintService.ValidateAttack(world, entity.Id).IsSuccess &&
            (!world.Civilians.TryGet(entity.Id, out var state) ||
                state.Activity != MortalActivity.Rescue && state.RescueTargetId.IsNone &&
                state.Activity != MortalActivity.SleepAtResidence && state.Activity != MortalActivity.SleepOnGround) &&
            entity.TryGet<ScheduleComponent>(out var schedule) && world.TryGetSchedule(schedule.DefinitionId, out var def) &&
            def.TryResolve(world.Tick, out var block) && block.Activity == ScheduleActivity.Patrol;

        public static bool CanAssign(SimulationWorld world, string siteId, Entity entity, out string reason)
        {
            reason = string.Empty;
            if (world == null || entity == null || string.IsNullOrEmpty(world.Strategic.PlayerFactionId) ||
                !world.Strategic.Sites.TryGet(siteId ?? string.Empty, out var site) || !site.IsCoreActive ||
                site.OwnerFactionId != world.Strategic.PlayerFactionId)
            { reason = "仅可安排我方已占领城池的巡卫。"; return false; }
            if ((entity.Tags & (EntityTag.Character | EntityTag.Npc)) == 0 ||
                !entity.TryGet<FactionMembershipComponent>(out var membership) || !membership.IsAffiliated ||
                membership.FactionId != world.Strategic.PlayerFactionId ||
                !PlayerPartyRuntime.CanPlayerControlCharacter(world, entity.Id) ||
                MortalCivilianQuery.ResolveCurrentSiteId(world, entity.Id) != siteId)
            { reason = "仅可指派本城我方可管理的常驻人员。"; return false; }
            if (!CombatLifeStateService.CanFight(entity) ||
                world.Strategic.PlayerPartyContext?.IsMember(entity.Id) == true ||
                SquadCommandService.OwnsIndividualSchedule(world, entity.Id) ||
                world.BackgroundCharacterTravel.IsTraveling(entity.Id) ||
                CharacterEncounterService.OwnsParticipantSpatialState(world, entity.Id) ||
                SeparateSpaceTransitionService.IsOwnedByActiveSeparateSpace(world, entity.Id) ||
                MortalTransportConstraintService.ValidateUnburdenedAction(world, entity.Id).IsFailure ||
                world.Civilians.TryGet(entity.Id, out var state) &&
                    (state.Disposition != CivilianDisposition.Normal || !state.RescueTargetId.IsNone) ||
                entity.TryGet<ActionStateComponent>(out var action) && action.HasActiveAction &&
                    (action.ActiveOrderSource == OrderSource.Player || action.ActiveOrderSource == OrderSource.Event) ||
                world.GetOrCreateOrderQueue(entity.Id).HasSource(OrderSource.Player) ||
                world.GetOrCreateOrderQueue(entity.Id).HasSource(OrderSource.Event))
            { reason = "请先解除随队／小队任务、战斗、运输或其他指令，并等待人物恢复行动。"; return false; }
            return true;
        }

        public static Result Assign(SimulationWorld world, SimulationLoop loop, string siteId, EntityId id, int shift)
        {
            if (loop == null || world == null || !world.Entities.TryGet(id, out var entity) || shift < 0 || shift > 2)
                return Result.Failure(ErrorCode.InvalidArgument, "无效巡卫排班。");
            if (!CanAssign(world, siteId, entity, out var reason))
                return Result.Failure(ErrorCode.InvalidOperation, reason);
            var scheduleId = shift == 1 ? DaySchedule : shift == 2 ? NightSchedule :
                MortalCivilianQuery.IsManagedCivilian(world, entity) ? "base:schedule_mortal_day" : "base:schedule_cultivator_day";
            if (!world.TryGetSchedule(scheduleId, out _))
                return Result.Failure(ErrorCode.NotFound, "巡卫或普通日程配置缺失。");
            loop.StopSubject(id);
            SettlementPatrolRouteDriver.ClearOwnedIntent(entity);
            world.WorkAreaOccupancy.Release(id);
            if (entity.TryGet<MovementIntentComponent>(out var movement)) movement.Clear();
            if (!entity.TryGet<ScheduleComponent>(out var binding))
            { binding = new ScheduleComponent(scheduleId); entity.AddComponent(binding); }
            else binding.DefinitionId = scheduleId;
            return Result.Success();
        }
    }
}
