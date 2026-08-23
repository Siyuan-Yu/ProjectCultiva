using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>单场残留战场身份：Hex 锚点 + 敌栈 Id（独立于 AttackerArmyId）。</summary>
    public sealed class LingeringBattlefieldRecord
    {
        public HexCoord Hex { get; set; }
        public string EnemyStackId { get; set; } = string.Empty;
    }
}
