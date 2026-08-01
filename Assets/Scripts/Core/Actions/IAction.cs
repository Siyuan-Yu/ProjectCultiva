using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Orders;
using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.Actions
{
    public interface IAction
    {
        ActionId Id { get; }

        EntityId Subject { get; }

        OrderId SourceOrderId { get; }

        ActionStatus Status { get; }

        ActionClock Clock { get; }

        Result CanStart(SimulationWorld world);

        Result Start(SimulationWorld world);

        /// <summary>Consume one world tick of duration.</summary>
        Result Advance(SimulationWorld world);

        /// <summary>Cancel a pending/running action (VS0.2 player override).</summary>
        void Cancel();
    }
}
