using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// 战略世界地点（城镇、宗门、遗迹等）。正常室外位置以 Continuous Surface
    /// 的精确世界坐标与 SiteCore 为准；Hex 字段仅供旧内容和兼容索引使用。
    /// </summary>
    public sealed class WorldSite
    {
        readonly List<HexCoord> _occupiedHexes = new List<HexCoord>(8);
        ReadOnlyCollection<HexCoord> _occupiedHexesView;

        public string SiteId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string SiteType { get; set; } = string.Empty;
        public string OwnerFactionId { get; set; } = string.Empty;
        /// <summary>Legacy bootstrap/migration order；实际行政优先级由 TerritoryClaim.AcquiredOrder 决定。</summary>
        public long ControlEstablishedOrder { get; set; }
        /// <summary>绑定 TerritoryRegion（2J §6.4）。FootprintHexes 与 Region.Hexes 严格分离。</summary>
        public string TerritoryRegionId { get; set; } = string.Empty;
        public string LocalMapId { get; set; } = string.Empty;
        /// <summary>
        /// Outdoor physical migration marker. When true this Site remains a strategic/domain
        /// entity, but normal player movement stays in the main continuous WorldPosition space.
        /// LocalMapId is then authoring/save-migration compatibility only.
        /// </summary>
        public bool UsesContinuousOutdoorSurface { get; set; }

        /// <summary>True only for a Site created during play and therefore persisted as identity.</summary>
        public bool IsRuntimeCreated { get; set; }
        /// <summary>The one control asset that serves as this Site's core.</summary>
        public string CoreAssetId { get; set; } = string.Empty;
        public string CoreSurfaceId { get; set; } = string.Empty;
        public bool HasCoreWorldPosition { get; set; }
        public float CoreWorldX { get; set; }
        public float CoreWorldY { get; set; }
        public int CoreLevel { get; set; }
        public float CoreRangeWidth { get; set; }
        public float CoreRangeHeight { get; set; }
        /// <summary>Inactive retains historical identity but provides no management/control.</summary>
        public bool IsCoreActive { get; set; } = true;
        public bool CoreIsRemovable { get; set; }

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

        public bool HasContinuousCore =>
            !string.IsNullOrWhiteSpace(CoreSurfaceId) && HasCoreWorldPosition &&
            CoreRangeWidth > 0f && CoreRangeHeight > 0f;
    }
}
