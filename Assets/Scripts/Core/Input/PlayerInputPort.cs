using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.Input
{
    /// <summary>
    /// Core-only player ingress: intent → PlayerOrderFactory → SimulationLoop enqueue.
    /// </summary>
    public sealed class PlayerInputPort : IPlayerInputPort
    {
        readonly SimulationLoop _loop;
        readonly PlayerOrderFactory _factory;

        public PlayerInputPort(SimulationLoop loop, PlayerOrderFactory factory = null)
        {
            _loop = loop ?? throw new System.ArgumentNullException(nameof(loop));
            _factory = factory ?? new PlayerOrderFactory();
        }

        public Result Submit(PlayerCommandRequest request)
        {
            if (request == null)
                return Result.Failure(ErrorCode.InvalidArgument, "PlayerCommandRequest is null.");

            var orderId = _loop.AllocateOrderId();
            var created = _factory.Create(orderId, request);
            if (created.IsFailure)
                return Result.Failure(created.Error);

            return _loop.EnqueueOrder(created.Value);
        }
    }
}
