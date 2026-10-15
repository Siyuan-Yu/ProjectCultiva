using XianXia.Core.Construction;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Npc;
using XianXia.Core.Orders;
using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.Actions
{
    /// <summary>Timed ordinary rest at one real residential/outdoor bed. It never restores combat pools.</summary>
    public sealed class BedRestAction : IAction
    {
        public const ulong DefaultDurationTicks = 6;
        public BedRestAction(ActionId id, EntityId subject, OrderId sourceOrderId, ulong durationTicks, string bedId)
        { Id = id; Subject = subject; SourceOrderId = sourceOrderId; BedId = bedId ?? string.Empty; Clock = ActionClock.Start(durationTicks); Status = ActionStatus.Pending; }
        public ActionId Id { get; }
        public EntityId Subject { get; }
        public OrderId SourceOrderId { get; }
        public string BedId { get; }
        public ActionStatus Status { get; private set; }
        public ActionClock Clock { get; private set; }

        public Result CanStart(SimulationWorld world)
        {
            var burden = MortalTransportConstraintService.ValidateUnburdenedAction(world, Subject);
            if (burden.IsFailure) return burden;
            if (world == null || !world.Entities.TryGet(Subject, out _) ||
                !world.OutdoorConstructedAssets.Assets.TryGetValue(BedId, out var bed) ||
                !RoomBedGeometry.IsBed(bed) ||
                !RoomBedFacilityService.BedHasUsage(world, bed, RoomUsage.Residential))
                return Result.Failure(ErrorCode.ActionCannotStart, "需要一张合法的普通休息床。");
            if (!RoomBedFacilityService.CanUseBed(world, bed, Subject, out var reason))
                return Result.Failure(ErrorCode.ActionCannotStart, reason);
            if (!RoomBedFacilityService.IsAtAccessPoint(world, bed, Subject))
                return Result.Failure(ErrorCode.ActionCannotStart, "需要先到达床边合法使用格。");
            return Result.Success();
        }
        public Result Start(SimulationWorld world)
        {
            var can = CanStart(world); if (can.IsFailure) return can;
            if (!RoomBedFacilityService.TryOccupy(world, BedId, Subject))
                return Result.Failure(ErrorCode.ActionCannotStart, "床位已被占用。");
            Status = ActionStatus.Running; return Result.Success();
        }
        public Result Advance(SimulationWorld world)
        {
            if (Status != ActionStatus.Running) return Result.Failure(ErrorCode.InvalidOperation, "Action not running.");
            if (!world.OutdoorConstructedAssets.Assets.TryGetValue(BedId, out var bed) ||
                !RoomBedFacilityService.BedHasUsage(world, bed, RoomUsage.Residential) ||
                bed.OccupantCharacterId != Subject || !RoomBedFacilityService.IsAtAccessPoint(world, bed, Subject))
            { Status = ActionStatus.Failed; RoomBedFacilityService.Release(world, Subject); return Result.Failure(ErrorCode.InvalidOperation, "床位已失效。"); }
            Clock = Clock.Consume(1); if (!Clock.IsComplete) return Result.Success();
            if (world.Civilians.TryGet(Subject, out var civilian))
            { civilian.SleptToday = true; civilian.Clamp(); }
            RoomBedFacilityService.Release(world, Subject); Status = ActionStatus.Completed; return Result.Success();
        }
        public void Cancel() { if (Status == ActionStatus.Pending || Status == ActionStatus.Running) Status = ActionStatus.Cancelled; }
        public void Restore(ActionStatus status, ActionClock clock) { Status = status; Clock = clock; }
    }
}
