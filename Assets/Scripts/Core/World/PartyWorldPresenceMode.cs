namespace XianXia.Core.World
{
    public enum PartyWorldPresenceMode
    {
        AtNode = 0,
        Traveling = 1,
        InEncounter = 2,
        /// <summary>已废弃（边缘离场）。保留枚举值以免旧存档错位；运行时不应再写入。</summary>
        DepartingLocalMap = 3,
        /// <summary>停驻在宏观道路某进度（金丹前仅能在路上）。</summary>
        RouteAnchored = 4,
    }
}
