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
        public string QuestKind { get; set; } = "general";
        public bool IsSecretRealm => QuestKind == "secretRealm";
        public string RuntimeMode { get; set; } = "fixed";
        public string AcceptanceMode { get; set; } = "journal";
        public bool Abandonable { get; set; }
        /// <summary>接取后有效游戏天数；0 = 无时限。</summary>
        public int DeadlineDays { get; set; }
        public List<ContentCondition> OfferConditions { get; } = new List<ContentCondition>();
        public List<ContentCondition> CompleteConditions { get; } = new List<ContentCondition>();
        public List<ContentCondition> FailConditions { get; } = new List<ContentCondition>();
        public List<ContentOutcome> Rewards { get; } = new List<ContentOutcome>();
        public List<ContentOutcome> FailResults { get; } = new List<ContentOutcome>();
        public List<QuestDeliveryRequirement> DeliveryRequirements { get; } = new List<QuestDeliveryRequirement>();
    }
}
