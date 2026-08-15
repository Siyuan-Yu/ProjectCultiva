namespace XianXia.Core.Exploration
{
    /// <summary>
    /// 当前加载的 LocalMap（session-only；对齐 [113] 进出图竖切，不进 Snapshot v1）。
    /// </summary>
    public sealed class LocalMapSession
    {
        /// <summary>当前 Host 应显示的 mapLayout id。</summary>
        public string ActiveMapLayoutId { get; set; } = string.Empty;

        /// <summary>进入洞府／秘境前记住的地表图；离开时还原。</summary>
        public string OverworldMapLayoutId { get; set; } = string.Empty;

        /// <summary>离开时把队伍送回的地点（通常是洞口）。</summary>
        public string ReturnLocationId { get; set; } = string.Empty;

        public bool IsInInterior =>
            !string.IsNullOrEmpty(ActiveMapLayoutId) &&
            !string.IsNullOrEmpty(OverworldMapLayoutId) &&
            !string.Equals(ActiveMapLayoutId, OverworldMapLayoutId, System.StringComparison.Ordinal);

        public void EnsureOverworld(string mapLayoutId)
        {
            if (string.IsNullOrWhiteSpace(mapLayoutId))
                return;
            if (string.IsNullOrEmpty(OverworldMapLayoutId))
                OverworldMapLayoutId = mapLayoutId;
            if (string.IsNullOrEmpty(ActiveMapLayoutId))
                ActiveMapLayoutId = mapLayoutId;
        }

        public void Clear()
        {
            ActiveMapLayoutId = string.Empty;
            OverworldMapLayoutId = string.Empty;
            ReturnLocationId = string.Empty;
        }
    }
}
