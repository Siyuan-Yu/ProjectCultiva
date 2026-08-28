namespace XianXia.Core.World.Strategic
{
    /// <summary>Phase 4 参战写入原因（Debug / 断言 / 验收）。</summary>
    public static class BattleParticipantInclusionReason
    {
        public const string None = "None";
        public const string DirectInitiator = "DirectInitiator";
        public const string DirectDefender = "DirectDefender";
        public const string SupportAreaArmy = "SupportAreaArmy";
        public const string SupportAreaPlayer = "SupportAreaPlayer";
        public const string ExcludedNotInSupportArea = "ExcludedNotInSupportArea";
        public const string ExcludedThirdParty = "ExcludedThirdParty";
        public const string ExcludedNotBelligerent = "ExcludedNotBelligerent";
        public const string PromoteInRangeIncapacitated = "PromoteInRangeIncapacitated";
        public const string LegacySnapshotBuilder = "LegacySnapshotBuilder";
        public const string LegacyMandatoryPartyRecords = "LegacyMandatoryPartyRecords";
        public const string LegacySeedMandatoryAttackers = "LegacySeedMandatoryAttackers";
    }

    /// <summary>Player 参战判定链 Debug 快照。</summary>
    public sealed class BattlePlayerInclusionPipelineTrace
    {
        public bool PlayerIncludedBeforeGathering { get; set; }
        public bool PlayerIncludedAfterGathering { get; set; }
        public bool PlayerIncludedAfterSnapshot { get; set; }
        public bool PlayerInSnapshotRecords { get; set; }
        public string PlayerIncludedReason { get; set; } = BattleParticipantInclusionReason.None;
        public string PlayerHexAuthoritySource { get; set; } = string.Empty;
        public string PlayerHexFormatted { get; set; } = string.Empty;
        public string SupportContainsPlayerHex { get; set; } = string.Empty;
        public string LastPlayerWriteSource { get; set; } = string.Empty;
    }
}
