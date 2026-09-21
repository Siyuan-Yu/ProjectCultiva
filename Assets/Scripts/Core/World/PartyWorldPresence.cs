namespace XianXia.Core.World
{
    /// <summary>
    /// 队伍宏观镜头焦点摘要。Normal Continuous Outdoor 使用 AtWorldPosition；InSeparateSpace
    /// 属于 Separate Space。AtSite / AtHex 只用于 legacy LocalMap compatibility，不是现代空间真源。
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
