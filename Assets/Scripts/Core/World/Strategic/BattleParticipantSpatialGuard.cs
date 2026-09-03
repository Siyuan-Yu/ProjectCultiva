using System.Diagnostics;
using System.Text;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
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
            AssertLockedFriendlyFormalArmyMembersCaptured(world, engagement, snap, "AfterSnapshot");
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

        /// <summary>
        /// Frozen participant snapshot 的友军 FormalArmy 完整性断言。
        /// FormalArmyContentBootstrap 创建的士兵带有 Npc 标签；该标签不能成为军团成员
        /// 被排除在 MandatoryFriendly 之外的理由。
        /// </summary>
        static void AssertLockedFriendlyFormalArmyMembersCaptured(
            SimulationWorld world,
            PendingEngagementRuntime engagement,
            BattleParticipantSnapshot snap,
            string stage)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (world?.Strategic?.FormalArmies == null || snap == null ||
                engagement?.LockedPlayerFormalArmyIds == null)
                return;

            var expected = 0;
            var captured = 0;
            for (var a = 0; a < engagement.LockedPlayerFormalArmyIds.Count; a++)
            {
                var armyId = engagement.LockedPlayerFormalArmyIds[a];
                if (string.IsNullOrEmpty(armyId) ||
                    !world.Strategic.FormalArmies.TryGet(armyId, out var army) || army == null)
                    continue;

                for (var i = 0; i < army.MemberCharacterIds.Count; i++)
                {
                    var memberId = new EntityId(army.MemberCharacterIds[i]);
                    if (memberId.IsNone ||
                        !LingeringBattlefieldPartyService.IsLivingForMacroOrder(world, memberId))
                        continue;

                    expected++;
                    var record = snap.FindByEntity(memberId);
                    if (record != null &&
                        record.Kind == BattleParticipantKind.MandatoryFriendly &&
                        string.Equals(record.FormalArmyId, army.ArmyId, System.StringComparison.Ordinal))
                    {
                        captured++;
                        continue;
                    }

                    var name = memberId.ToString();
                    var tags = "(entity missing)";
                    var faction = "(unknown)";
                    if (world.Entities.TryGet(memberId, out var entity) && entity != null)
                    {
                        name = string.IsNullOrEmpty(entity.DisplayName) ? memberId.ToString() : entity.DisplayName;
                        tags = entity.Tags.ToString();
                        if (entity.TryGet<FactionMembershipComponent>(out var membership) && membership != null)
                            faction = membership.FactionId ?? string.Empty;
                    }

                    Debug.WriteLine(
                        "[BattleFriendlySnapshot] " + stage +
                        " missing MandatoryFriendly record" +
                        " ArmyId=" + army.ArmyId +
                        " MemberId=" + memberId +
                        " Name=" + name +
                        " Tags=" + tags +
                        " Faction=" + faction +
                        " RecordKind=" + (record != null ? record.Kind.ToString() : "(none)") +
                        " RecordArmyId=" + (record?.FormalArmyId ?? string.Empty) +
                        " Reason=LockedPlayerFormalArmy living member is absent or mismatched in snapshot.");
                }
            }

            Debug.WriteLine(
                "[BattleFriendlySnapshot] " + stage +
                " ExpectedLivingLockedMembers=" + expected +
                " CapturedMandatoryFriendly=" + captured);
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
