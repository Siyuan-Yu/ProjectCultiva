using XianXia.Core.Entities;

namespace XianXia.Core.Settlement
{
    /// <summary>Per-entity settlement work assignment (session state; not in Snapshot).</summary>
    public sealed class WorkAssignmentComponent : IComponent
    {
        public string SettlementId { get; set; } = string.Empty;

        public WorkRoleKind Role { get; set; } = WorkRoleKind.None;

        public bool IsAssigned =>
            !string.IsNullOrEmpty(SettlementId) && Role != WorkRoleKind.None;

        public void Assign(string settlementId, WorkRoleKind role)
        {
            SettlementId = settlementId ?? string.Empty;
            Role = string.IsNullOrEmpty(SettlementId) ? WorkRoleKind.None : role;
        }

        public void Clear()
        {
            SettlementId = string.Empty;
            Role = WorkRoleKind.None;
        }
    }
}
