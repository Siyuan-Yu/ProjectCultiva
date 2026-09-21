using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Orders;
using XianXia.Core.Simulation;
using XianXia.Core.Npc;

namespace XianXia.Core.Actions
{
    /// <summary>Own-body actions stop when their subject can no longer act.</summary>
    public static class AutonomousActionContinuationService
    {
        public static bool IsAutonomous(IAction action) =>
            action is CultivateAction || action is LaborAction || action is WorkAction ||
            action is MoveAction || action is RestAction || action is ObserveAction;

        public static bool IsAutonomous(OrderType type) =>
            type == OrderType.Cultivate || type == OrderType.Labor || type == OrderType.Work ||
            type == OrderType.Move || type == OrderType.Rest || type == OrderType.Observe;

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
