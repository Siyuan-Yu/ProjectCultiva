using XianXia.Core.Entities;

namespace XianXia.Core.Exploration
{
    /// <summary>Current abstract world location (session; not Snapshot v1).</summary>
    public sealed class EntityLocationComponent : IComponent
    {
        public string LocationId { get; set; } = string.Empty;

        public bool HasLocation => !string.IsNullOrEmpty(LocationId);

        /// <summary>刷怪区等：覆盖地点中心，用表现坐标落点。</summary>
        public bool HasPresentationOverride { get; set; }

        public float PresentationOverrideX { get; set; }

        public float PresentationOverrideZ { get; set; }

        public void SetPresentationOverride(float x, float z)
        {
            HasPresentationOverride = true;
            PresentationOverrideX = x;
            PresentationOverrideZ = z;
        }

        /// <summary>尸体腐烂／离场：清掉本地图占位与表现坐标。</summary>
        public void ClearPresence()
        {
            LocationId = string.Empty;
            HasPresentationOverride = false;
            PresentationOverrideX = 0f;
            PresentationOverrideZ = 0f;
        }
    }
}
