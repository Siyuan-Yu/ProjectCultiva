namespace XianXia.Core.World
{
    public enum PartyWorldPresenceMode
    {
        AtNode = 0,
        Traveling = 1,
        InEncounter = 2,
        /// <summary>已确认宏观出行，正在 LocalMap 走向地图边缘。</summary>
        DepartingLocalMap = 3,
        /// <summary>停驻在宏观道路某进度（金丹前仅能在路上）。</summary>
        RouteAnchored = 4,
    }
}
