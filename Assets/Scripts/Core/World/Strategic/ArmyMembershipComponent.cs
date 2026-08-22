using XianXia.Core.Entities;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Character → Army 反向索引。必须由 <see cref="ArmyService"/> 维护；
    /// 不得作为成员关系的独立真源。
    /// </summary>
    public sealed class ArmyMembershipComponent : IComponent
    {
        public string ArmyId { get; private set; } = string.Empty;

        public bool IsInArmy => !string.IsNullOrEmpty(ArmyId);

        internal void SetArmyId(string armyId) => ArmyId = armyId ?? string.Empty;

        internal void ClearArmyId() => ArmyId = string.Empty;
    }
}
