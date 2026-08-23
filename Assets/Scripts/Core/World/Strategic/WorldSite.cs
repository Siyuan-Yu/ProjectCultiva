using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// 战略世界地点（城镇、宗门、遗迹等）。Hex 才是空间基础单位；
    /// <see cref="AnchorHex"/> 仅用于 UI / 镜头 / LocalMap 入口。
    /// </summary>
    public sealed class WorldSite
    {
        readonly List<HexCoord> _occupiedHexes = new List<HexCoord>(8);
        ReadOnlyCollection<HexCoord> _occupiedHexesView;

        public string SiteId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string SiteType { get; set; } = string.Empty;
        public string OwnerFactionId { get; set; } = string.Empty;
        public string LocalMapId { get; set; } = string.Empty;

        /// <summary>内容导入期临时字段；战略空间真源为 Hex，不得作为正式位置。</summary>
        public string LegacyNodeId { get; set; } = string.Empty;

        public HexCoord AnchorHex { get; set; }

        /// <summary>兼容旧字段名。</summary>
        public HexCoord HexCoord
        {
            get => AnchorHex;
            set => AnchorHex = value;
        }

        /// <summary>兼容旧字段名。</summary>
        public string Kind
        {
            get => SiteType;
            set => SiteType = value;
        }

        public IReadOnlyList<HexCoord> OccupiedHexes =>
            _occupiedHexesView ?? (_occupiedHexesView = _occupiedHexes.AsReadOnly());

        public void SetFootprint(IEnumerable<HexCoord> hexes)
        {
            _occupiedHexes.Clear();
            if (hexes == null)
            {
                if (!AnchorHex.Equals(default))
                    _occupiedHexes.Add(AnchorHex);
                return;
            }

            foreach (var hex in hexes)
            {
                if (_occupiedHexes.Contains(hex))
                    continue;
                _occupiedHexes.Add(hex);
            }

            if (!AnchorHex.Equals(default) && !_occupiedHexes.Contains(AnchorHex))
                _occupiedHexes.Insert(0, AnchorHex);
        }

        public IEnumerable<HexCoord> EnumerateFootprintHexes()
        {
            if (_occupiedHexes.Count > 0)
            {
                for (var i = 0; i < _occupiedHexes.Count; i++)
                    yield return _occupiedHexes[i];
                yield break;
            }

            if (!AnchorHex.Equals(default))
                yield return AnchorHex;
        }

        public bool OccupiesHex(HexCoord coord)
        {
            if (_occupiedHexes.Count > 0)
            {
                for (var i = 0; i < _occupiedHexes.Count; i++)
                {
                    if (_occupiedHexes[i] == coord)
                        return true;
                }

                return false;
            }

            return AnchorHex == coord;
        }
    }
}
