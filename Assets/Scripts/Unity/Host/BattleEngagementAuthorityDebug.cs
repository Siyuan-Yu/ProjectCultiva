using System.Text;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>LevelTester Battle Authority Debug 输出。</summary>
    public static class BattleEngagementAuthorityDebug
    {
        public static string BuildSummary(SimulationWorld world)
        {
            if (world?.Strategic == null)
                return "No strategic board.";

            var engagement = world.Strategic.PendingEngagement;
            if (engagement == null || !engagement.IsActive)
                return BuildPreEngagementTriggerSummary(world);

            var options = BattleDecisionPolicy.ResolveDecisionOptions(engagement);
            var sb = new StringBuilder(3072);
            sb.AppendLine("=== 接战触发 ===");
            AppendTriggerSection(world, engagement, sb);
            sb.AppendLine("=== 参与者收集 ===");
            sb.AppendLine("EngagementId=" + engagement.EngagementId);
            sb.AppendLine("BattleLocationHex(presentation)=" + FormatHex(engagement.BattleLocation));
            sb.AppendLine("BattleInitiator=" + engagement.InitiatorKind + " " + engagement.InitiatorFormalArmyId);
            sb.AppendLine("Defender=" + engagement.DefenderFormalArmyId);

            AppendArmyHexLine(world, "Initiator", engagement.InitiatorFormalArmyId, sb);
            AppendArmyHexLine(world, "Defender", engagement.DefenderFormalArmyId, sb);

            if (engagement.HasSupportArea)
            {
                var supportArea = engagement.SupportArea;
                sb.AppendLine("BattleSiteId=" + supportArea.BattleSiteId);
                sb.AppendLine("BattleSiteResolutionSource=" + supportArea.BattleSiteResolutionSource);
                AppendHexList(sb, "BattleAreaHexes", supportArea.BattleAreaHexes);
                AppendHexList(sb, "SupportRingHexes", supportArea.SupportRingHexes);
                AppendHexList(sb, "SupportAreaHexes", supportArea.SupportAreaHexes);
                AppendSupportAreaConstructionTrace(world, engagement, supportArea, sb);
            }
            else
            {
                sb.AppendLine("BattleAreaHexes=(none)");
                sb.AppendLine("SupportAreaHexes=(none)");
            }

            AppendPlayerHexSection(world, engagement, sb);

            sb.AppendLine("PlayerPartyIncluded=" + engagement.PlayerPartyIncluded);
            sb.AppendLine("PlayerIncludedReason=" + engagement.PlayerInclusionReason);
            sb.AppendLine("ManualEligible=" + options.Manual);
            AppendPlayerInclusionPipeline(engagement, sb);
            sb.AppendLine("DecisionOptions Manual=" + options.Manual + " Auto=" + options.Auto +
                          " Retreat=" + options.Retreat);
            AppendArmyList(sb, "LockedPlayerArmies", engagement.LockedPlayerFormalArmyIds);
            AppendArmyList(sb, "LockedEnemyArmies", engagement.LockedEnemyFormalArmyIds);
            AppendCandidateGatheringChecks(world, engagement, sb);
            AppendSnapshotParticipantReasons(world, sb);

            if (engagement.HasInitiatorEngagementLocation)
            {
                sb.AppendLine("InitiatorEngagementLocation(debug-only)=" +
                              FormatHex(engagement.InitiatorEngagementLocation.Hex) +
                              (string.IsNullOrEmpty(engagement.InitiatorEngagementSiteId)
                                  ? string.Empty
                                  : " site=" + engagement.InitiatorEngagementSiteId));
            }

            if (engagement.DecisionSubjectRetreatLocation != null)
            {
                var r = engagement.DecisionSubjectRetreatLocation;
                sb.AppendLine("PreEngagementLegalLocation kind=" +
                              (r.IsPlayerParty ? r.PartyLocationKind.ToString() : r.ArmyLocationKind.ToString()) +
                              " site=" + r.SiteId + " hex=" + FormatHex(r.Hex));
            }

            return sb.ToString();
        }

        static string BuildPreEngagementTriggerSummary(SimulationWorld world)
        {
            var sb = new StringBuilder(512);
            sb.AppendLine("No active PendingEngagement.");
            var rt = world.Strategic.Encounter;
            if (string.IsNullOrEmpty(rt?.PursueAttackerArmyId) ||
                string.IsNullOrEmpty(rt.PursueDefenderArmyId))
                return sb.ToString();

            sb.AppendLine("=== 接战触发(追击态) ===");
            AppendArmyHexLine(world, "Initiator", rt.PursueAttackerArmyId, sb);
            AppendArmyHexLine(world, "Defender", rt.PursueDefenderArmyId, sb);
            var adjacent = BattleEngagementTriggerService.IsActuallyAdjacentToBattleArea(
                world, rt.PursueAttackerArmyId, rt.PursueDefenderArmyId);
            BattleEngagementTriggerService.CanTriggerEngagement(
                world, rt.PursueAttackerArmyId, rt.PursueDefenderArmyId, out var reason);
            sb.AppendLine("IsActuallyAdjacentToBattleArea=" + adjacent);
            sb.AppendLine("PendingBattleTriggerReason=" + reason);
            return sb.ToString();
        }

        static void AppendTriggerSection(
            SimulationWorld world,
            PendingEngagementRuntime engagement,
            StringBuilder sb)
        {
            AppendArmyHexLine(world, "Initiator", engagement.InitiatorFormalArmyId, sb);
            AppendArmyHexLine(world, "Defender", engagement.DefenderFormalArmyId, sb);

            if (engagement.HasInitiatorCommittedHex)
                sb.AppendLine("InitiatorHex(at-trigger)=" + FormatHex(engagement.InitiatorCommittedHex));
            if (engagement.DefenderCommittedHexQ != ArmyHexBattleAnchorService.InvalidHexComponent)
                sb.AppendLine("DefenderHex(at-trigger)=" + FormatHex(engagement.DefenderCommittedHex));

            var adjacent = BattleEngagementTriggerService.IsActuallyAdjacentToBattleArea(
                world, engagement.InitiatorFormalArmyId, engagement.DefenderFormalArmyId);
            sb.AppendLine("IsActuallyAdjacentToBattleArea=" + adjacent);
            sb.AppendLine("PendingBattleTriggerReason=" + engagement.PendingBattleTriggerReason);
        }

        static void AppendArmyHexLine(
            SimulationWorld world,
            string label,
            string armyId,
            StringBuilder sb)
        {
            if (string.IsNullOrEmpty(armyId) ||
                !world.Strategic.FormalArmies.TryGet(armyId, out var army) ||
                army == null)
            {
                sb.AppendLine(label + "Hex=(none)");
                return;
            }

            BattleEngagementSpatialQuery.TryGetCommittedArmyHex(world, army, out var committedHex);
            BattleEngagementSpatialQuery.TryGetDerivedArmyHexForDebug(world, army, out var derivedHex);
            sb.AppendLine(label + "Hex=" + FormatHex(committedHex));
            sb.AppendLine(label + " ContinuousDerivedHex=" + FormatHex(derivedHex));
            if (army.WorldMotion != null && army.WorldMotion.HasPosition)
                sb.AppendLine(label + " ContinuousWorldPosition=" + army.WorldMotion.WorldPosition);
        }

        static void AppendSupportAreaConstructionTrace(
            SimulationWorld world,
            PendingEngagementRuntime engagement,
            BattleEngagementSupportArea supportArea,
            StringBuilder sb)
        {
            var defenderHex = supportArea.PresentationAnchorHex;
            if (engagement.DefenderCommittedHexQ != ArmyHexBattleAnchorService.InvalidHexComponent)
                defenderHex = engagement.DefenderCommittedHex;
            else if (BattleEngagementSpatialQuery.TryGetCommittedArmyHex(
                         world, engagement.DefenderFormalArmyId, out var resolvedDefenderHex))
                defenderHex = resolvedDefenderHex;

            var hasInitiatorHex = engagement.HasInitiatorCommittedHex;
            var initiatorHex = engagement.InitiatorCommittedHex;
            if (!hasInitiatorHex &&
                BattleEngagementSpatialQuery.TryGetCommittedArmyHex(
                    world, engagement.InitiatorFormalArmyId, out var resolvedInitiatorHex))
            {
                hasInitiatorHex = true;
                initiatorHex = resolvedInitiatorHex;
            }

            var hasPlayerHex = false;
            var playerHex = default(HexCoord);
            var party = world?.Strategic?.PlayerPartyContext;
            if (party != null &&
                party.HasActive &&
                BattleEngagementSpatialQuery.TryGetCommittedPartyHex(
                    world, party, out var authorityHex, out _))
            {
                hasPlayerHex = true;
                playerHex = authorityHex;
            }

            supportArea.AppendConstructionTrace(
                sb,
                defenderHex,
                initiatorHex,
                hasInitiatorHex,
                playerHex,
                hasPlayerHex);
        }

        static void AppendPlayerHexSection(
            SimulationWorld world,
            PendingEngagementRuntime engagement,
            StringBuilder sb)
        {
            var party = world.Strategic.PlayerPartyContext;
            if (party == null || !party.HasActive)
            {
                sb.AppendLine("PlayerHex=(none)");
                return;
            }

            var activeId = party.ActiveCharacterId;
            BattleEngagementSpatialQuery.TryGetCommittedPartyHex(
                world, party, out var authorityHex, out var authoritySource);
            sb.AppendLine("PlayerHex(authority)=" + FormatHex(authorityHex));
            sb.AppendLine("PlayerHexAuthoritySource=" + authoritySource);

            if (engagement.HasSupportArea)
            {
                var inSupport = engagement.SupportArea.Contains(authorityHex);
                sb.AppendLine("PlayerInSupportArea=" + inSupport);
                sb.AppendLine("PlayerIncludedReason=" +
                              (engagement.PlayerPartyIncluded
                                  ? (inSupport ? "SupportAreaContainsAuthorityHex" : "LockedWithoutSpatialCheck?")
                                  : "NotInSupportArea"));
            }

            if (CharacterWorldPresenceQuery.TryGetWorldHex(world, activeId, out var presenceHex))
                sb.AppendLine("PlayerHex(WorldPresence)=" + FormatHex(presenceHex));

            if (PlayerPartyWorldLocationQuery.TryResolve(world, party, out var marker))
                sb.AppendLine("PlayerHex(MarkerDerived)=" + FormatHex(marker.DerivedHex));

            if (world.PlayerPartyTravel != null && world.PlayerPartyTravel.HasPosition)
            {
                sb.AppendLine("PlayerPartyTravel.CurrentHex=" + FormatHex(world.PlayerPartyTravel.CurrentHex));
                sb.AppendLine("PlayerContinuousWorldPosition=" + world.PlayerPartyTravel.WorldPosition);
            }
        }

        static void AppendCandidateGatheringChecks(
            SimulationWorld world,
            PendingEngagementRuntime engagement,
            StringBuilder sb)
        {
            if (!engagement.HasSupportArea || world?.Strategic?.FormalArmies == null)
                return;

            var supportArea = engagement.SupportArea;
            var initiatorId = engagement.InitiatorFormalArmyId ?? string.Empty;
            var defenderId = engagement.DefenderFormalArmyId ?? string.Empty;
            var playerFaction = engagement.PrimaryPlayerFactionId ?? string.Empty;
            var enemyFaction = engagement.PrimaryEnemyFactionId ?? string.Empty;

            foreach (var kv in world.Strategic.FormalArmies.Armies)
            {
                var army = kv.Value;
                if (army == null || string.IsNullOrEmpty(army.ArmyId))
                    continue;

                var mandatory = string.Equals(army.ArmyId, initiatorId, System.StringComparison.Ordinal) ||
                                string.Equals(army.ArmyId, defenderId, System.StringComparison.Ordinal);
                var belligerent = string.Equals(army.FactionId, playerFaction, System.StringComparison.Ordinal) ||
                                  string.Equals(army.FactionId, enemyFaction, System.StringComparison.Ordinal);
                var hasLivingMember = ArmyPostBattleSyncService.HasMacroOrderLivingMember(world, army);
                var lockedElsewhere = IsArmyLockedInAnotherBattle(world, army.ArmyId, engagement.EngagementId);
                var hasSpatialHex = BattleEngagementSpatialQuery.TryGetCommittedArmyHex(
                    world, army, out var armyHex);
                var inBattleArea = hasSpatialHex && supportArea.ContainsBattleArea(armyHex);
                var inSupportRing = hasSpatialHex && supportArea.ContainsSupportRing(armyHex);
                var inSupportArea = hasSpatialHex && supportArea.Contains(armyHex);
                var finalIncluded = mandatory
                    ? hasLivingMember && belligerent
                    : hasLivingMember && belligerent && !lockedElsewhere && inSupportArea;
                var exclusionReason = !hasLivingMember
                    ? "NoLivingMember"
                    : !belligerent
                        ? "WrongFaction"
                        : mandatory
                            ? "DirectCombatant"
                            : lockedElsewhere
                                ? "LockedInAnotherBattle"
                                : !hasSpatialHex
                                    ? "NoSpatialHex"
                                    : !inSupportArea
                                        ? "OutsideSupportArea"
                                        : "IncludedSupportAreaArmy";

                sb.AppendLine("ArmyId=" + army.ArmyId +
                              " FactionId=" + army.FactionId +
                              " Leader=" + army.LeaderCharacterId +
                              " Living=" + hasLivingMember +
                              " LockedElsewhere=" + lockedElsewhere +
                              " Belligerent=" + belligerent);
                if (army.WorldMotion != null && army.WorldMotion.HasPosition)
                {
                    var motion = army.WorldMotion;
                    sb.AppendLine("  Motion.LocationKind=" + motion.LocationKind +
                                  " Motion.IsMoving=" + motion.IsMoving +
                                  " Motion.WorldPosition=" + motion.WorldPosition +
                                  " Motion.CurrentHex=" + FormatHex(motion.CurrentHex) +
                                  " SegmentIndex=" + motion.SegmentIndex +
                                  " SegmentProgress=" + motion.SegmentProgress);
                    if (motion.TryGetActiveStepHexes(out var from, out var to))
                        sb.AppendLine("  ActiveStepFrom=" + FormatHex(from) +
                                      " ActiveStepTo=" + FormatHex(to));
                }
                sb.AppendLine("  BattleSpatialHex=" + (hasSpatialHex ? FormatHex(armyHex) : "(none)") +
                              " InBattleArea=" + inBattleArea +
                              " InSupportRing=" + inSupportRing +
                              " InSupportArea=" + inSupportArea +
                              " FinalIncluded=" + finalIncluded +
                              " ExclusionReason=" + exclusionReason);
            }
        }

        static bool IsArmyLockedInAnotherBattle(
            SimulationWorld world,
            string armyId,
            string currentEngagementId)
        {
            if (string.IsNullOrEmpty(armyId) || world?.Strategic == null)
                return false;

            var pending = world.Strategic.PendingEngagement;
            if (pending != null &&
                pending.IsActive &&
                !string.Equals(pending.EngagementId, currentEngagementId, System.StringComparison.Ordinal) &&
                pending.ContainsFormalArmy(armyId))
                return true;

            if (!world.Strategic.IsModalEncounter || world.Strategic.Participants == null)
                return false;

            var records = world.Strategic.Participants.Records;
            for (var i = 0; i < records.Count; i++)
            {
                var record = records[i];
                if (record != null &&
                    string.Equals(record.FormalArmyId, armyId, System.StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        static void AppendSnapshotParticipantReasons(SimulationWorld world, StringBuilder sb)
        {
            var snap = world?.Strategic?.Participants;
            if (snap == null || snap.Records.Count == 0)
                return;

            sb.AppendLine("=== Snapshot Participants ===");
            for (var i = 0; i < snap.Records.Count; i++)
            {
                var rec = snap.Records[i];
                if (rec.Kind == BattleParticipantKind.OptionalFriendly)
                    continue;
                sb.AppendLine(
                    rec.DisplayLabel +
                    " kind=" + rec.Kind +
                    " army=" + (rec.FormalArmyId ?? string.Empty) +
                    " IncludedReason=" + (string.IsNullOrEmpty(rec.IncludedReason) ? "(none)" : rec.IncludedReason));
            }
        }

        static void AppendPlayerInclusionPipeline(
            PendingEngagementRuntime engagement,
            StringBuilder sb)
        {
            var trace = engagement.PlayerInclusionTrace;
            sb.AppendLine("Player Included Before Gathering=" + trace.PlayerIncludedBeforeGathering);
            sb.AppendLine("Player Included After Gathering=" + trace.PlayerIncludedAfterGathering);
            sb.AppendLine("Player Included After Snapshot=" + trace.PlayerIncludedAfterSnapshot);
            sb.AppendLine("Player In Snapshot Records=" + trace.PlayerInSnapshotRecords);
            sb.AppendLine("Player IncludedReason(final)=" + engagement.PlayerInclusionReason);
            sb.AppendLine("Player Last Write Source=" + trace.LastPlayerWriteSource);
        }

        static void AppendHexList(StringBuilder sb, string label, System.Collections.Generic.IReadOnlyList<HexCoord> hexes)
        {
            sb.Append(label).Append('=');
            if (hexes == null || hexes.Count == 0)
            {
                sb.AppendLine("(none)");
                return;
            }

            sb.Append('[');
            for (var i = 0; i < hexes.Count; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                sb.Append(FormatHex(hexes[i]));
            }

            sb.AppendLine("]");
        }

        static void AppendArmyList(StringBuilder sb, string label, System.Collections.Generic.IReadOnlyList<string> ids)
        {
            sb.Append(label).Append('=');
            if (ids == null || ids.Count == 0)
            {
                sb.AppendLine("(none)");
                return;
            }

            for (var i = 0; i < ids.Count; i++)
            {
                if (i > 0)
                    sb.Append(',');
                sb.Append(ids[i]);
            }

            sb.AppendLine();
        }

        static string FormatHex(HexCoord hex) => "(" + hex.Q + "," + hex.R + ")";
    }
}
