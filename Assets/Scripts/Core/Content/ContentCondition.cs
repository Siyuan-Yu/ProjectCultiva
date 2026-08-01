namespace XianXia.Core.Content
{
    /// <summary>Declarative condition kinds for quests／events／location entry.</summary>
    public sealed class ContentCondition
    {
        /// <summary>
        /// atLocation | hasFlag | missingFlag | realmAtLeast | knowsSite |
        /// stockAtLeast | questActive | questCompleted | exploredLocation | hasManual
        /// </summary>
        public string Kind { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public int Amount { get; set; }
        public string Realm { get; set; } = string.Empty;
    }
}
