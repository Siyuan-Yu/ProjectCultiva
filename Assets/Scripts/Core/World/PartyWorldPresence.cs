namespace XianXia.Core.World
{
    /// <summary>队伍在宏观 WorldGraph 上的位置（[113]）。</summary>
    public sealed class PartyWorldPresence
    {
        public PartyWorldPresenceMode Mode { get; set; } = PartyWorldPresenceMode.AtNode;
        public string NodeId { get; set; } = string.Empty;
        public string RouteId { get; set; } = string.Empty;
        /// <summary>Traveling 时剩余旅行 tick（与 route.travelCost 对齐）。</summary>
        public int RemainingTravelTicks { get; set; }
        public string LocalMapId { get; set; } = string.Empty;
        public string EncounterId { get; set; } = string.Empty;

        public void ClearTravel()
        {
            Mode = PartyWorldPresenceMode.AtNode;
            RouteId = string.Empty;
            RemainingTravelTicks = 0;
        }
    }
}
