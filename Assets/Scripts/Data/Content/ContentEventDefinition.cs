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
        public bool Once { get; set; } = true;
        public List<ContentCondition> Conditions { get; } = new List<ContentCondition>();
        public List<ContentEventChoiceDefinition> Choices { get; } = new List<ContentEventChoiceDefinition>();
    }

    public sealed class ContentEventChoiceDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public List<ContentCondition> Conditions { get; } = new List<ContentCondition>();
        public List<ContentOutcome> Outcomes { get; } = new List<ContentOutcome>();
    }
}
