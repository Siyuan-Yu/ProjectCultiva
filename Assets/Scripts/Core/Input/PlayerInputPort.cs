using XianXia.Core.Cultivation;
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
        readonly CultivationAttemptGate _cultivationGate;

        public PlayerInputPort(
            SimulationLoop loop,
            PlayerOrderFactory factory = null,
            CultivationAttemptGate cultivationGate = null)
        {
            _loop = loop ?? throw new System.ArgumentNullException(nameof(loop));
            _factory = factory ?? new PlayerOrderFactory();
            _cultivationGate = cultivationGate ?? new CultivationAttemptGate();
        }

        public Result Submit(PlayerCommandRequest request)
        {
            if (request == null)
                return Result.Failure(ErrorCode.InvalidArgument, "PlayerCommandRequest is null.");

            if (request.Kind == PlayerCommandKind.Cultivate)
            {
                var prepared = _cultivationGate.Prepare(_loop.World, request.Subject);
                if (prepared.IsFailure)
                    return Result.Failure(prepared.Error);
            }

            var orderId = _loop.AllocateOrderId();
            var created = _factory.Create(orderId, request);
            if (created.IsFailure)
                return Result.Failure(created.Error);

            return _loop.EnqueueOrder(created.Value);
        }
    }
}
