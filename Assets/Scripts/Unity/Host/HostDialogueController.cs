using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;

namespace XianXia.Unity.Host
{
    /// <summary>Builds dialogue view-model and resolves choices (presentation-agnostic).</summary>
    public sealed class HostDialogueController
    {
        public HostDialogueModel Model { get; } = new HostDialogueModel();

        public void Clear()
        {
            Model.IsActive = false;
            Model.IsFallback = false;
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

            Model.IsActive = true;
            Model.IsFallback = false;
            Model.SpeakerName = ResolveSpeakerName(session, speakerNpc, spec);
            Model.Body = string.IsNullOrEmpty(spec.Body) ? "（无正文）" : spec.Body;
            BuildChoices(session, subject, spec);
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

            if (string.IsNullOrEmpty(line.ChoiceId))
            {
                if (session.World.ContentEvents.HasActive)
                    session.World.ContentEvents.ClearActive();
                Clear();
                return true;
            }

            if (bridge == null || session == null || !session.IsInitialized)
                return false;

            if (!bridge.ResolveContentChoice(line.ChoiceId))
                return false;

            bootstrap?.DispatchDrainedEvents();

            if (session.World.ContentEvents.HasActive &&
                TryGetActiveOnTalkSpec(session.World, out var spec))
            {
                var subject = ResolveSubject(session);
                Model.Choices.Clear();
                BuildChoices(session, subject, spec);
                return true;
            }

            Clear();
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

        void BuildChoices(PlayableHostSession session, EntityId subject, ContentEventSpec spec)
        {
            Model.Choices.Clear();
            if (spec.Choices == null || spec.Choices.Count == 0)
            {
                Model.Choices.Add(new HostDialogueChoiceLine
                {
                    ChoiceId = string.Empty,
                    Label = "继续",
                    Enabled = true
                });
                return;
            }

            for (var i = 0; i < spec.Choices.Count; i++)
            {
                var choice = spec.Choices[i];
                if (choice == null)
                    continue;
                var ok = ContentConditionEvaluator.AllPass(session.World, subject, choice.Conditions);
                var label = string.IsNullOrEmpty(choice.Text) ? choice.Id : choice.Text;
                if (!ok)
                    label += "（条件未满足）";
                Model.Choices.Add(new HostDialogueChoiceLine
                {
                    ChoiceId = choice.Id ?? string.Empty,
                    Label = label,
                    Enabled = ok
                });
            }
        }

        static EntityId ResolveSubject(PlayableHostSession session)
        {
            return session.CharacterIds.Count > 0 ? session.CharacterIds[0] : EntityId.None;
        }

        static string ResolveSpeakerName(PlayableHostSession session, EntityId speakerNpc, ContentEventSpec spec)
        {
            if (!speakerNpc.IsNone &&
                session.World.Entities.TryGet(speakerNpc, out var entity) &&
                !string.IsNullOrEmpty(entity.DisplayName))
                return entity.DisplayName;
            if (!string.IsNullOrEmpty(spec.Name))
                return spec.Name;
            return ShortId(spec.Id);
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
