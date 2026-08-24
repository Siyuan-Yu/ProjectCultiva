namespace XianXia.Core.World
{
    /// <summary>队伍宏观镜头焦点摘要（Pure Hex）。</summary>
    public sealed class PartyWorldPresence
    {
        public PartyWorldPresenceMode Mode { get; set; } = PartyWorldPresenceMode.AtSite;
        public string LocalMapId { get; set; } = string.Empty;
        public string EncounterId { get; set; } = string.Empty;

        /// <summary>Hex 战略：当前镜头焦点 WorldSite。</summary>
        public string SiteId { get; set; } = string.Empty;

        /// <summary>从大地图进入地点时的控制焦点 FormalArmy（Spawn/Selection；不限制 LocalMap 人口）。</summary>
        public string FocusFormalArmyId { get; set; } = string.Empty;

        public void ClearSiteFocus()
        {
            SiteId = string.Empty;
            FocusFormalArmyId = string.Empty;
        }
    }
}
