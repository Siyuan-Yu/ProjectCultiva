using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Phase 5D: WorldSite 战略 Transit 属性。默认 None（普通地点）；
    /// 只有明确配置的关隘 / 渡口 / 山口等才是 Gateway。禁止按名字猜。
    /// </summary>
    public enum WorldSiteTransitMode
    {
        None = 0,
        Gateway = 1,
    }

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

        /// <summary>Phase 5D: 战略 Transit 属性（None / Gateway）。默认 None。</summary>
        public WorldSiteTransitMode TransitMode { get; set; } = WorldSiteTransitMode.None;

        public HexCoord AnchorHex { get; set; }

        /// <summary>
        /// Character 位于该 Site LocalMap 时的 HexWorld 位置代理（兼容字段；必须与 <see cref="AnchorHex"/> 相同）。
        /// Authoring／Content 固定；Runtime 不随 LocalPosition 漂移。
        /// </summary>
        public HexCoord PresenceHex { get; set; }

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
                EnsurePresenceHexValid();
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

            EnsurePresenceHexValid();
        }

        /// <summary>
        /// 确保 PresenceHex 在 Footprint 内，并强制 PresenceHex == AnchorHex（兼容 invariant）。
        /// </summary>
        public void EnsurePresenceHexValid()
        {
            if (!OccupiesHex(PresenceHex) && OccupiesHex(AnchorHex))
            {
                PresenceHex = AnchorHex;
            }
            else if (!OccupiesHex(PresenceHex))
            {
                foreach (var hex in EnumerateFootprintHexes())
                {
                    PresenceHex = hex;
                    break;
                }
            }

            if (OccupiesHex(AnchorHex))
                PresenceHex = AnchorHex;
        }

        /// <summary>PresenceHex 是否与 AnchorHex 不一致（加载旧 Content 时可用来打 Development warning）。</summary>
        public bool HasPresenceAnchorMismatch(HexCoord loadedPresence) =>
            !loadedPresence.Equals(default) && loadedPresence != AnchorHex;

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
