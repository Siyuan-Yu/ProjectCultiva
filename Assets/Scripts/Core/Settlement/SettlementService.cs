using XianXia.Core.Domain.Ids;
using XianXia.Core.Events;
using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.Settlement
{
    /// <summary>Assign work roles and mutate settlement stock (rules only).</summary>
    public sealed class SettlementService
    {
        public Result AssignWork(
            SimulationWorld world,
            EntityId subject,
            WorkRoleKind role,
            string settlementId = null)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld is null.");
            if (!world.Entities.TryGet(subject, out var entity))
                return Result.Failure(ErrorCode.EntityNotFound, "Subject missing.", subject.ToString());

            var sid = settlementId;
            if (string.IsNullOrEmpty(sid))
                sid = world.Settlements.PrimarySettlementId;
            if (string.IsNullOrEmpty(sid) || !world.Settlements.TryGet(sid, out _))
                return Result.Failure(ErrorCode.NotFound, "Settlement missing for work assignment.", sid ?? "");

            if (role == WorkRoleKind.None)
                return Result.Failure(ErrorCode.InvalidArgument, "Work role required.");

            if (!entity.TryGet<WorkAssignmentComponent>(out var work))
            {
                work = new WorkAssignmentComponent();
                var added = entity.AddComponent(work);
                if (added.IsFailure)
                    return added;
            }

            work.Assign(sid, role);
            world.Events.Publish(
                EventType.WorkAssignmentChanged,
                world.Tick,
                target: subject,
                payload: sid + ":" + role);
            return Result.Success();
        }

        public Result AddStock(SimulationWorld world, string settlementId, string resourceId, int delta)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld is null.");
            if (!world.Settlements.TryGet(settlementId, out var settlement))
                return Result.Failure(ErrorCode.NotFound, "Settlement missing.", settlementId ?? "");
            if (string.IsNullOrEmpty(resourceId) || delta == 0)
                return Result.Failure(ErrorCode.InvalidArgument, "Resource delta invalid.");

            settlement.AddStock(resourceId, delta);
            world.Events.Publish(
                EventType.SettlementStockChanged,
                world.Tick,
                payload: settlementId + ":" + resourceId + ":" + settlement.GetStock(resourceId));
            return Result.Success();
        }
    }
}
