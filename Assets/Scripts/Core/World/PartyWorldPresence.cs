namespace XianXia.Core.World
{
    /// <summary>
    /// 队伍宏观镜头焦点摘要。Normal Continuous Outdoor 使用 AtWorldPosition；InSeparateSpace
    /// 属于 Separate Space。AtSite 只用于 Site-scoped presence，不是普通户外玩家位置真源。
    /// </summary>
    public sealed class PartyWorldPresence
    {
        public PartyWorldPresenceMode Mode { get; set; } = PartyWorldPresenceMode.AtSite;
        public string LocalMapId { get; set; } = string.Empty;
        public string EncounterId { get; set; } = string.Empty;

        /// <summary>当前 legacy LocalMap compatibility 的 WorldSite focus。</summary>
        public string SiteId { get; set; } = string.Empty;

        public void ClearSiteFocus()
        {
            SiteId = string.Empty;
        }
    }
}
