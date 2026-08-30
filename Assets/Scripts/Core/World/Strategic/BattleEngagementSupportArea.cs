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

        public IReadOnlyList<HexCoord> BattleAreaHexes => _battleAreaHexes;
        public IReadOnlyList<HexCoord> SupportAreaHexes => _supportAreaHexes;
        public IReadOnlyList<HexCoord> SupportRingHexes => _supportRingHexes;
        public HexCoord PresentationAnchorHex { get; private set; }
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

        public static BattleEngagementSupportArea FromFrozenLists(
            IReadOnlyList<HexCoord> battleAreaHexes,
            IReadOnlyList<HexCoord> supportAreaHexes,
            HexCoord presentationAnchorHex)
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
            _supportSet.Clear();
            PresentationAnchorHex = default;

            if (world?.Strategic == null || string.IsNullOrEmpty(defenderFormalArmyId))
                return;

            if (!world.Strategic.FormalArmies.TryGet(defenderFormalArmyId, out var defender) ||
                defender == null)
                return;

            BattleEngagementHexDistance.TryResolveFormalArmyPresenceHex(
                world, defender, out var defenderPresenceHex);
            PresentationAnchorHex = defenderPresenceHex;

            WorldSite site;
            if (ShouldUseSiteFootprintAsBattleArea(world, defender, defenderPresenceHex, out site))
            {
                foreach (var hex in WorldSiteBattleSpatialPolicy.CollectBattleArea(site))
                    _battleAreaHexes.Add(hex);
                foreach (var hex in WorldSiteBattleSpatialPolicy.CollectSupportRing(site, world.HexWorld))
                    _supportRingHexes.Add(hex);
            }
            else if (!defenderPresenceHex.Equals(default))
            {
                _battleAreaHexes.Add(defenderPresenceHex);
            }

            BuildSupportFromBattleArea();
            if (site != null)
            {
                _supportRingHexes.Clear();
                foreach (var hex in WorldSiteBattleSpatialPolicy.CollectSupportRing(site, world.HexWorld))
                    _supportRingHexes.Add(hex);
                _supportSet.Clear();
                for (var i = 0; i < _battleAreaHexes.Count; i++) _supportSet.Add(_battleAreaHexes[i]);
                for (var i = 0; i < _supportRingHexes.Count; i++) _supportSet.Add(_supportRingHexes[i]);
                _supportAreaHexes.Clear();
                foreach (var hex in _supportSet) _supportAreaHexes.Add(hex);
            }
        }

        static bool ShouldUseSiteFootprintAsBattleArea(
            SimulationWorld world,
            FormalArmy defender,
            HexCoord defenderPresenceHex,
            out WorldSite site)
        {
            site = null;
            if (defender?.WorldMotion == null ||
                defender.WorldMotion.LocationKind != FormalArmyLocationKind.AtWorldSite ||
                string.IsNullOrEmpty(defender.WorldMotion.SiteId) ||
                world?.Strategic?.Sites == null ||
                !world.Strategic.Sites.TryGet(defender.WorldMotion.SiteId, out site) ||
                site == null)
                return false;

            return true;
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
