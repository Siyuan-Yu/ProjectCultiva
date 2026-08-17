namespace XianXia.Core.World.Strategic
{
    public sealed class ArmyStack
    {
        public string Id { get; set; } = string.Empty;
        public string FactionId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        /// <summary>AtNode 时所在节点；Traveling 时为出发节点。</summary>
        public string NodeId { get; set; } = string.Empty;
        public string RouteId { get; set; } = string.Empty;
        public string DestNodeId { get; set; } = string.Empty;
        public int RemainingTravelTicks { get; set; }
        public int TravelTotalTicks { get; set; }
        public int MemberCount { get; set; } = 1;
        public int CombatPower { get; set; } = 1;
        public bool IsTraveling => !string.IsNullOrEmpty(RouteId) && RemainingTravelTicks > 0;

        public float TravelProgress
        {
            get
            {
                if (!IsTraveling || TravelTotalTicks <= 0)
                    return 0f;
                var done = TravelTotalTicks - RemainingTravelTicks;
                if (done <= 0)
                    return 0f;
                if (done >= TravelTotalTicks)
                    return 1f;
                return (float)done / TravelTotalTicks;
            }
        }

        public void ClearTravel()
        {
            RouteId = string.Empty;
            DestNodeId = string.Empty;
            RemainingTravelTicks = 0;
            TravelTotalTicks = 0;
        }
    }
}
