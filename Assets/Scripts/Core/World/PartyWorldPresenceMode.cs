namespace XianXia.Core.World
{
    public enum PartyWorldPresenceMode
    {
        InEncounter = 0,
        /// <summary>已废弃（边缘离场）。保留枚举值以免旧存档错位；运行时不应再写入。</summary>
        DepartingLocalMap = 1,
        /// <summary>
        /// Legacy serialized / Outdoor LocalMap compatibility only. Modern Continuous gameplay
        /// does not produce AtHex presence.
        /// </summary>
        AtHex = 2,
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
