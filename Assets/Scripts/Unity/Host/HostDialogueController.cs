using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;

namespace XianXia.Unity.Host
{
    /// <summary>Builds dialogue view-model and resolves choices (presentation-agnostic).</summary>
    public sealed class HostDialogueController
    {
        ContentInteractionContext _topicContext;
        string _topicTrigger;
        string _failureKey, _failureMessage;
        public HostDialogueModel Model { get; } = new HostDialogueModel();

        public void Clear()
        {
            Model.IsActive = false;
            Model.IsFallback = false;
            Model.IsTopicSelection = false;
            Model.SpeakerName = string.Empty;
            Model.Body = string.Empty;
            Model.Choices.Clear();
        }

        public bool TryBuildFromActiveOnTalk(PlayableHostSession session, EntityId subject, EntityId speakerNpc)
        {
            Clear();
            if (session == null || !session.IsInitialized)
                return false;
            if (!TryGetActiveOnTalkSpec(session.World, out var spec))
                return false;

            return TryBuildFromActiveEvent(session);
        }

        public bool TryBuildFromActiveEvent(PlayableHostSession session)
        {
            Clear();
            var step = ContentEventService.ActiveStep(session.World);
            if (step == null) return false;
            Model.IsActive = true;
            Model.PageKey = session.World.ContentEvents.ActiveEventId + ":" + session.World.ContentEvents.ActiveStepId;
            var speaker = ContentEventService.ResolveSpeaker(session.World, step.SpeakerRef, out var name);
            Model.SpeakerName = speaker.IsFailure ? "【人物解析失败：" + step.SpeakerRef + "】" : name;
            Model.Body = string.IsNullOrEmpty(step.Text) ? "（无正文）" : step.Text;
            if (_failureKey == Model.PageKey) Model.Body += "\n" + _failureMessage;
            BuildChoices(session, step);
            return true;
        }

        public bool TryStartInteraction(PlayableHostSession session, EntityId actor, EntityId target, string definitionId)
            => TryStartInteraction(session, ContentInteractionContext.ForNpc(actor, target, definitionId), "onTalk");

        public bool TryStartInteraction(PlayableHostSession session, ContentInteractionContext context, string trigger)
        {
            if (session == null || !session.IsInitialized || context == null) return false;
            var service = new ContentEventService();
            var candidates = service.ResolveInteractionCandidates(session.World, context, trigger);
            if (candidates.Count == 0) return false;
            if (candidates.Count == 1)
            {
                if (service.BeginInteraction(session.World, context, candidates[0].Id, trigger).IsFailure) return false;
                return TryBuildFromActiveEvent(session);
            }
            Clear();
            _topicContext = context; _topicTrigger = trigger;
            Model.IsActive = true;
            Model.IsTopicSelection = true;
            Model.PageKey = "topics:" + context.TargetKey;
            Model.SpeakerName = !string.IsNullOrEmpty(context.TargetDisplayName)
                ? context.TargetDisplayName
                : session.World.Entities.TryGet(context.TargetEntityId, out var npc) ? npc.DisplayName : context.TargetDefinitionId;
            Model.Body = string.Equals(trigger, "onInspect", System.StringComparison.OrdinalIgnoreCase)
                ? "要调查什么？" : "想聊些什么？";
            foreach (var spec in candidates)
                Model.Choices.Add(new HostDialogueChoiceLine { ChoiceId = spec.Id,
                    Label = !string.IsNullOrEmpty(spec.TopicText) ? spec.TopicText : !string.IsNullOrEmpty(spec.Name) ? spec.Name : ShortId(spec.Id) });
            Model.Choices.Add(new HostDialogueChoiceLine { ChoiceId = "", Label = "离开" });
            return true;
        }

        public void ShowFallback(string speakerName, string body)
        {
            Clear();
            Model.IsActive = true;
            Model.IsFallback = true;
            Model.SpeakerName = string.IsNullOrEmpty(speakerName) ? "?" : speakerName;
            Model.Body = string.IsNullOrEmpty(body) ? "（无对话内容）" : body;
            Model.Choices.Add(new HostDialogueChoiceLine
            {
                ChoiceId = string.Empty,
                Label = "结束",
                Enabled = true
            });
        }

