using System.Collections.Generic;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;

namespace XianXia.Data.Content
{
    public sealed class ContentEventDefinition
    {
        public DefinitionId Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string Trigger { get; set; } = string.Empty;
        public string LocationId { get; set; } = string.Empty;
        public string QuestId { get; set; } = string.Empty;
        public string NpcDefinitionId { get; set; } = string.Empty;
        public List<string> NpcTags { get; } = new List<string>();
        public string WorldOpportunityId { get; set; } = string.Empty;
        public string WorldObjectKind { get; set; } = string.Empty;
        public string WorldObjectId { get; set; } = string.Empty;
        public int Priority { get; set; }
        public string TopicText { get; set; } = string.Empty;
        public string OnceScope { get; set; } = "global";
        public string EntryStepId { get; set; } = string.Empty;
        public List<ContentEventStepSpec> Steps { get; } = new List<ContentEventStepSpec>();
        public bool Once { get; set; } = true;
        public List<ContentCondition> Conditions { get; } = new List<ContentCondition>();
        public List<ContentEventChoiceDefinition> Choices { get; } = new List<ContentEventChoiceDefinition>();
    }

    public sealed class ContentEventChoiceDefinition
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
