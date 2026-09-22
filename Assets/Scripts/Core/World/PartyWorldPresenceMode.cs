namespace XianXia.Core.World
{
    public enum PartyWorldPresenceMode
    {
        InEncounter = 0,
        /// <summary>
        /// Site-scoped / background Character presence. Normal Continuous PlayerParty does not use AtSite.
        /// </summary>
        AtSite = 3,
        /// <summary>Modern precise Continuous Surface WorldPosition authority.</summary>
        AtWorldPosition = 4,
        /// <summary>
        /// SPACE-01：PlayerParty 处于 active Separate Space（Cave／Interior／Dungeon／SeparateMap）。
        /// Additive only — 不得改动既有枚举 numeric value。
        /// </summary>
        InSeparateSpace = 5,
    }
}
