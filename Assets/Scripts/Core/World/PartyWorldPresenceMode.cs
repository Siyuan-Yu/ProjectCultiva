namespace XianXia.Core.World
{
    public enum PartyWorldPresenceMode
    {
        AtNode = 0,
        Traveling = 1,
        InEncounter = 2,
        /// <summary>已确认宏观出行，正在 LocalMap 走向地图边缘。</summary>
        DepartingLocalMap = 3
    }
}
