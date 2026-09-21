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
        readonly List<HexCoord> _legacyOccupiedHexes = new List<HexCoord>(8);
        ReadOnlyCollection<HexCoord> _legacyOccupiedHexesView;

        public string SiteId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string SiteType { get; set; } = string.Empty;
        public string OwnerFactionId { get; set; } = string.Empty;
        /// <summary>Legacy bootstrap/migration order；实际行政优先级由 TerritoryClaim.AcquiredOrder 决定。</summary>
        public long ControlEstablishedOrder { get; set; }
        /// <summary>Legacy content/migration key only. Never administrative authority.</summary>
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

        public HexCoord LegacyAnchorHex { get; set; }

        /// <summary>
        /// Character 位于该 Site LocalMap 时的 HexWorld 位置代理（兼容字段；必须与 <see cref="LegacyAnchorHex"/> 相同）。
        /// Authoring／Content 固定；Runtime 不随 LocalPosition 漂移。
        /// </summary>
        public HexCoord LegacyPresenceHex { get; set; }

        public IReadOnlyList<HexCoord> LegacyOccupiedHexes =>
            _legacyOccupiedHexesView ?? (_legacyOccupiedHexesView = _legacyOccupiedHexes.AsReadOnly());

        public void SetLegacyHexFootprint(IEnumerable<HexCoord> hexes)
        {
            _legacyOccupiedHexes.Clear();
            if (hexes == null)
            {
                if (!LegacyAnchorHex.Equals(default))
                    _legacyOccupiedHexes.Add(LegacyAnchorHex);
                EnsureLegacyPresenceHexValid();
                return;
            }

            foreach (var hex in hexes)
            {
                if (_legacyOccupiedHexes.Contains(hex))
                    continue;
                _legacyOccupiedHexes.Add(hex);
            }

            if (!LegacyAnchorHex.Equals(default) && !_legacyOccupiedHexes.Contains(LegacyAnchorHex))
                _legacyOccupiedHexes.Insert(0, LegacyAnchorHex);

            EnsureLegacyPresenceHexValid();
        }

        /// <summary>
        /// 确保 LegacyPresenceHex 在 Hex footprint 内，并强制
        /// LegacyPresenceHex == LegacyAnchorHex（兼容 invariant）。
        /// </summary>
        public void EnsureLegacyPresenceHexValid()
        {
            if (!OccupiesLegacyHex(LegacyPresenceHex) && OccupiesLegacyHex(LegacyAnchorHex))
            {
                LegacyPresenceHex = LegacyAnchorHex;
            }
            else if (!OccupiesLegacyHex(LegacyPresenceHex))
            {
                foreach (var hex in EnumerateLegacyFootprintHexes())
                {
                    LegacyPresenceHex = hex;
                    break;
                }
            }

            if (OccupiesLegacyHex(LegacyAnchorHex))
                LegacyPresenceHex = LegacyAnchorHex;
        }

        /// <summary>LegacyPresenceHex 是否与 LegacyAnchorHex 不一致（加载旧 Content 时可用来打 Development warning）。</summary>
        public bool HasLegacyPresenceAnchorMismatch(HexCoord loadedPresence) =>
            !loadedPresence.Equals(default) && loadedPresence != LegacyAnchorHex;

        public IEnumerable<HexCoord> EnumerateLegacyFootprintHexes()
        {
            if (_legacyOccupiedHexes.Count > 0)
            {
                for (var i = 0; i < _legacyOccupiedHexes.Count; i++)
                    yield return _legacyOccupiedHexes[i];
                yield break;
            }

            if (!LegacyAnchorHex.Equals(default))
                yield return LegacyAnchorHex;
        }

        public bool OccupiesLegacyHex(HexCoord coord)
        {
            if (_legacyOccupiedHexes.Count > 0)
            {
                for (var i = 0; i < _legacyOccupiedHexes.Count; i++)
                {
                    if (_legacyOccupiedHexes[i] == coord)
                        return true;
                }

                return false;
            }

            return LegacyAnchorHex == coord;
        }

        public bool HasContinuousCore =>
            !string.IsNullOrWhiteSpace(CoreSurfaceId) && HasCoreWorldPosition &&
            CoreRangeWidth > 0f && CoreRangeHeight > 0f;
    }
}
