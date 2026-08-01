using System.Collections.Generic;

namespace XianXia.Core.Content
{
    /// <summary>Runtime quest template mapped from Content (rules interpret conditions).</summary>
    public sealed class QuestSpec
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool AutoOffer { get; set; }
        public List<ContentCondition> OfferConditions { get; } = new List<ContentCondition>();
        public List<ContentCondition> CompleteConditions { get; } = new List<ContentCondition>();
        public List<ContentCondition> FailConditions { get; } = new List<ContentCondition>();
        public List<ContentOutcome> Rewards { get; } = new List<ContentOutcome>();
        public List<ContentOutcome> FailResults { get; } = new List<ContentOutcome>();
    }
}
