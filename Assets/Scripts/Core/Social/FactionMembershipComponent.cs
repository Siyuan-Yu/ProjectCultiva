using XianXia.Core.Entities;

namespace XianXia.Core.Social
{
    /// <summary>
    /// VS0.5 Phase D: thin current faction membership. Leaving does not clear RelationshipLedger.
    /// </summary>
    public sealed class FactionMembershipComponent : IComponent
    {
        public string FactionId { get; set; } = string.Empty;

        public FactionRoleKind Role { get; set; } = FactionRoleKind.None;

        public bool IsAffiliated =>
            !string.IsNullOrEmpty(FactionId) && Role != FactionRoleKind.None;

        public void ClearMembership()
        {
            FactionId = string.Empty;
            Role = FactionRoleKind.None;
        }

        public void Assign(string factionId, FactionRoleKind role)
        {
            FactionId = factionId ?? string.Empty;
            Role = string.IsNullOrEmpty(FactionId) ? FactionRoleKind.None : role;
        }
    }
}
