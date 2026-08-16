using XianXia.Core.Entities;

namespace XianXia.Core.Combat
{
    /// <summary>
    /// 斗气纱衣姿态：筑基起可召唤；开启后普攻变为远程灵力外放（伤害／攻速不变）。
    /// 会话态，不进 Snapshot。
    /// </summary>
    public sealed class SpiritVeilComponent : IComponent
    {
        public bool IsActive { get; set; }
    }
}
