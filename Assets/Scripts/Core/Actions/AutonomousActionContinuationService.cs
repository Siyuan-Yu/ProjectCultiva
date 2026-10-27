using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Orders;
using XianXia.Core.Simulation;
using XianXia.Core.Npc;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Actions
{
    /// <summary>Own-body actions stop when their subject can no longer act.</summary>
    public static class AutonomousActionContinuationService
    {
        public const string EncounterWorkBlockedReason = "战斗中不能工作、采集或修炼，请先结束战斗。";

        public static bool BlocksOrdinaryWork(SimulationWorld world, EntityId subject) =>
            CharacterEncounterService.OwnsParticipantSpatialState(world, subject);

        /// <summary>The encounter takes over ordinary actions without moving the body.</summary>
        public static void CancelForEncounter(SimulationWorld world, EntityId subject, bool releaseBed = false)
        {
            if (!BlocksOrdinaryWork(world, subject)) return;
            world.GetOrCreateOrderQueue(subject).RemoveWhere(o => IsAutonomous(o.Type));
            world.WorkAreaOccupancy.Release(subject);
            if (!world.Entities.TryGet(subject, out var entity)) return;
            if (entity.TryGet<MovementIntentComponent>(out var movement) && movement.Active) movement.Clear();
            if (world.Civilians.TryGet(subject, out var civilian))
            {
                releaseBed |= civilian.SleepPhase != CivilianSleepPhase.None;
                civilian.SleepPhase = CivilianSleepPhase.None;
                civilian.SleepStartedTick = 0;
            }
            if (entity.TryGet<ActionStateComponent>(out var activeState) && activeState.HasActiveAction &&
                world.ActiveActions.TryGetValue(activeState.ActiveActionId, out var oldAction) && oldAction is BedRestAction)
                releaseBed = true;
            if (releaseBed && CombatLifeStateService.CanFight(entity))
                XianXia.Core.Construction.RoomBedFacilityService.Release(world, subject);
            if (entity.TryGet<ActionStateComponent>(out var state) && state.HasActiveAction &&
                world.ActiveActions.TryGetValue(state.ActiveActionId, out var action) && IsAutonomous(action))
            {
                action.Cancel();
                world.ActiveActions.Remove(action.Id);
                state.ActiveActionId = ActionId.None;
                state.ActiveClock = null;
                state.ActiveOrderSource = OrderSource.Player;
            }
        }

        public static bool IsAutonomous(IAction action) =>
            action is CultivateAction || action is LaborAction || action is WorkAction ||
            action is MoveAction || action is RestAction || action is BedRestAction || action is RecoveryAction ||
            action is ObserveAction || action is HuangcunSurvivalAction;

        public static bool IsAutonomous(OrderType type) =>
            type == OrderType.Cultivate || type == OrderType.Labor || type == OrderType.Work ||
            type == OrderType.Move || type == OrderType.Rest || type == OrderType.RestAtBed || type == OrderType.Recover ||
            type == OrderType.Observe || type == OrderType.HuangcunSurvival;

        public static bool CanContinue(SimulationWorld world, EntityId subject)
        {
            return world != null && world.Entities.TryGet(subject, out var entity) &&
                   entity.TryGet<LifecycleComponent>(out var life) &&
                   life.State == LifecycleState.Alive;
        }

        public static bool CancelInvalid(SimulationWorld world, EntityId subject)
        {
            if (world == null || CanContinue(world, subject))
                return false;

            var changed = false;
            world.GetOrCreateOrderQueue(subject).RemoveWhere(o => IsAutonomous(o.Type));
            // A scheduled NPC may have soft-claimed a slot before its queued action starts.
            // Releasing by subject is ownership-scoped and cannot disturb another worker.
            world.WorkAreaOccupancy.Release(subject);
            if (world.Entities.TryGet(subject, out var entity) &&
                entity.TryGet<ActionStateComponent>(out var state) && state.HasActiveAction &&
                world.ActiveActions.TryGetValue(state.ActiveActionId, out var action) &&
                IsAutonomous(action))
            {
                action.Cancel();
                if (action is BedRestAction)
                    XianXia.Core.Construction.RoomBedFacilityService.Release(world, subject);
                world.ActiveActions.Remove(action.Id);
                if (action is MoveAction && entity.TryGet<MovementIntentComponent>(out var intent))
                    intent.Clear();
                state.ActiveActionId = ActionId.None;
                state.ActiveClock = null;
                state.ActiveOrderSource = OrderSource.Player;
                changed = true;
            }

            return changed;
        }
    }
}
