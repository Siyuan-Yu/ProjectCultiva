using System;
using System.Collections.Generic;
using System.Text;
using XianXia.Core.Persistence;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Persistence
{
    /// <summary>Pending Engagement + BattleOffer 决策态 Snapshot（Phase 4）。</summary>
    public static class PendingEngagementSnapshotRestore
    {
        public static void Capture(SimulationWorld world, StrategicSnapshotDto dto)
        {
            if (world?.Strategic == null || dto == null)
                return;

            var engagement = world.Strategic.PendingEngagement;
            var offer = world.Strategic.BattleOffer;
            if (engagement == null || !engagement.IsActive || offer == null || offer.Resolved)
                return;

            var snap = new PendingEngagementSnapshotDto
            {
                EngagementId = engagement.EngagementId,
                InitiatorKind = (int)engagement.InitiatorKind,
                InitiatorFormalArmyId = engagement.InitiatorFormalArmyId,
                InitiatorIsPlayerSide = engagement.InitiatorIsPlayerSide,
                DecisionSubjectKind = (int)engagement.DecisionSubjectKind,
                DecisionSubjectFormalArmyId = engagement.DecisionSubjectFormalArmyId,
                BattleLocationHexQ = engagement.BattleLocationHexQ,
                BattleLocationHexR = engagement.BattleLocationHexR,
                InitiatorEngagementHexQ = engagement.InitiatorEngagementHexQ,
                InitiatorEngagementHexR = engagement.InitiatorEngagementHexR,
                InitiatorEngagementSiteId = engagement.InitiatorEngagementSiteId ?? string.Empty,
                AttackerFormalArmyId = engagement.AttackerFormalArmyId,
                DefenderFormalArmyId = engagement.DefenderFormalArmyId,
                PlayerPartyIncluded = engagement.PlayerPartyIncluded,
                InvolvesPlayerSide = engagement.InvolvesPlayerSide,
                PrimaryEnemyFactionId = engagement.PrimaryEnemyFactionId ?? string.Empty,
                PlayerInclusionReason = engagement.PlayerInclusionReason ?? string.Empty,
                RequiresPlayerDecision = engagement.RequiresPlayerDecision,
                PendingBattleTriggerReason = engagement.PendingBattleTriggerReason ?? string.Empty,
                InitiatorCommittedHexQ = engagement.InitiatorCommittedHexQ,
                InitiatorCommittedHexR = engagement.InitiatorCommittedHexR,
                DefenderCommittedHexQ = engagement.DefenderCommittedHexQ,
                DefenderCommittedHexR = engagement.DefenderCommittedHexR,
                OfferId = offer.OfferId,
                OfferTitle = offer.Title,
                ArmyStackId = offer.ArmyStackId,
                EncounterLocalMapId = offer.EncounterLocalMapId,
                OfferOrigin = (int)offer.Origin,
                OfferRequiresWarDeclaration = offer.RequiresWarDeclaration,
                PendingWarAttackerFactionId = offer.PendingWarAttackerFactionId,
                PendingWarDefenderFactionId = offer.PendingWarDefenderFactionId,
            };

            if (engagement.HasSupportArea)
            {
                var supportArea = engagement.SupportArea;
                CopyHexList(supportArea.BattleAreaHexes, snap.BattleAreaHexQList, snap.BattleAreaHexRList);
                CopyHexList(supportArea.SupportAreaHexes, snap.SupportAreaHexQList, snap.SupportAreaHexRList);
                snap.SupportBattleSiteId = supportArea.BattleSiteId ?? string.Empty;
                snap.SupportBattleSiteResolutionSource = supportArea.BattleSiteResolutionSource ?? string.Empty;
            }

            CopyArmyIds(engagement.LockedPlayerFormalArmyIds, snap.PlayerFormalArmyIds);
            CopyArmyIds(engagement.LockedEnemyFormalArmyIds, snap.EnemyFormalArmyIds);
            for (var i = 0; i < engagement.LockedPlayerPartyMemberIds.Count; i++)
                snap.PlayerPartyMemberIds.Add(engagement.LockedPlayerPartyMemberIds[i]);

            var retreat = engagement.DecisionSubjectRetreatLocation;
            if (retreat != null)
            {
                snap.RetreatHasValue = true;
                snap.RetreatArmyLocationKind = (int)retreat.ArmyLocationKind;
                snap.RetreatPartyLocationKind = (int)retreat.PartyLocationKind;
                snap.RetreatSiteId = retreat.SiteId ?? string.Empty;
                snap.RetreatWorldX = retreat.WorldX;
                snap.RetreatWorldY = retreat.WorldY;
                snap.RetreatHexQ = retreat.HexQ;
                snap.RetreatHexR = retreat.HexR;
                snap.RetreatIsPlayerParty = retreat.IsPlayerParty;
            }

            var participants = world.Strategic.Participants;
            if (participants != null)
            {
                snap.ParticipantOfferId = participants.OfferId;
                snap.ParticipantAttackerArmyId = participants.AttackerArmyId;
                snap.ParticipantDefenderArmyId = participants.DefenderArmyId;
                snap.ParticipantPrimaryEnemyStackId = participants.PrimaryEnemyStackId;
                snap.ParticipantBattleAnchorHexQ = participants.BattleAnchorHexQ;
                snap.ParticipantBattleAnchorHexR = participants.BattleAnchorHexR;
                snap.ParticipantEncounterLocalMapId = participants.EncounterLocalMapId ?? string.Empty;
                snap.ParticipantLocalMapResolutionKind = (int)participants.LocalMapResolutionKind;
                snap.HasParticipantLocalMapResolutionKind = true;
                for (var i = 0; i < participants.Records.Count; i++)
                {
                    var r = participants.Records[i];
                    if (r == null)
                        continue;
                    var rec = new PendingEngagementParticipantRecordDto
                    {
                        Kind = (int)r.Kind,
                        EntityId = r.EntityId.Value,
                        ArmyStackId = r.ArmyStackId,
                        FormalArmyId = r.FormalArmyId,
                        DisplayLabel = r.DisplayLabel,
                        CombatPower = r.CombatPower,
                        Selected = r.Selected,
                        IncludedReason = r.IncludedReason ?? string.Empty,
                    };
                    if (r.PreBattle != null)
                    {
                        rec.HasPreBattle = true;
                        rec.PreBattleMode = (int)r.PreBattle.Mode;
                        rec.PreBattleSiteId = r.PreBattle.SiteId ?? string.Empty;
                        rec.PreBattleHexQ = r.PreBattle.HexQ;
                        rec.PreBattleHexR = r.PreBattle.HexR;
                        rec.PreBattleFollowStackId = r.PreBattle.FollowStackId ?? string.Empty;
                        rec.PreBattleCombatPursuitStackId = r.PreBattle.CombatPursuitStackId ?? string.Empty;
                    }

                    snap.ParticipantRecords.Add(rec);
                }
            }

            dto.PendingEngagement = snap;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            EmitSnapshotDump(world, snap);
#endif
        }

        public static void Restore(SimulationWorld world, StrategicSnapshotDto dto)
        {
            if (world?.Strategic == null || dto?.PendingEngagement == null)
                return;

            var src = dto.PendingEngagement;
            if (string.IsNullOrEmpty(src.EngagementId))
                return;

            var engagement = world.Strategic.PendingEngagement;
            engagement.Clear();
            engagement.EngagementId = src.EngagementId;
            engagement.InitiatorKind = (BattleInitiatorKind)src.InitiatorKind;
            engagement.InitiatorFormalArmyId = src.InitiatorFormalArmyId ?? string.Empty;
            engagement.InitiatorIsPlayerSide = src.InitiatorIsPlayerSide;
            engagement.DecisionSubjectKind = (BattleDecisionSubjectKind)src.DecisionSubjectKind;
            engagement.DecisionSubjectFormalArmyId = src.DecisionSubjectFormalArmyId ?? string.Empty;
            engagement.SetInitiatorEngagementLocation(new InitiatorEngagementLocation(
                new HexCoord(src.InitiatorEngagementHexQ, src.InitiatorEngagementHexR),
                src.InitiatorEngagementSiteId ?? string.Empty,
                src.InitiatorEngagementHexQ != ArmyHexBattleAnchorService.InvalidHexComponent &&
                src.InitiatorEngagementHexR != ArmyHexBattleAnchorService.InvalidHexComponent));
            if (!engagement.HasInitiatorEngagementLocation &&
                !string.IsNullOrEmpty(engagement.InitiatorFormalArmyId))
            {
                engagement.SetInitiatorEngagementLocation(
                    BattleEngagementHexDistance.ResolveInitiatorEngagementLocation(
                        world, engagement.InitiatorFormalArmyId));
            }
            engagement.AttackerFormalArmyId = src.AttackerFormalArmyId ?? string.Empty;
            engagement.DefenderFormalArmyId = src.DefenderFormalArmyId ?? string.Empty;
            RestoreSupportArea(world, engagement, src);
            engagement.PlayerPartyIncluded = src.PlayerPartyIncluded;
            engagement.InvolvesPlayerSide = src.InvolvesPlayerSide;
            // Phase 5S Persistence：RequiresPlayerDecision 用持久化值，不再用 InvolvesPlayerSide 推导。
            // （旧存档无该字段时 Read 层已 fallback 到 involvesPlayerSide，语义与旧推导一致。）
            engagement.RequiresPlayerDecision = src.RequiresPlayerDecision;
            engagement.PrimaryPlayerFactionId = world.Strategic.PlayerFactionId ?? string.Empty;
            engagement.PrimaryEnemyFactionId = src.PrimaryEnemyFactionId ?? string.Empty;
            engagement.PendingBattleTriggerReason = src.PendingBattleTriggerReason ?? string.Empty;
            engagement.InitiatorCommittedHexQ = src.InitiatorCommittedHexQ;
            engagement.InitiatorCommittedHexR = src.InitiatorCommittedHexR;
            engagement.DefenderCommittedHexQ = src.DefenderCommittedHexQ;
            engagement.DefenderCommittedHexR = src.DefenderCommittedHexR;

            for (var i = 0; i < src.PlayerFormalArmyIds.Count; i++)
                engagement.AddPlayerFormalArmy(src.PlayerFormalArmyIds[i]);
            for (var i = 0; i < src.EnemyFormalArmyIds.Count; i++)
                engagement.AddEnemyFormalArmy(src.EnemyFormalArmyIds[i]);

            var partyMembers = new List<XianXia.Core.Domain.Ids.EntityId>(src.PlayerPartyMemberIds.Count);
            for (var i = 0; i < src.PlayerPartyMemberIds.Count; i++)
                partyMembers.Add(new XianXia.Core.Domain.Ids.EntityId(src.PlayerPartyMemberIds[i]));
            engagement.SetPlayerPartyMembers(partyMembers);
            // Phase 5S Persistence：PlayerInclusionReason 以持久化为准；
            // 仅旧 snapshot（无该字段）才从 InitiatorKind 推导 DirectInitiator。
            if (!string.IsNullOrEmpty(src.PlayerInclusionReason))
            {
                engagement.PlayerInclusionReason = src.PlayerInclusionReason;
            }
            else if (engagement.InitiatorKind == BattleInitiatorKind.PlayerParty &&
                     engagement.PlayerPartyIncluded)
            {
                engagement.PlayerInclusionReason = BattleParticipantInclusionReason.DirectInitiator;
            }

            if (src.RetreatHasValue)
            {
                engagement.DecisionSubjectRetreatLocation = new PreEngagementLegalLocation
                {
                    ArmyLocationKind = (FormalArmyLocationKind)src.RetreatArmyLocationKind,
                    PartyLocationKind = (PlayerPartyLocationKind)src.RetreatPartyLocationKind,
                    SiteId = src.RetreatSiteId ?? string.Empty,
                    WorldX = src.RetreatWorldX,
                    WorldY = src.RetreatWorldY,
                    HexQ = src.RetreatHexQ,
                    HexR = src.RetreatHexR,
                    IsPlayerParty = src.RetreatIsPlayerParty,
                };
            }
            else
            {
                engagement.DecisionSubjectRetreatLocation = null;
            }

            var offer = world.Strategic.BattleOffer;
            offer.Resolved = false;
            offer.OfferId = src.OfferId ?? src.EngagementId;
            offer.Title = src.OfferTitle ?? string.Empty;
            offer.ArmyStackId = src.ArmyStackId ?? string.Empty;
            offer.AttackerArmyId = src.AttackerFormalArmyId ?? string.Empty;
            offer.DefenderArmyId = src.DefenderFormalArmyId ?? string.Empty;
            offer.EncounterLocalMapId = src.EncounterLocalMapId ?? string.Empty;
            offer.Origin = (BattleOfferOrigin)src.OfferOrigin;
            offer.RequiresWarDeclaration = src.OfferRequiresWarDeclaration;
            offer.PendingWarAttackerFactionId = src.PendingWarAttackerFactionId ?? string.Empty;
            offer.PendingWarDefenderFactionId = src.PendingWarDefenderFactionId ?? string.Empty;

            var participants = world.Strategic.Participants;
            participants.Clear();
            participants.OfferId = src.ParticipantOfferId ?? string.Empty;
            participants.AttackerArmyId = src.ParticipantAttackerArmyId ?? string.Empty;
            participants.DefenderArmyId = src.ParticipantDefenderArmyId ?? string.Empty;
            participants.PrimaryEnemyStackId = src.ParticipantPrimaryEnemyStackId ?? string.Empty;
            participants.BattleAnchorHexQ = src.ParticipantBattleAnchorHexQ;
            participants.BattleAnchorHexR = src.ParticipantBattleAnchorHexR;
            // Phase 5S Persistence：frozen participant LocalMap 决议以持久化为准（Auto 不得
            // 因缺字段回退 ExplicitEncounterMap）。ParticipantEncounterLocalMapId 优先，
            // 旧 snapshot 缺省时回退 offer 级 EncounterLocalMapId。
            participants.EncounterLocalMapId =
                string.IsNullOrEmpty(src.ParticipantEncounterLocalMapId)
                    ? src.EncounterLocalMapId ?? string.Empty
                    : src.ParticipantEncounterLocalMapId;
            if (src.HasParticipantLocalMapResolutionKind)
            {
                participants.LocalMapResolutionKind =
                    (BattleLocalMapResolutionKind)src.ParticipantLocalMapResolutionKind;
            }
            else
            {
                // 旧 snapshot fallback：0 与缺失无法区分（WorldSite=0），必须重新 resolve。
                var resolution = BattleLocalMapResolver.ResolvePendingEngagement(world);
                participants.LocalMapResolutionKind = resolution.Success
                    ? resolution.Kind
                    : BattleLocalMapResolutionKind.ExplicitEncounterMap;
            }

            for (var i = 0; i < src.ParticipantRecords.Count; i++)
            {
                var r = src.ParticipantRecords[i];
                participants.Add(new BattleParticipantRecord
                {
                    Kind = (BattleParticipantKind)r.Kind,
                    EntityId = new XianXia.Core.Domain.Ids.EntityId(r.EntityId),
                    ArmyStackId = r.ArmyStackId ?? string.Empty,
                    FormalArmyId = r.FormalArmyId ?? string.Empty,
                    DisplayLabel = r.DisplayLabel ?? string.Empty,
                    CombatPower = r.CombatPower,
                    Selected = r.Selected,
                    IncludedReason = r.IncludedReason ?? string.Empty,
                    PreBattle = r.HasPreBattle
                        ? new PreBattleWorldPresence
                        {
                            Mode = (PartyWorldPresenceMode)r.PreBattleMode,
                            SiteId = r.PreBattleSiteId ?? string.Empty,
                            HexQ = r.PreBattleHexQ,
                            HexR = r.PreBattleHexR,
                            FollowStackId = r.PreBattleFollowStackId ?? string.Empty,
                            CombatPursuitStackId = r.PreBattleCombatPursuitStackId ?? string.Empty
                        }
                        : null
                });
            }

            // Phase 5S Persistence：BattleOffer.PlayerPartyIds 不额外存一份 roster ——
            // Participants.Selected 是 frozen selection authority，恢复后从 selected friendly 重建。
            offer.SetPlayerParty(participants.CollectSelectedFriendly());

            BattleOfferService.RefreshOfferPowerLabels(world);
            StrategicClockFreezeService.BeginOrPromote(world, StrategicClockFreezeReason.BattleOffer);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            EmitSnapshotDump(world, src);
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Save 前 / Load 后 dump frozen engagement 关键事实，方便直接比较（Phase 5S Persistence）。</summary>
        static void EmitSnapshotDump(SimulationWorld world, PendingEngagementSnapshotDto snap)
        {
            if (snap == null)
                return;
            var sb = new StringBuilder();
            sb.Append("[PendingEngagementSnapshot] EngagementId=").Append(snap.EngagementId ?? "");
            sb.Append(" InitiatorKind=").Append(snap.InitiatorKind);
            sb.Append(" DecisionSubjectKind=").Append(snap.DecisionSubjectKind);
            sb.Append(" BattleLocation=(").Append(snap.BattleLocationHexQ).Append(',').Append(snap.BattleLocationHexR).Append(')');
            sb.Append(" BattleAreaCount=").Append(snap.BattleAreaHexQList?.Count ?? 0);
            sb.Append(" SupportAreaCount=").Append(snap.SupportAreaHexQList?.Count ?? 0);
            sb.Append(" BattleSiteId=").Append(snap.SupportBattleSiteId ?? "");
            sb.Append(" AttackerArmy=").Append(snap.AttackerFormalArmyId ?? "");
            sb.Append(" DefenderArmy=").Append(snap.DefenderFormalArmyId ?? "");
            sb.Append(" PlayerPartyIncluded=").Append(snap.PlayerPartyIncluded);
            sb.Append(" PlayerInclusionReason=").Append(snap.PlayerInclusionReason ?? "");
            sb.Append(" RequiresPlayerDecision=").Append(snap.RequiresPlayerDecision);
            sb.Append(" LockedPlayerArmies=").Append(snap.PlayerFormalArmyIds?.Count ?? 0);
            sb.Append(" LockedEnemyArmies=").Append(snap.EnemyFormalArmyIds?.Count ?? 0);
            sb.Append(" LockedPartyMembers=").Append(snap.PlayerPartyMemberIds?.Count ?? 0);
            sb.Append(" BattleAnchor=(").Append(snap.ParticipantBattleAnchorHexQ).Append(',').Append(snap.ParticipantBattleAnchorHexR).Append(')');
            sb.Append(" ParticipantCount=").Append(snap.ParticipantRecords?.Count ?? 0);
            sb.Append(" ParticipantLocalMapResolutionKind=").Append(snap.ParticipantLocalMapResolutionKind);
            sb.Append(" HasResolutionKind=").Append(snap.HasParticipantLocalMapResolutionKind);
            sb.Append(" ParticipantMap=").Append(snap.ParticipantEncounterLocalMapId ?? "");
            sb.Append(" OfferOrigin=").Append(snap.OfferOrigin);
            sb.Append(" RequiresWarDeclaration=").Append(snap.OfferRequiresWarDeclaration);
            sb.Append(" RetreatHasValue=").Append(snap.RetreatHasValue);
            if (world?.Strategic != null && world.Strategic.PendingEngagement != null)
                sb.Append(" RuntimeIsActive=").Append(world.Strategic.PendingEngagement.IsActive);
            System.Diagnostics.Debug.WriteLine(sb.ToString());
        }
#endif

        static void CopyArmyIds(IReadOnlyList<string> from, List<string> into)
        {
            into.Clear();
            if (from == null)
                return;
            for (var i = 0; i < from.Count; i++)
            {
                if (!string.IsNullOrEmpty(from[i]))
                    into.Add(from[i]);
            }
        }

        static void CopyHexList(
            IReadOnlyList<HexCoord> from,
            List<int> qList,
            List<int> rList)
        {
            qList.Clear();
            rList.Clear();
            if (from == null)
                return;
            for (var i = 0; i < from.Count; i++)
            {
                qList.Add(from[i].Q);
                rList.Add(from[i].R);
            }
        }

        static List<HexCoord> ReadHexList(List<int> qList, List<int> rList)
        {
            var hexes = new List<HexCoord>(8);
            if (qList == null || rList == null)
                return hexes;
            var count = Math.Min(qList.Count, rList.Count);
            for (var i = 0; i < count; i++)
                hexes.Add(new HexCoord(qList[i], rList[i]));
            return hexes;
        }

        static void RestoreSupportArea(
            SimulationWorld world,
            PendingEngagementRuntime engagement,
            PendingEngagementSnapshotDto src)
        {
            var presentation = new HexCoord(src.BattleLocationHexQ, src.BattleLocationHexR);
            var battleArea = ReadHexList(src.BattleAreaHexQList, src.BattleAreaHexRList);
            var supportArea = ReadHexList(src.SupportAreaHexQList, src.SupportAreaHexRList);

            if (battleArea.Count > 0 || supportArea.Count > 0)
            {
                engagement.SetSupportArea(BattleEngagementSupportArea.FromFrozenLists(
                    battleArea,
                    supportArea,
                    presentation,
                    src.SupportBattleSiteId,
                    src.SupportBattleSiteResolutionSource));
                return;
            }

            if (!string.IsNullOrEmpty(engagement.DefenderFormalArmyId))
            {
                engagement.SetSupportArea(BattleEngagementSupportArea.ResolveAndFreeze(
                    world, engagement.DefenderFormalArmyId));
                return;
            }

            engagement.SetBattleLocation(presentation);
        }
    }
}
