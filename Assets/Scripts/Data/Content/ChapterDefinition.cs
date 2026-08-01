using System.Collections.Generic;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;

namespace XianXia.Data.Content
{
    public sealed class ChapterDefinition
    {
        public DefinitionId Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string OpeningScenarioId { get; set; } = string.Empty;
        public int PlannedDays { get; set; }
        public List<string> QuestChainIds { get; } = new List<string>();
        public List<string> EventChainIds { get; } = new List<string>();
        public List<ChapterDayBeatDefinition> DayBeats { get; } = new List<ChapterDayBeatDefinition>();
    }

    public sealed class ChapterDayBeatDefinition
    {
        public int DayIndex { get; set; }
        public List<ContentCondition> Conditions { get; } = new List<ContentCondition>();
        public List<string> QuestOfferIds { get; } = new List<string>();
        public List<string> ContentEventIds { get; } = new List<string>();
        public List<string> SetFlags { get; } = new List<string>();
    }
}
