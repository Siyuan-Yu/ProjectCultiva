using XianXia.Core.Content;
using XianXia.Core.Cultivation;
using XianXia.Core.Exploration;
using XianXia.Core.Results;
using XianXia.Core.Inventory;
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
        readonly ExplorationService _exploration;
        readonly ContentEventService _contentEvents;
        readonly QuestService _quests;

        public PlayerInputPort(
            SimulationLoop loop,
            PlayerOrderFactory factory = null,
            CultivationAttemptGate cultivationGate = null,
            SocialInteractionService social = null,
            RecruitService recruit = null,
            ExplorationService exploration = null)
        {
            _loop = loop ?? throw new System.ArgumentNullException(nameof(loop));
            _factory = factory ?? new PlayerOrderFactory();
            _cultivationGate = cultivationGate ?? new CultivationAttemptGate();
            _social = social ?? new SocialInteractionService();
            _recruit = recruit ?? new RecruitService();
            _exploration = exploration ?? new ExplorationService();
            _contentEvents = new ContentEventService();
            _quests = new QuestService();
        }

        public Result Submit(PlayerCommandRequest request)
        {
            if (request == null)
                return Result.Failure(ErrorCode.InvalidArgument, "PlayerCommandRequest is null.");
            // Stable command number 14 is retired. Reject before interrupting any action
            // or consuming an item, including direct calls made by older clients.
            if (request.Kind == PlayerCommandKind.UseConcealGrass)
                return Result.Failure(ErrorCode.InvalidOperation, "暴露值机制已移除，敛息草降暴露指令已停用。");
            if ((request.Kind == PlayerCommandKind.Labor || request.Kind == PlayerCommandKind.Cultivate) &&
                XianXia.Core.Actions.AutonomousActionContinuationService.BlocksOrdinaryWork(_loop.World, request.Subject))
                return Result.Failure(ErrorCode.ActionCannotStart,
                    XianXia.Core.Actions.AutonomousActionContinuationService.EncounterWorkBlockedReason);

            if (_loop.World.Strategic.PlayerPartyContext != null &&
                !XianXia.Core.World.PlayerPartyRuntime.CanPlayerControlCharacter(_loop.World,request.Subject))
                return Result.Failure(ErrorCode.InvalidOperation,"该角色不可接受玩家手动指令。");

            if (_loop.World.Entities.TryGet(request.Subject, out var tired) &&
                XianXia.Core.Npc.PartyRestService.BlocksMovement(tired) &&
                request.Kind != PlayerCommandKind.Rest && request.Kind != PlayerCommandKind.RestAtBed && request.Kind != PlayerCommandKind.Stop)
                return Result.Failure(ErrorCode.ActionCannotStart, "精力耗尽，请先休息恢复。");

            XianXia.Core.Npc.ChessRecreationService.Interrupt(_loop.World, _loop, request.Subject);
            if (request.IsSocialIntent)
                return SubmitSocial(request);

            if (request.IsInstantUtilityIntent)
                return SubmitUtility(request);

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

            if ((request.Kind == PlayerCommandKind.Rest || request.Kind == PlayerCommandKind.RestAtBed) &&
                _loop.World.Strategic.PlayerPartyContext?.IsMember(request.Subject) == true)
                return XianXia.Core.Npc.PartyRestService.Begin(_loop, request.Subject, request.DurationTicks,
                    request.Kind == PlayerCommandKind.RestAtBed ? request.TargetLocationId : null);

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

            return Result.Failure(ErrorCode.InvalidArgument, "Unsupported utility kind.");
        }

        Result SubmitExploration(PlayerCommandRequest request)
        {
            if (request.Kind == PlayerCommandKind.Explore)
                return _exploration.ExploreHere(_loop.World, request.Subject);
            if (request.Kind == PlayerCommandKind.Travel)
                return _exploration.Travel(_loop.World, request.Subject, request.TargetLocationId);
            if (request.Kind == PlayerCommandKind.EnterLocalMap)
                return _exploration.EnterLocalMap(_loop.World, request.Subject, request.TargetLocationId);
            if (request.Kind == PlayerCommandKind.LeaveLocalMap)
                return _exploration.LeaveLocalMap(_loop.World, request.Subject);
            if (request.Kind == PlayerCommandKind.SurveyEntrance)
                return _exploration.SurveyEntrance(_loop.World, request.Subject, request.TargetLocationId);
            return Result.Failure(ErrorCode.InvalidArgument, "Unsupported exploration kind.");
        }

        Result SubmitContent(PlayerCommandRequest request)
        {
            if (request.Kind == PlayerCommandKind.ResolveContentChoice)
                return _contentEvents.ResolveChoice(_loop.World, request.Subject, request.ChoiceId);
            if (request.Kind == PlayerCommandKind.StartQuest)
                return _quests.TryStart(_loop.World, request.QuestId, request.Subject);
            if (request.Kind == PlayerCommandKind.ClaimQuestRewards)
                return _quests.TryClaimRewards(_loop.World, request.QuestId, request.Subject);
            if (request.Kind == PlayerCommandKind.AbandonQuest)
                return _quests.TryAbandon(_loop.World, request.QuestId, request.Subject);
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
