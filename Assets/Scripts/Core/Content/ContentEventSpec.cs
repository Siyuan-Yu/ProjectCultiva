using System.Collections.Generic;
using XianXia.Core.Domain.Ids;

namespace XianXia.Core.Content
{
    /// <summary>Interaction identity shared by character and fixed-world object targets.</summary>
    public sealed class ContentInteractionContext
    {
        public EntityId ActorId { get; set; } = EntityId.None;
        public EntityId TargetEntityId { get; set; } = EntityId.None;
        public string TargetKind { get; set; } = string.Empty;
        public string TargetKey { get; set; } = string.Empty;
        public string TargetDefinitionId { get; set; } = string.Empty;
        public string TargetDisplayName { get; set; } = string.Empty;

        public static ContentInteractionContext ForNpc(
            EntityId actor, EntityId target, string definitionId, string displayName = null) =>
            new ContentInteractionContext
            {
                ActorId = actor,
                TargetEntityId = target,
                TargetKind = "npc",
                // Keep the pre-EVENT-01 Final per-target key payload byte-for-byte compatible.
                TargetKey = target.IsNone ? string.Empty : target.Value.ToString(),
                TargetDefinitionId = definitionId ?? string.Empty,
                TargetDisplayName = displayName ?? string.Empty
            };
    }

    public sealed class ContentEventSpec
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        /// <summary>onExplore | onArrive | onQuestCompleted | onTalk | onInspect | manual</summary>
        public string Trigger { get; set; } = string.Empty;
        public string LocationId { get; set; } = string.Empty;
        public string QuestId { get; set; } = string.Empty;
        /// <summary>onTalk: character definition id to match.</summary>
        public string NpcDefinitionId { get; set; } = string.Empty;
        /// <summary>onTalk: every tag must exist on the actual target entity.</summary>
        public List<string> NpcTags { get; } = new List<string>();
        /// <summary>onTalk: actual target must belong to an active WorldOpportunity instance of this template.</summary>
        public string WorldOpportunityId { get; set; } = string.Empty;
        /// <summary>onInspect: fixed-world object category and optional exact stable instance id.</summary>
        public string WorldObjectKind { get; set; } = string.Empty;
        public string WorldObjectId { get; set; } = string.Empty;
        public int Priority { get; set; }
        public string TopicText { get; set; } = string.Empty;
        public string OnceScope { get; set; } = "global";
        public string EntryStepId { get; set; } = string.Empty;
        public List<ContentEventStepSpec> Steps { get; } = new List<ContentEventStepSpec>();
        public ContentEventStepSpec GetStep(string id)
        {
            if (Steps.Count == 0)
            {
                var legacy = new ContentEventStepSpec { Id = "$legacy", Text = Body,
                    SpeakerRef = string.Equals(Trigger, "onTalk", System.StringComparison.OrdinalIgnoreCase) ? "@target" : "" };
                legacy.Choices.AddRange(Choices);
                return legacy;
            }
            return Steps.Find(s => string.Equals(s.Id, id, System.StringComparison.Ordinal));
        }
        public bool Once { get; set; } = true;
        public List<ContentCondition> Conditions { get; } = new List<ContentCondition>();
        public List<ContentEventChoiceSpec> Choices { get; } = new List<ContentEventChoiceSpec>();
    }

    public sealed class ContentEventStepSpec
    {
        public string Id { get; set; } = string.Empty;
        public string SpeakerRef { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string NextStepId { get; set; } = string.Empty;
        public List<ContentOutcome> Outcomes { get; } = new List<ContentOutcome>();
        public List<ContentEventChoiceSpec> Choices { get; } = new List<ContentEventChoiceSpec>();
    }

    public sealed class ContentEventChoiceSpec
    {
        public string Id { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string NextStepId { get; set; } = string.Empty;
        public string UnavailableMode { get; set; } = "disabled";
        public string RequirementText { get; set; } = string.Empty;
        public List<ContentCondition> Conditions { get; } = new List<ContentCondition>();
        public List<ContentOutcome> Outcomes { get; } = new List<ContentOutcome>();
    }
}
