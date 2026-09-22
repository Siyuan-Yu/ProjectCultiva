using System;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// 战略世界地点（城镇、宗门、遗迹等）。室外位置以 Continuous Surface
    /// 的精确世界坐标与 SiteCore 为准。
    /// </summary>
    public sealed class WorldSite
    {
        public string SiteId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string SiteType { get; set; } = string.Empty;
        public string OwnerFactionId { get; set; } = string.Empty;
        /// <summary>Legacy bootstrap/migration order；实际行政优先级由 TerritoryClaim.AcquiredOrder 决定。</summary>
        public long ControlEstablishedOrder { get; set; }
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

        public bool HasContinuousCore =>
            !string.IsNullOrWhiteSpace(CoreSurfaceId) && HasCoreWorldPosition &&
            CoreRangeWidth > 0f && CoreRangeHeight > 0f;
    }
}
