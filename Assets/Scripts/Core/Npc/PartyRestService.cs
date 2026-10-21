using System;
using XianXia.Core.Construction;
using XianXia.Core.Actions;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Orders;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Npc
{
    public static class PartyRestService
    {
        public const int WarningThreshold = 15;
        public const int ExhaustionRecoveryThreshold = 30;

        public static void EnterExhaustion(SimulationWorld world, Entity entity)
        {
            var state = NpcAutonomyState.Ensure(entity);
            if (!state.Exhausted && CharacterWorldPresenceQuery.TryResolve(world, entity.Id, out var at) &&
                at.HasWorldPosition && !string.IsNullOrEmpty(at.SurfaceId))
                world.WorldPresence.SetAtWorldPosition(entity.Id, at.WorldPosition, at.SurfaceId);
            state.Exhausted = true;
            state.DetachedByExhaustion = true;
            PlayerPartyTransitionMembership.CaptureTravelingMembersForPartyTransition(world, world.Strategic.PlayerPartyContext);
            if (world.Strategic.PlayerPartyContext?.ActiveCharacterId == entity.Id)
                PlayerPartyTravelRuntimeService.CancelTravel(world);
        }

        public static bool HasIndependentPosition(Entity entity) => entity != null &&
            entity.TryGet<NpcAutonomyState>(out var state) && state.DetachedByExhaustion;

        public static bool BlocksMovement(Entity entity) => entity != null &&
            entity.TryGet<NpcAutonomyState>(out var state) && state.Exhausted;

        public static bool IsResting(SimulationWorld world, Entity entity) => entity != null &&
            entity.TryGet<ActionStateComponent>(out var state) && state.HasActiveAction &&
            world.ActiveActions.TryGetValue(state.ActiveActionId, out var action) &&
            (action is RestAction || action is BedRestAction);

        public static Result Begin(SimulationLoop loop, EntityId subject, ulong ticks, string bedId = null)
        {
            var world = loop.World;
            var party = world.Strategic.PlayerPartyContext;
            if (party == null || !party.IsMember(subject) || ticks == 0 ||
                world.Strategic.CharacterEncounter != null)
                return Result.Failure(ErrorCode.InvalidOperation, "当前不能安排全队休息。");
            if (!world.Entities.TryGet(subject, out var actor) || !CombatLifeStateService.CanFight(actor))
                return Result.Failure(ErrorCode.ActionCannotStart, "当前人物无法休息。");
            if (!string.IsNullOrEmpty(bedId) &&
                (!world.OutdoorConstructedAssets.Assets.TryGetValue(bedId, out var bed) ||
                 !RoomBedFacilityService.CanUseBed(world, bed, subject, out _) ||
                 !RoomBedFacilityService.IsAtAccessPoint(world, bed, subject)))
                return Result.Failure(ErrorCode.ActionCannotStart, "请先到达可用床边。");
            var canRest = string.IsNullOrEmpty(bedId)
                ? new RestAction(ActionId.None, subject, OrderId.None, ticks).CanStart(world)
                : new BedRestAction(ActionId.None, subject, OrderId.None, ticks, bedId).CanStart(world);
            if (canRest.IsFailure) return canRest;
            PlayerPartyTravelRuntimeService.CancelTravel(world);
            foreach (var id in party.Members)
            {
                if (!world.Entities.TryGet(id, out var e) || !CombatLifeStateService.CanFight(e) ||
                    MortalTransportConstraintService.ValidateUnburdenedAction(world, id).IsFailure) continue;
                loop.StopSubject(id);
                if (e.TryGet<MovementIntentComponent>(out var movement)) movement.Clear();
                var personal = PersonalLifeService.Ensure(world, e);
                if (personal != null) { personal.Activity = PersonalLifeActivity.Resting; personal.Status = "全队休息"; }
                var useBed = id == subject && !string.IsNullOrEmpty(bedId);
                loop.EnqueueOrder(new Order(loop.AllocateOrderId(), id,
                    useBed ? OrderType.RestAtBed : OrderType.Rest, OrderSource.Player,
                    waitTicks: ticks, targetRef: useBed ? bedId : string.Empty));
            }
            return Result.Success();
        }
    }
}
