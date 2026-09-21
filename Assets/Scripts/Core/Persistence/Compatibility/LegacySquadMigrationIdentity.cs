namespace XianXia.Core.Persistence.Compatibility
{
    /// <summary>Stable identities produced only while importing legacy army snapshots.</summary>
    public static class LegacySquadMigrationIdentity
    {
        public static string SquadIdFromLegacyArmyId(string legacyArmyId) =>
            "squad:army:" + (legacyArmyId ?? string.Empty);
    }
}
