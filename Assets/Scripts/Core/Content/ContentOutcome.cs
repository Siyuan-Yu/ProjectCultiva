namespace XianXia.Core.Content
{
    /// <summary>Declarative outcome for quest rewards／event choices／fail results.</summary>
    public sealed class ContentOutcome
    {
        /// <summary>
        /// setFlag | clearFlag | addStock | startQuest | completeQuestHint |
        /// relationDelta | grantProgress | discoverSite
        /// </summary>
        public string Kind { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public int Amount { get; set; }
        public string FromDefinitionId { get; set; } = string.Empty;
        /// <summary>Legacy single target; use <see cref="ToDefinitionIds"/> for multiple.</summary>
        public string ToDefinitionId { get; set; } = string.Empty;
        /// <summary>
        /// relationDelta targets. Supports character definition ids or <c>@party</c> (all controllable characters).
        /// </summary>
        public System.Collections.Generic.List<string> ToDefinitionIds { get; } =
            new System.Collections.Generic.List<string>();
    }
}
