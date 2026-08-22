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
        /// <summary>自动战未处决／手动清场后仍弥留的人数（大地图残留用）。</summary>
        public int IncapacitatedMemberCount { get; set; }
        /// <summary>自动战处决／清场后仍留尸体的人数（大地图残留用）。</summary>
        public int CorpseMemberCount { get; set; }
        /// <summary>驻留 Route 上的进度 0..1（-1 表示不在路径锚点）。</summary>
        public float RouteAnchorProgress { get; set; } = -1f;
        /// <summary>接战点残留：场上有弥留／尸体，大地图可再攻击进入。</summary>
        public bool IsBattlefieldRemnant { get; set; }

        public bool HasIncapacitatedRemnant =>
            IsBattlefieldRemnant && IncapacitatedMemberCount > 0;
        public bool HasCorpseRemnant =>
            IsBattlefieldRemnant && CorpseMemberCount > 0;
        /// <summary>弥留或尸体残留（自动战／清场后仍可再进）。</summary>
        public bool HasDownedRemnant => HasIncapacitatedRemnant || HasCorpseRemnant;
        public bool IsTraveling => !string.IsNullOrEmpty(RouteId) && RemainingTravelTicks > 0;
        public bool IsRouteAnchored =>
            !string.IsNullOrEmpty(RouteId) && RouteAnchorProgress >= 0f && !IsTraveling;
        public bool IsRoutePositioned => IsTraveling || IsRouteAnchored;

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

        public float GetRouteDisplayProgress()
        {
            if (IsTraveling)
                return TravelProgress;
            if (IsRouteAnchored)
                return RouteAnchorProgress;
            return 0f;
        }

        public void ClearTravel()
        {
            if (!IsRouteAnchored)
                RouteId = string.Empty;
            DestNodeId = string.Empty;
            RemainingTravelTicks = 0;
            TravelTotalTicks = 0;
        }
    }
}
