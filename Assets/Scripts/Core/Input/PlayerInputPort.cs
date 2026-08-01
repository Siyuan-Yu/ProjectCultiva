using XianXia.Core.Cultivation;
using XianXia.Core.Exploration;
using XianXia.Core.Results;
using XianXia.Core.Settlement;
using XianXia.Core.Simulation;
using XianXia.Core.Social;

namespace XianXia.Core.Input
{
    /// <summary>
    /// Core-only player ingress: Order intents → SimulationLoop; social intents → existing services.
    /// </summary>
    public sealed class PlayerInputPort : IPlayerInputPort
    {
        readonly SimulationLoop _loop;
        readonly PlayerOrderFactory _factory;
        readonly CultivationAttemptGate _cultivationGate;
        readonly SocialInteractionService _social;
        readonly RecruitService _recruit;
        readonly SettlementService _settlement;
        readonly ExplorationService _exploration;

        public PlayerInputPort(
            SimulationLoop loop,
            PlayerOrderFactory factory = null,
            CultivationAttemptGate cultivationGate = null,
            SocialInteractionService social = null,
            RecruitService recruit = null,
            SettlementService settlement = null,
            ExplorationService exploration = null)
        {
            _loop = loop ?? throw new System.ArgumentNullException(nameof(loop));
            _factory = factory ?? new PlayerOrderFactory();
            _cultivationGate = cultivationGate ?? new CultivationAttemptGate();
            _social = social ?? new SocialInteractionService();
            _recruit = recruit ?? new RecruitService();
            _settlement = settlement ?? new SettlementService();
            _exploration = exploration ?? new ExplorationService();
        }

        public Result Submit(PlayerCommandRequest request)
        {
            if (request == null)
                return Result.Failure(ErrorCode.InvalidArgument, "PlayerCommandRequest is null.");

            if (request.IsSocialIntent)
                return SubmitSocial(request);

            if (request.IsSettlementIntent)
                return _settlement.AssignWork(_loop.World, request.Subject, request.WorkRole);

            if (request.IsExplorationIntent)
                return SubmitExploration(request);

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

        Result SubmitExploration(PlayerCommandRequest request)
        {
            if (request.Kind == PlayerCommandKind.Explore)
                return _exploration.ExploreHere(_loop.World, request.Subject);
            if (request.Kind == PlayerCommandKind.Travel)
                return _exploration.Travel(_loop.World, request.Subject, request.TargetLocationId);
            return Result.Failure(ErrorCode.InvalidArgument, "Unsupported exploration kind.");
        }

        Result SubmitSocial(PlayerCommandRequest request)
        {
            if (request.Subject.IsNone || request.Target.IsNone)
            {
                return Result.Failure(
                    ErrorCode.InvalidArgument,
                    "Social command requires Subject (actor) and Target.");
            }

            if (request.Subject == request.Target)
            {
                return Result.Failure(
                    ErrorCode.InvalidArgument,
                    "Social actor and target must differ.");
            }

            switch (request.Kind)
            {
                case PlayerCommandKind.Help:
                    return _social.Help(_loop.World, request.Subject, request.Target);
                case PlayerCommandKind.Slight:
                    return _social.Slight(_loop.World, request.Subject, request.Target);
                case PlayerCommandKind.Recruit:
                    return _recruit.TryRecruit(_loop.World, request.Subject, request.Target);
                default:
                    return Result.Failure(ErrorCode.InvalidArgument, "Unsupported social kind.");
            }
        }
    }
}
