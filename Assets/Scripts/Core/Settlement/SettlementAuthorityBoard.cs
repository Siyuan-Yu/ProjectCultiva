using System.Collections.Generic;

namespace XianXia.Core.Settlement
{
    /// <summary>Named privileges unlocked by capturing control structures (content-driven).</summary>
    public static class SettlementPrivilegeIds
    {
        public const string ManageHousing = "manageHousing";
        public const string ManageSchedules = "manageSchedules";
    }

    /// <summary>Session authority after capturing settlement cores. Not Snapshot v1.</summary>
    public sealed class SettlementAuthorityBoard
    {
        readonly HashSet<string> _privileges = new HashSet<string>(System.StringComparer.Ordinal);

        public IReadOnlyCollection<string> All => _privileges;

        public bool Has(string privilege) =>
            !string.IsNullOrEmpty(privilege) && _privileges.Contains(privilege);

        public bool CanManageHousing => Has(SettlementPrivilegeIds.ManageHousing);

        public bool CanManageSchedules => Has(SettlementPrivilegeIds.ManageSchedules);

        public void Grant(string privilege)
        {
            if (!string.IsNullOrWhiteSpace(privilege))
                _privileges.Add(privilege.Trim());
        }

        public void GrantAll(IEnumerable<string> privileges)
        {
            if (privileges == null)
                return;
            foreach (var p in privileges)
                Grant(p);
        }

        public void Clear() => _privileges.Clear();
    }
}
