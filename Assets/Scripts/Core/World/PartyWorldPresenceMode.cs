namespace XianXia.Core.World
{
    public enum PartyWorldPresenceMode
    {
        InEncounter = 0,
        /// <summary>已废弃（边缘离场）。保留枚举值以免旧存档错位；运行时不应再写入。</summary>
        DepartingLocalMap = 1,
        /// <summary>战略 Hex 钉点（战后 Downed / Visible Corpse Residual 专用）。</summary>
        AtHex = 2,
        /// <summary>Pure Hex：角色战略位置真源为 WorldSite。</summary>
        AtSite = 3,
    }
}
