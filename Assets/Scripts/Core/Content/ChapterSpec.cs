using System.Collections.Generic;

namespace XianXia.Core.Content
{
    /// <summary>Chapter production framework template (session; not Snapshot).</summary>
    public sealed class ChapterSpec
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string OpeningScenarioId { get; set; } = string.Empty;
        public int PlannedDays { get; set; }
        public List<string> QuestChainIds { get; } = new List<string>();
        public List<string> EventChainIds { get; } = new List<string>();
        public List<ChapterDayBeatSpec> DayBeats { get; } = new List<ChapterDayBeatSpec>();
    }

    public sealed class ChapterDayBeatSpec
    {
        /// <summary>0-based day index relative to chapter activation day.</summary>
        public int DayIndex { get; set; }
        public List<ContentCondition> Conditions { get; } = new List<ContentCondition>();
        public List<string> QuestOfferIds { get; } = new List<string>();
        public List<string> ContentEventIds { get; } = new List<string>();
        public List<string> SetFlags { get; } = new List<string>();
    }
}
