using System.Collections.Generic;
using System.Collections.ObjectModel;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// 政治辖区（2J §6）：绑定一个 Primary WorldSite 的 Hex 集合。
    /// Region identity 与 ControlFactionId 分离 —— 无主 Site（Owner=""）仍有 Region（Control=""）。
    /// Runtime 真源是 <see cref="Hexes"/>（内容固化），<b>绝不</b>运行时按 radius 重算。
    /// </summary>
    public sealed class TerritoryRegion
    {
        readonly List<HexCoord> _hexes = new List<HexCoord>(24);
        ReadOnlyCollection<HexCoord> _hexesView;

        public string RegionId { get; set; } = string.Empty;
        public string PrimaryWorldSiteId { get; set; } = string.Empty;
        public string ControlFactionId { get; set; } = string.Empty;

        public IReadOnlyList<HexCoord> Hexes =>
            _hexesView ?? (_hexesView = _hexes.AsReadOnly());

        public void SetHexes(IEnumerable<HexCoord> hexes)
        {
            _hexes.Clear();
            if (hexes == null)
                return;
            foreach (var hex in hexes)
            {
                if (_hexes.Contains(hex))
                    continue;
                _hexes.Add(hex);
            }
        }

        public bool Contains(HexCoord hex) => _hexes.Contains(hex);

        public int HexCount => _hexes.Count;
    }
}
