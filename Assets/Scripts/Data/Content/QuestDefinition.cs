using System.Collections.Generic;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;

namespace XianXia.Data.Content
{
    public sealed class QuestDefinition
    {
        public DefinitionId Id { get; set; }
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
