using System;
using XianXia.Core.Domain.Ids;
using System.Collections.Generic;

namespace XianXia.Core.Content
{
    public sealed class ContentEventBoard
    {
        readonly Dictionary<string, ContentEventSpec> _specs =
            new Dictionary<string, ContentEventSpec>(StringComparer.Ordinal);
        readonly HashSet<string> _fired = new HashSet<string>(StringComparer.Ordinal);

        public string ActiveEventId { get; private set; } = string.Empty;

        public string ActiveStepId { get; private set; } = string.Empty;
        public EntityId ActiveActorId { get; private set; } = EntityId.None;
        public EntityId ActiveTargetEntityId { get; private set; } = EntityId.None;
        public string ActiveTargetKind { get; private set; } = string.Empty;
        public string ActiveTargetKey { get; private set; } = string.Empty;
        public string ActiveTargetDefinitionId { get; private set; } = string.Empty;
        public string ActiveTargetDisplayName { get; private set; } = string.Empty;
        public EntityId ActiveIssuerEntityId { get; private set; } = EntityId.None;
        public bool ActiveInteraction { get; private set; }
        public string ActiveScheduledInstanceId { get; private set; } = string.Empty;
        public string ActiveOpportunityInstanceId { get; private set; } = string.Empty;

        internal void SetScheduledActive(ScheduledContentEventInstance item)
        { SetActive(item.EventId, item.Context(), false); ActiveScheduledInstanceId = item.InstanceId; }

        public string FiredKey(ContentEventSpec spec, EntityId actor, EntityId target)
            => FiredKey(spec, actor, target.IsNone ? string.Empty : target.Value.ToString());

        public string FiredKey(ContentEventSpec spec, EntityId actor, string stableTargetKey)
        {
            // Length prefix keeps scoped instance keys disjoint from authored definition IDs.
            if (spec.OnceScope == "perTarget") return "#target:" + spec.Id.Length + ":" + spec.Id + ":" + stableTargetKey;
            if (spec.OnceScope == "perActorTarget") return "#pair:" + spec.Id.Length + ":" + spec.Id + ":" + actor.Value + ":" + stableTargetKey;
            return spec.Id;
        }

        public IReadOnlyDictionary<string, ContentEventSpec> Specs => _specs;

        public bool HasActive => !string.IsNullOrEmpty(ActiveEventId);

        public void Register(ContentEventSpec spec)
        {
            if (spec == null || string.IsNullOrEmpty(spec.Id))
                throw new ArgumentException("ContentEventSpec requires Id.");
            _specs[spec.Id] = spec;
        }

        public bool TryGet(string id, out ContentEventSpec spec) =>
            _specs.TryGetValue(id ?? string.Empty, out spec);

        public bool HasFired(string id) => _fired.Contains(id ?? string.Empty);

        public void MarkFired(string id)
        {
            if (!string.IsNullOrEmpty(id))
                _fired.Add(id);
        }

        public List<string> CaptureFiredKeys() => new List<string>(_fired);

        public void RestoreFiredKeys(IEnumerable<string> keys)
        {
            _fired.Clear();
            if (keys != null) foreach (var key in keys) _fired.Add(key);
            ClearActive();
        }

        public void SetActive(string id) => SetActive(id, EntityId.None, EntityId.None, false);

        public void SetActive(string id, EntityId actor, EntityId target, bool interaction)
            => SetActive(id, new ContentInteractionContext
            {
                ActorId = actor,
                TargetEntityId = target,
                TargetKey = target.IsNone ? string.Empty : target.Value.ToString()
            }, interaction);

        public void SetActive(string id, ContentInteractionContext context, bool interaction)
        {
            context = context ?? new ContentInteractionContext();
            ActiveEventId = id ?? string.Empty;
            ActiveScheduledInstanceId = string.Empty;
            ActiveOpportunityInstanceId = context.OpportunityInstanceId ?? string.Empty;
            ActiveActorId = context.ActorId;
            ActiveTargetEntityId = context.TargetEntityId;
            ActiveTargetKind = context.TargetKind ?? string.Empty;
            ActiveTargetKey = context.TargetKey ?? string.Empty;
            ActiveTargetDefinitionId = context.TargetDefinitionId ?? string.Empty;
            ActiveTargetDisplayName = context.TargetDisplayName ?? string.Empty;
            ActiveIssuerEntityId = context.IssuerEntityId;
            ActiveInteraction = interaction;
            ActiveStepId = TryGet(id, out var spec) ? (spec.Steps.Count == 0 ? "$legacy" : spec.EntryStepId) : "";
        }

        public void AdvanceStep(string id) => ActiveStepId = id ?? string.Empty;
        public void ClearActive() => SetActive("", EntityId.None, EntityId.None, false);

        public ContentInteractionContext ActiveContext() => new ContentInteractionContext
        {
            ActorId = ActiveActorId, TargetEntityId = ActiveTargetEntityId,
            TargetKind = ActiveTargetKind, TargetKey = ActiveTargetKey,
            TargetDefinitionId = ActiveTargetDefinitionId,
            TargetDisplayName = ActiveTargetDisplayName,
            IssuerEntityId = ActiveIssuerEntityId, OpportunityInstanceId = ActiveOpportunityInstanceId
        };

        internal sealed class RuntimeState
        {
            public string Event, Step, Scheduled, Opportunity;
            public EntityId Actor, Target, Issuer;
            public string TargetKind, TargetKey, TargetDefinitionId, TargetDisplayName;
            public bool Interaction;
            public List<string> Fired;
        }
        internal RuntimeState CaptureState() => new RuntimeState
        {
            Event = ActiveEventId, Step = ActiveStepId, Scheduled = ActiveScheduledInstanceId, Opportunity = ActiveOpportunityInstanceId, Actor = ActiveActorId,
            Target = ActiveTargetEntityId, TargetKind = ActiveTargetKind, TargetKey = ActiveTargetKey,
            TargetDefinitionId = ActiveTargetDefinitionId, TargetDisplayName = ActiveTargetDisplayName,
            Issuer = ActiveIssuerEntityId,
            Interaction = ActiveInteraction, Fired = new List<string>(_fired)
        };
        internal void RestoreState(RuntimeState state)
        {
            ActiveEventId = state.Event;
            ActiveScheduledInstanceId = state.Scheduled; ActiveOpportunityInstanceId = state.Opportunity;
            ActiveStepId = state.Step;
            ActiveActorId = state.Actor;
            ActiveTargetEntityId = state.Target;
            ActiveTargetKind = state.TargetKind ?? string.Empty;
            ActiveTargetKey = state.TargetKey ?? string.Empty;
            ActiveTargetDefinitionId = state.TargetDefinitionId ?? string.Empty;
            ActiveTargetDisplayName = state.TargetDisplayName ?? string.Empty;
            ActiveIssuerEntityId = state.Issuer;
            ActiveInteraction = state.Interaction;
            _fired.Clear();
            foreach (var key in state.Fired) _fired.Add(key);
        }
    }
}