        public bool TrySelectChoice(
            int choiceIndex,
            PlayableHostSession session,
            HostCommandBridge bridge,
            PlayableHostBootstrap bootstrap)
        {
            if (!Model.IsActive || choiceIndex < 0 || choiceIndex >= Model.Choices.Count)
                return false;

            var line = Model.Choices[choiceIndex];
            if (!line.Enabled)
                return false;

            if (Model.IsFallback)
            {
                Clear();
                return true;
            }

            if (session == null || !session.IsInitialized) return false;
            if (Model.IsTopicSelection)
            {
                if (string.IsNullOrEmpty(line.ChoiceId)) { Clear(); return true; }
                var begin = new ContentEventService().BeginInteraction(
                    session.World, _topicContext, line.ChoiceId, _topicTrigger);
                if (begin.IsFailure)
                {
                    Model.Body = "话题条件已变化，请离开后重新交谈。";
                    return false;
                }
                bootstrap?.DispatchDrainedEvents();
                return TryBuildFromActiveEvent(session);
            }
            var step = ContentEventService.ActiveStep(session.World);
            var choice = step?.Choices.Find(c => c.Id == line.ChoiceId);
            string gameId = null;
            if (choice != null) TryGetStartMinigameId(choice, out gameId);
            var subject = session.World.ContentEvents.ActiveActorId;
            var result = new ContentEventService().ResolveChoice(session.World, subject, line.ChoiceId);
            if (result.IsFailure)
            {
                _failureKey = Model.PageKey;
                _failureMessage = "【结算失败，请重试：" + result.Error + "】";
                if (step != null) { Model.Body = step.Text + "\n" + _failureMessage; BuildChoices(session, step); }
                return false;
            }
            _failureKey = null; _failureMessage = null;
            bootstrap?.DispatchDrainedEvents();
            if (session.World.ContentEvents.HasActive) return TryBuildFromActiveEvent(session);
            Clear();
            if (string.Equals(gameId, HostJiangLaoChess.MinigameId, System.StringComparison.OrdinalIgnoreCase))
                bootstrap?.TicTacToePanel?.Open(subject, outcome =>
                {
                    if (!session.IsInitialized) return;
                    HostJiangLaoChess.ApplyResult(session.World, subject, outcome);
                    bootstrap.DispatchDrainedEvents();
                });
            return true;
        }

        public static bool IsActiveOnTalk(SimulationWorld world)
        {
            return TryGetActiveOnTalkSpec(world, out _);
        }

        public static bool TryGetActiveOnTalkSpec(SimulationWorld world, out ContentEventSpec spec)
        {
            spec = null;
            if (world == null || !world.ContentEvents.HasActive)
                return false;
            if (!world.ContentEvents.TryGet(world.ContentEvents.ActiveEventId, out spec) || spec == null)
                return false;
            return string.Equals(spec.Trigger, "onTalk", System.StringComparison.OrdinalIgnoreCase);
        }

        static bool TryGetStartMinigameId(ContentEventChoiceSpec choice, out string gameId)
        {
            gameId = null;
            if (choice?.Outcomes == null)
                return false;
            for (var i = 0; i < choice.Outcomes.Count; i++)
            {
                var o = choice.Outcomes[i];
                if (o == null || string.IsNullOrEmpty(o.Kind))
                    continue;
                if (!string.Equals(o.Kind?.Trim(), "startMinigame", System.StringComparison.OrdinalIgnoreCase))
                    continue;
                gameId = string.IsNullOrEmpty(o.Id) ? HostJiangLaoChess.MinigameId : o.Id;
                return true;
            }

            return false;
        }

        void BuildChoices(PlayableHostSession session, ContentEventStepSpec step)
        {
            Model.Choices.Clear();
            if (step.Choices.Count == 0)
            {
                Model.Choices.Add(new HostDialogueChoiceLine { ChoiceId = "", Label = "继续" });
                return;
            }
            foreach (var choice in step.Choices)
            {
                var ok = ContentEventService.ActiveConditionsPass(session.World, choice.Conditions);
                if (!ok && choice.UnavailableMode == "hidden") continue;
                var label = string.IsNullOrEmpty(choice.Text) ? choice.Id : choice.Text;
                if (!ok) label += "（" + (string.IsNullOrEmpty(choice.RequirementText) ? "条件未满足" : choice.RequirementText) + "）";
                Model.Choices.Add(new HostDialogueChoiceLine { ChoiceId = choice.Id, Label = label, Enabled = ok });
            }
        }

        static string ShortId(string id)
        {
            if (string.IsNullOrEmpty(id))
                return "?";
            var i = id.LastIndexOf('_');
            if (i >= 0 && i + 1 < id.Length)
                return id.Substring(i + 1);
            i = id.IndexOf(':');
            return i >= 0 && i + 1 < id.Length ? id.Substring(i + 1) : id;
        }
    }
}
