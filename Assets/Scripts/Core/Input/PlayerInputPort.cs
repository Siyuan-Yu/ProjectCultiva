using XianXia.Core.Concealment;
using XianXia.Core.Content;
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
        readonly ContentEventService _contentEvents;
        readonly QuestService _quests;

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
            _contentEvents = new ContentEventService();
            _quests = new QuestService();
        }

        public Result Submit(PlayerCommandRequest request)
        {
            if (request == null)
                return Result.Failure(ErrorCode.InvalidArgument, "PlayerCommandRequest is null.");

            if (request.IsSocialIntent)
                return SubmitSocial(request);

            if (request.IsInstantUtilityIntent)
                return SubmitUtility(request);

            if (request.IsSettlementIntent)
                return _settlement.AssignWork(_loop.World, request.Subject, request.WorkRole);

            if (request.IsExplorationIntent)
                return SubmitExploration(request);

            if (request.IsContentIntent)
                return SubmitContent(request);

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

        Result SubmitUtility(PlayerCommandRequest request)
        {
            if (request.Kind == PlayerCommandKind.Stop)
                return _loop.StopSubject(request.Subject);

            if (request.Kind == PlayerCommandKind.UseConcealGrass)
                return UseConcealGrass(request.Subject);

            return Result.Failure(ErrorCode.InvalidArgument, "Unsupported utility kind.");
        }

        Result UseConcealGrass(XianXia.Core.Domain.Ids.EntityId subject)
        {
            const string grassId = "base:resource_conceal_grass";
            const int riskDrop = 15;
            var world = _loop.World;
            if (!world.Settlements.TryGetPrimary(out var settlement))
                return Result.Failure(ErrorCode.NotFound, "No settlement for conceal grass.");
            if (settlement.GetStock(grassId) < 1)
                return Result.Failure(ErrorCode.InvalidOperation, "No conceal grass stock.");
            if (!world.Entities.TryGet(subject, out var entity) ||
                !entity.TryGet<PersonalConcealmentRiskComponent>(out var risk))
                return Result.Failure(ErrorCode.ComponentMissing, "Concealment risk missing.");

            var spent = _settlement.AddStock(world, settlement.Id, grassId, -1);
            if (spent.IsFailure)
                return spent;
            risk.Add(-riskDrop);
            return Result.Success();
        }

        Result SubmitExploration(PlayerCommandRequest request)
        {
            if (request.Kind == PlayerCommandKind.Explore)
                return _exploration.ExploreHere(_loop.World, request.Subject);
            if (request.Kind == PlayerCommandKind.Travel)
                return _exploration.Travel(_loop.World, request.Subject, request.TargetLocationId);
            return Result.Failure(ErrorCode.InvalidArgument, "Unsupported exploration kind.");
        }

        Result SubmitContent(PlayerCommandRequest request)
        {
            if (request.Kind == PlayerCommandKind.ResolveContentChoice)
                return _contentEvents.ResolveChoice(_loop.World, request.Subject, request.ChoiceId);
            if (request.Kind == PlayerCommandKind.StartQuest)
                return _quests.TryStart(_loop.World, request.QuestId, request.Subject);
            return Result.Failure(ErrorCode.InvalidArgument, "Unsupported content kind.");
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
