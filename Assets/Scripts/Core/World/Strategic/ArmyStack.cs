namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Prototype ArmyStack 兼容视图（Phase C 收敛中）。
    /// Living roster / CombatPower 真源：链接 FormalArmyId 时由 ArmyStackAdapter 派生。
    /// </summary>
    public sealed class ArmyStack
    {
        public string Id { get; set; } = string.Empty;
        /// <summary>Phase C+：链接 FormalArmy 时 MemberCount/CombatPower 从 FormalArmy 派生。</summary>
        public string FormalArmyId { get; set; } = string.Empty;
        public string FactionId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        /// <summary>AtNode 时所在节点；Traveling 时为出发节点。</summary>
        public string NodeId { get; set; } = string.Empty;
        public string RouteId { get; set; } = string.Empty;
        public string DestNodeId { get; set; } = string.Empty;
        public int RemainingTravelTicks { get; set; }
        public int TravelTotalTicks { get; set; }

        /// <summary>Legacy 匿名栈 living count（无 FormalArmyId 时）。</summary>
        internal int LegacyMemberCount { get; set; } = 1;
        internal int LegacyCombatPower { get; set; } = 1;
        internal int DerivedMemberCount { get; set; } = 1;
        internal int DerivedCombatPower { get; set; } = 1;

        /// <summary>展示／兼容：FormalArmy 链接时用 DerivedMemberCount。</summary>
        public int MemberCount
        {
            get => string.IsNullOrEmpty(FormalArmyId) ? LegacyMemberCount : DerivedMemberCount;
            set
            {
                if (!string.IsNullOrEmpty(FormalArmyId))
                    return;
                LegacyMemberCount = value;
            }
        }

        public int CombatPower
        {
            get => string.IsNullOrEmpty(FormalArmyId) ? LegacyCombatPower : DerivedCombatPower;
            set
            {
                if (!string.IsNullOrEmpty(FormalArmyId))
                    return;
                LegacyCombatPower = value;
            }
        }

        /// <summary>自动战未处决／手动清场后仍弥留的人数（大地图残留用）。</summary>
        public int IncapacitatedMemberCount { get; set; }
        /// <summary>自动战处决／清场后仍留尸体的人数（大地图残留用）。</summary>
        public int CorpseMemberCount { get; set; }
        /// <summary>驻留 Route 上的进度 0..1（-1 表示不在路径锚点）。</summary>
        public float RouteAnchorProgress { get; set; } = -1f;
        /// <summary>接战点残留：场上有弥留／尸体，大地图可再攻击进入。</summary>
        public bool IsBattlefieldRemnant { get; set; }

        public bool HasFormalArmyLink => !string.IsNullOrEmpty(FormalArmyId);

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
