using System.Diagnostics;
using System.Text;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Phase 4：Participant 空间硬约束与违规断言。</summary>
    public static class BattleParticipantSpatialGuard
    {
        public static void ValidateAfterGathering(
            SimulationWorld world,
            PendingEngagementRuntime engagement,
            PlayerPartyRuntime party)
        {
            if (world == null || engagement == null || !engagement.HasSupportArea)
                return;

            RefreshPlayerPipelineTrace(world, engagement, party, null, "AfterGathering");
            AssertPlayerPartySpatialInvariant(world, engagement, party, null, "AfterGathering");
        }

        public static void ValidateAfterSnapshot(
            SimulationWorld world,
            PendingEngagementRuntime engagement,
            PlayerPartyRuntime party,
            BattleParticipantSnapshot snap)
        {
            if (world == null || engagement == null || !engagement.HasSupportArea)
                return;

            RefreshPlayerPipelineTrace(world, engagement, party, snap, "AfterSnapshot");
            AssertPlayerPartySpatialInvariant(world, engagement, party, snap, "AfterSnapshot");
            AssertSnapshotPlayerRecordsInSupportArea(world, engagement, snap, "AfterSnapshot");
        }

        public static void RefreshPlayerPipelineTrace(
            SimulationWorld world,
            PendingEngagementRuntime engagement,
            PlayerPartyRuntime party,
            BattleParticipantSnapshot snap,
            string stage)
        {
            if (engagement == null)
                return;

            var trace = engagement.PlayerInclusionTrace;
            trace.PlayerIncludedAfterGathering = engagement.PlayerPartyIncluded;
            trace.PlayerIncludedReason = engagement.PlayerInclusionReason ?? BattleParticipantInclusionReason.None;

            if (party != null && party.HasActive &&
                BattleEngagementSpatialQuery.TryGetCommittedPartyHex(
                    world, party, out var playerHex, out var source))
            {
                trace.PlayerHexAuthoritySource = source.ToString();
                trace.PlayerHexFormatted = FormatHex(playerHex);
                trace.SupportContainsPlayerHex = engagement.SupportArea.Contains(playerHex).ToString();
            }

            if (snap != null)
            {
                trace.PlayerInSnapshotRecords = ContainsNonArmyPlayerRecord(world, snap, party);
                trace.PlayerIncludedAfterSnapshot = trace.PlayerInSnapshotRecords;
            }

            trace.LastPlayerWriteSource = stage ?? string.Empty;
        }

        static void AssertPlayerPartySpatialInvariant(
            SimulationWorld world,
            PendingEngagementRuntime engagement,
            PlayerPartyRuntime party,
            BattleParticipantSnapshot snap,
            string stage)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!engagement.PlayerPartyIncluded || party == null || !party.HasActive || !engagement.HasSupportArea)
                return;

            if (!BattleEngagementSpatialQuery.TryGetCommittedPartyHex(
                    world, party, out var playerHex, out var source))
                return;

            if (engagement.SupportArea.Contains(playerHex))
                return;

            LogSpatialViolation(
                stage,
                "PlayerPartyIncluded=true but PlayerHex not in SupportAreaHexes",
                engagement,
                playerHex,
                source.ToString(),
                engagement.PlayerInclusionReason,
                engagement.PlayerInclusionTrace.LastPlayerWriteSource);
#endif
        }

        static void AssertSnapshotPlayerRecordsInSupportArea(
            SimulationWorld world,
            PendingEngagementRuntime engagement,
            BattleParticipantSnapshot snap,
            string stage)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (snap == null || !engagement.HasSupportArea)
                return;

            for (var i = 0; i < snap.Records.Count; i++)
            {
                var rec = snap.Records[i];
                if (rec.EntityId.IsNone)
                    continue;
                if (rec.Kind != BattleParticipantKind.MandatoryFriendly &&
                    rec.Kind != BattleParticipantKind.OptionalFriendly)
                    continue;
                if (!string.IsNullOrEmpty(rec.FormalArmyId))
                    continue;
                if (ArmyService.TryGetArmyForCharacter(world, rec.EntityId, out _))
                    continue;

                if (!BattleEngagementSpatialQuery.TryGetCommittedCharacterHex(
                        world, rec.EntityId, out var memberHex))
                    continue;

                if (engagement.SupportArea.Contains(memberHex))
                    continue;

                LogSpatialViolation(
                    stage,
                    "Snapshot contains non-army Player record outside SupportAreaHexes",
                    engagement,
                    memberHex,
                    rec.IncludedReason ?? "(none)",
                    engagement.PlayerInclusionReason,
                    rec.DisplayLabel);
            }
#endif
        }

        public static bool ContainsNonArmyPlayerRecord(
            SimulationWorld world,
            BattleParticipantSnapshot snap,
            PlayerPartyRuntime party)
        {
            if (snap == null || party == null)
                return false;

            for (var i = 0; i < party.Members.Count; i++)
            {
                var id = party.Members[i];
                if (id.IsNone || snap.FindByEntity(id) == null)
                    continue;
                if (ArmyService.TryGetArmyForCharacter(world, id, out _))
                    continue;
                return true;
            }

            return false;
        }

        static void LogSpatialViolation(
            string stage,
            string message,
            PendingEngagementRuntime engagement,
            HexCoord playerHex,
            string detailA,
            string detailB,
            string detailC)
        {
            var sb = new StringBuilder(512);
            sb.Append("[BattleParticipantSpatialGuard] ").Append(stage).Append(": ").AppendLine(message);
            sb.AppendLine("PlayerHex=" + FormatHex(playerHex));
            if (engagement.HasSupportArea)
            {
                engagement.SupportArea.AppendHexList(sb, "BattleAreaHexes", engagement.SupportArea.BattleAreaHexes);
                engagement.SupportArea.AppendHexList(sb, "SupportAreaHexes", engagement.SupportArea.SupportAreaHexes);
            }

            sb.AppendLine("DetailA=" + detailA);
            sb.AppendLine("DetailB=" + detailB);
            sb.AppendLine("DetailC=" + detailC);
            sb.AppendLine("PlayerIncludedAfterGathering=" + engagement.PlayerPartyIncluded);
            sb.AppendLine("PlayerIncludedReason=" + engagement.PlayerInclusionReason);
            Debug.WriteLine(sb.ToString());
        }

        static string FormatHex(HexCoord hex) => "(" + hex.Q + "," + hex.R + ")";
    }
}
