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
        readonly HashSet<HexCoord> _supportSet = new HashSet<HexCoord>();

        public IReadOnlyList<HexCoord> BattleAreaHexes => _battleAreaHexes;
        public IReadOnlyList<HexCoord> SupportAreaHexes => _supportAreaHexes;
        public HexCoord PresentationAnchorHex { get; private set; }
        public bool HasValue => _supportSet.Count > 0;

        BattleEngagementSupportArea()
        {
        }

        public bool Contains(HexCoord hex) => _supportSet.Contains(hex);

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

            if (supportAreaHexes != null && supportAreaHexes.Count > 0)
            {
                for (var i = 0; i < supportAreaHexes.Count; i++)
                {
                    var hex = supportAreaHexes[i];
                    if (area._supportSet.Add(hex))
                        area._supportAreaHexes.Add(hex);
                }
            }
            else
            {
                area.BuildSupportFromBattleArea();
            }

            area.PresentationAnchorHex = presentationAnchorHex;
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

            if (defender.WorldMotion != null &&
                defender.WorldMotion.LocationKind == FormalArmyLocationKind.AtWorldSite &&
                !string.IsNullOrEmpty(defender.WorldMotion.SiteId) &&
                world.Strategic.Sites.TryGet(defender.WorldMotion.SiteId, out var site) &&
                site != null)
            {
                foreach (var hex in site.EnumerateFootprintHexes())
                {
                    if (!_battleAreaHexes.Contains(hex))
                        _battleAreaHexes.Add(hex);
                }
            }
            else if (!defenderPresenceHex.Equals(default))
            {
                _battleAreaHexes.Add(defenderPresenceHex);
            }

            BuildSupportFromBattleArea();
        }

        void BuildSupportFromBattleArea()
        {
            _supportAreaHexes.Clear();
            _supportSet.Clear();

            for (var i = 0; i < _battleAreaHexes.Count; i++)
                _supportSet.Add(_battleAreaHexes[i]);

            for (var i = 0; i < _battleAreaHexes.Count; i++)
            {
                for (var d = 0; d < 6; d++)
                    _supportSet.Add(HexMath.Neighbor(_battleAreaHexes[i], d));
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
    }
}
