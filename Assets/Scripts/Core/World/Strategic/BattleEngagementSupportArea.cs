using System.Collections.Generic;
using System.Text;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Phase 4 接战支援范围：BattleAreaHexes + 与其直接共边相邻的全部 Hex。
    /// 空间资格判断为 SupportAreaHexes.Contains(UnitHex)，禁止中心距离 / 圆形半径。
    /// </summary>
    public sealed class BattleEngagementSupportArea
    {
        readonly List<HexCoord> _battleAreaHexes = new List<HexCoord>(8);
        readonly List<HexCoord> _supportAreaHexes = new List<HexCoord>(16);
        readonly List<HexCoord> _supportRingHexes = new List<HexCoord>(16);
        readonly HashSet<HexCoord> _supportSet = new HashSet<HexCoord>();
        static readonly HashSet<string> LoggedFootprintFallbacks = new HashSet<string>();

        public IReadOnlyList<HexCoord> BattleAreaHexes => _battleAreaHexes;
        public IReadOnlyList<HexCoord> SupportAreaHexes => _supportAreaHexes;
        public IReadOnlyList<HexCoord> SupportRingHexes => _supportRingHexes;
        public HexCoord PresentationAnchorHex { get; private set; }
        public string BattleSiteId { get; private set; } = string.Empty;
        public string BattleSiteResolutionSource { get; private set; } = "None";
        public bool HasValue => _supportSet.Count > 0;

        BattleEngagementSupportArea()
        {
        }

        public bool Contains(HexCoord hex) => _supportSet.Contains(hex);
        public bool ContainsBattleArea(HexCoord hex) => _battleAreaHexes.Contains(hex);
        public bool ContainsSupportRing(HexCoord hex) => _supportRingHexes.Contains(hex);
        public bool ContainsEngagementArea(HexCoord hex) => Contains(hex);

        /// <summary>
        /// 接战创建瞬间冻结 BattleArea / SupportArea；PresentationAnchorHex 供 UI / BattleAnchor。
        /// </summary>
        public static BattleEngagementSupportArea ResolveAndFreeze(
            SimulationWorld world,
            string defenderFormalArmyId)
        {
            var area = new BattleEngagementSupportArea();
            area.Build(world, defenderFormalArmyId);
            return area;
        }

        /// <summary>攻城使用同一正式 WorldSite footprint + 外圈 SupportArea，不从建筑 anchor 重新推导。</summary>
        public static BattleEngagementSupportArea ResolveAndFreezeForWorldSite(
            SimulationWorld world,
            string siteId)
        {
            var area = new BattleEngagementSupportArea();
            if (world?.Strategic?.Sites == null || string.IsNullOrEmpty(siteId) ||
                !world.Strategic.Sites.TryGet(siteId, out var site) || site == null)
                return area;
            area.BattleSiteId = site.SiteId ?? string.Empty;
            area.BattleSiteResolutionSource = "ExplicitWorldSiteSiege";
            foreach (var hex in WorldSiteBattleSpatialPolicy.CollectBattleArea(site))
                area._battleAreaHexes.Add(hex);
            foreach (var hex in WorldSiteBattleSpatialPolicy.CollectSupportRing(site, world.HexWorld))
                area._supportRingHexes.Add(hex);
            area.PresentationAnchorHex = area._battleAreaHexes.Count > 0
                ? area._battleAreaHexes[0]
                : default;
            area.BuildSupportSetFromBattleAndRing();
            return area;
        }

        public static BattleEngagementSupportArea FromFrozenLists(
            IReadOnlyList<HexCoord> battleAreaHexes,
            IReadOnlyList<HexCoord> supportAreaHexes,
            HexCoord presentationAnchorHex,
            string battleSiteId = "",
            string battleSiteResolutionSource = "")
        {
            var area = new BattleEngagementSupportArea();
            if (battleAreaHexes != null)
            {
                for (var i = 0; i < battleAreaHexes.Count; i++)
                {
                    var hex = battleAreaHexes[i];
                    if (!area._battleAreaHexes.Contains(hex))
                        area._battleAreaHexes.Add(hex);
                }
            }

            area.PresentationAnchorHex = presentationAnchorHex;
            area.BattleSiteId = battleSiteId ?? string.Empty;
            area.BattleSiteResolutionSource = string.IsNullOrEmpty(battleSiteResolutionSource)
                ? "None"
                : battleSiteResolutionSource;

            if (area._battleAreaHexes.Count > 0)
            {
                area.BuildSupportFromBattleArea();
                return area;
            }

            if (supportAreaHexes != null && supportAreaHexes.Count > 0)
            {
                for (var i = 0; i < supportAreaHexes.Count; i++)
                {
                    var hex = supportAreaHexes[i];
                    if (area._supportSet.Add(hex))
                        area._supportAreaHexes.Add(hex);
                }
            }

            return area;
        }

        void Build(SimulationWorld world, string defenderFormalArmyId)
        {
            _battleAreaHexes.Clear();
            _supportAreaHexes.Clear();
            _supportRingHexes.Clear();
            _supportSet.Clear();
            PresentationAnchorHex = default;
            BattleSiteId = string.Empty;
            BattleSiteResolutionSource = "None";

            if (world?.Strategic == null || string.IsNullOrEmpty(defenderFormalArmyId))
                return;

            if (!world.Strategic.FormalArmies.TryGet(defenderFormalArmyId, out var defender) ||
                defender == null)
                return;

            var hasDefenderSpatialHex = BattleEngagementSpatialQuery.TryGetCommittedArmyHex(
                world, defender, out var defenderSpatialHex);
            PresentationAnchorHex = defenderSpatialHex;

            if (TryResolveDefenderBattleSite(
                    world,
                    defender,
                    defenderSpatialHex,
                    hasDefenderSpatialHex,
                    out var site,
                    out var resolutionSource))
            {
                BattleSiteId = site.SiteId ?? string.Empty;
                BattleSiteResolutionSource = resolutionSource;
                foreach (var hex in WorldSiteBattleSpatialPolicy.CollectBattleArea(site))
                    _battleAreaHexes.Add(hex);
                foreach (var hex in WorldSiteBattleSpatialPolicy.CollectSupportRing(site, world.HexWorld))
                    _supportRingHexes.Add(hex);
                BuildSupportSetFromBattleAndRing();
                AssertWorldSiteSupportInvariant(site, world.HexWorld);
            }
            else if (hasDefenderSpatialHex)
            {
                _battleAreaHexes.Add(defenderSpatialHex);
                BuildSupportFromBattleArea();
            }
        }

        static bool TryResolveDefenderBattleSite(
            SimulationWorld world,
            FormalArmy defender,
            HexCoord defenderSpatialHex,
            bool hasDefenderSpatialHex,
            out WorldSite site,
            out string resolutionSource)
        {
            site = null;
            resolutionSource = "None";
            if (world?.Strategic?.Sites == null || defender == null)
                return false;

            var motion = defender.WorldMotion;
            if (motion != null &&
                motion.LocationKind == FormalArmyLocationKind.AtWorldSite &&
                !string.IsNullOrEmpty(motion.SiteId) &&
                world.Strategic.Sites.TryGet(motion.SiteId, out site) &&
                site != null)
            {
                resolutionSource = "ExplicitAtWorldSite";
                return true;
            }

            // 仅用于 legacy / 数据修复：移动中或仍在 Site boundary transition 的 Army
            // 不得因偶然经过 footprint 被误判成 WorldSite battle。
            if (!hasDefenderSpatialHex ||
                (motion != null && (motion.IsMoving || motion.IsSiteDeparturePending)) ||
                !world.Strategic.Sites.TryGetAtHex(defenderSpatialHex, out site) ||
                site == null)
                return false;

            resolutionSource = "FootprintOccupancyFallback";
            LogFootprintOccupancyFallbackOnce(defender.ArmyId, defenderSpatialHex, site.SiteId);
            return true;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        static void LogFootprintOccupancyFallbackOnce(
            string armyId,
            HexCoord spatialHex,
            string siteId)
        {
            var key = (armyId ?? string.Empty) + "@" + spatialHex;
            if (!LoggedFootprintFallbacks.Add(key))
                return;
            System.Diagnostics.Debug.WriteLine(
                "[BattleSpatial] FootprintOccupancyFallback army=" + armyId +
                " hex=" + spatialHex + " site=" + siteId);
        }

        void BuildSupportSetFromBattleAndRing()
        {
            _supportAreaHexes.Clear();
            _supportSet.Clear();
            for (var i = 0; i < _battleAreaHexes.Count; i++)
                _supportSet.Add(_battleAreaHexes[i]);
            for (var i = 0; i < _supportRingHexes.Count; i++)
                _supportSet.Add(_supportRingHexes[i]);
            foreach (var hex in _supportSet)
                _supportAreaHexes.Add(hex);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        void AssertWorldSiteSupportInvariant(WorldSite site, HexWorld world)
        {
            if (site == null)
                return;
            foreach (var footprintHex in site.OccupiedHexes)
            {
                System.Diagnostics.Debug.Assert(
                    ContainsBattleArea(footprintHex),
                    "WorldSite battle area lost a footprint hex: " + footprintHex);
                for (var direction = 0; direction < 6; direction++)
                {
                    var neighbor = HexMath.Neighbor(footprintHex, direction);
                    if (site.OccupiesHex(neighbor) || (world != null && !world.Contains(neighbor)))
                        continue;
                    System.Diagnostics.Debug.Assert(
                        ContainsSupportRing(neighbor) && Contains(neighbor),
                        "WorldSite support ring lost an outer neighbor: " + neighbor);
                }
            }
        }

        void BuildSupportFromBattleArea()
        {
            _supportAreaHexes.Clear();
            _supportRingHexes.Clear();
            _supportSet.Clear();

            var battleArea = new List<HexCoord>(_battleAreaHexes);
            for (var i = 0; i < battleArea.Count; i++)
                _supportSet.Add(battleArea[i]);

            for (var i = 0; i < battleArea.Count; i++)
            {
                var battleHex = battleArea[i];
                for (var d = 0; d < 6; d++)
                {
                    var neighbor = HexMath.Neighbor(battleHex, d);
                    if (!_supportSet.Contains(neighbor)) _supportRingHexes.Add(neighbor);
                    _supportSet.Add(neighbor);
                }
            }

            foreach (var hex in _supportSet)
                _supportAreaHexes.Add(hex);
        }

        public void AppendHexList(StringBuilder sb, string label, IReadOnlyList<HexCoord> hexes)
        {
            sb.Append(label).Append('=');
            if (hexes == null || hexes.Count == 0)
            {
                sb.AppendLine("(none)");
                return;
            }

            for (var i = 0; i < hexes.Count; i++)
            {
                if (i > 0)
                    sb.Append(',');
                sb.Append(hexes[i]);
            }

            sb.AppendLine();
        }

        public void AppendConstructionTrace(
            StringBuilder sb,
            HexCoord defenderHex,
            HexCoord initiatorHex,
            bool hasInitiatorHex,
            HexCoord playerHex,
            bool hasPlayerHex)
        {
            sb.AppendLine("=== SupportArea 构造 ===");
            sb.AppendLine("DefenderHex=" + FormatHex(defenderHex));
            if (hasInitiatorHex)
                sb.AppendLine("InitiatorHex=" + FormatHex(initiatorHex));
            else
                sb.AppendLine("InitiatorHex=(none)");

            AppendHexList(sb, "BattleAreaHexes", _battleAreaHexes);
            sb.AppendLine("SupportArea (逐项):");
            for (var i = 0; i < _supportAreaHexes.Count; i++)
            {
                var supportHex = _supportAreaHexes[i];
                TryResolveSupportEntrySource(supportHex, out var sourceBattleHex, out var reason);
                sb.Append("SupportHex ")
                    .Append(FormatHex(supportHex))
                    .Append(" SourceBattleHex=")
                    .Append(FormatHex(sourceBattleHex))
                    .Append(" Reason=")
                    .AppendLine(reason);
            }

            if (!hasPlayerHex)
                return;

            sb.AppendLine("PlayerHex=" + FormatHex(playerHex));
            var inSupport = Contains(playerHex);
            sb.AppendLine("SupportArea.Contains(PlayerHex)=" + inSupport);
            if (!inSupport)
                return;

            TryResolveSupportEntrySource(playerHex, out var playerSource, out var playerReason);
            sb.AppendLine("PlayerSupportSourceBattleHex=" + FormatHex(playerSource));
            sb.AppendLine("PlayerSupportReason=" + playerReason);
        }

        void TryResolveSupportEntrySource(
            HexCoord supportHex,
            out HexCoord sourceBattleHex,
            out string reason)
        {
            sourceBattleHex = default;
            reason = "Unknown";
            for (var i = 0; i < _battleAreaHexes.Count; i++)
            {
                var battleHex = _battleAreaHexes[i];
                if (battleHex.Equals(supportHex))
                {
                    sourceBattleHex = battleHex;
                    reason = "BattleArea";
                    return;
                }
            }

            for (var i = 0; i < _battleAreaHexes.Count; i++)
            {
                var battleHex = _battleAreaHexes[i];
                for (var d = 0; d < 6; d++)
                {
                    if (!HexMath.Neighbor(battleHex, d).Equals(supportHex))
                        continue;

                    sourceBattleHex = battleHex;
                    reason = "DirectNeighbor";
                    return;
                }
            }
        }

        static string FormatHex(HexCoord hex) => "(" + hex.Q + "," + hex.R + ")";
    }
}
