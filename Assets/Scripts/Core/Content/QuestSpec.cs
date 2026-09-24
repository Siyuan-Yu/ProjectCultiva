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
        /// <summary>fixed | characterCommission。</summary>
        public string RuntimeMode { get; set; } = "fixed";
        /// <summary>journal | interaction。人物委托必须为 interaction。</summary>
        public string AcceptanceMode { get; set; } = "journal";
        /// <summary>为 true 时玩家可从任务 UI 放弃（回到 Inactive）。</summary>
        public bool Abandonable { get; set; }
        /// <summary>接取后有效游戏天数；0 = 无时限。超时自动 Failed 并应用 failResults。</summary>
        public int DeadlineDays { get; set; }
        public List<ContentCondition> OfferConditions { get; } = new List<ContentCondition>();
        public List<ContentCondition> CompleteConditions { get; } = new List<ContentCondition>();
        public List<ContentCondition> FailConditions { get; } = new List<ContentCondition>();
        public List<ContentOutcome> Rewards { get; } = new List<ContentOutcome>();
        public List<ContentOutcome> FailResults { get; } = new List<ContentOutcome>();
        public List<QuestDeliveryRequirement> DeliveryRequirements { get; } =
            new List<QuestDeliveryRequirement>();

        public bool IsCharacterCommission =>
            string.Equals(RuntimeMode, "characterCommission", System.StringComparison.OrdinalIgnoreCase);
    }

    public sealed class QuestDeliveryRequirement
    {
        public string ItemId { get; set; } = string.Empty;
        public int Amount { get; set; } = 1;
    }
}
