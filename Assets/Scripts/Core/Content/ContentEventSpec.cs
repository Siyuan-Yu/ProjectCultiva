using System.Collections.Generic;

namespace XianXia.Core.Content
{
    public sealed class ContentEventSpec
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        /// <summary>onExplore | onArrive | onQuestCompleted | manual</summary>
        public string Trigger { get; set; } = string.Empty;
        public string LocationId { get; set; } = string.Empty;
        public string QuestId { get; set; } = string.Empty;
        public bool Once { get; set; } = true;
        public List<ContentCondition> Conditions { get; } = new List<ContentCondition>();
        public List<ContentEventChoiceSpec> Choices { get; } = new List<ContentEventChoiceSpec>();
    }

    public sealed class ContentEventChoiceSpec
    {
        public string Id { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public List<ContentCondition> Conditions { get; } = new List<ContentCondition>();
        public List<ContentOutcome> Outcomes { get; } = new List<ContentOutcome>();
    }
}
