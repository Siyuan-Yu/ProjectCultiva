using System;
using System.Collections.Generic;
using XianXia.Core.Persistence;
using XianXia.Core.Simulation;
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
                OfferId = offer.OfferId,
                OfferTitle = offer.Title,
                ArmyStackId = offer.ArmyStackId,
                EncounterLocalMapId = offer.EncounterLocalMapId,
            };

            if (engagement.HasSupportArea)
            {
                var supportArea = engagement.SupportArea;
                CopyHexList(supportArea.BattleAreaHexes, snap.BattleAreaHexQList, snap.BattleAreaHexRList);
                CopyHexList(supportArea.SupportAreaHexes, snap.SupportAreaHexQList, snap.SupportAreaHexRList);
            }

            CopyArmyIds(engagement.LockedPlayerFormalArmyIds, snap.PlayerFormalArmyIds);
            CopyArmyIds(engagement.LockedEnemyFormalArmyIds, snap.EnemyFormalArmyIds);
            for (var i = 0; i < engagement.LockedPlayerPartyMemberIds.Count; i++)
                snap.PlayerPartyMemberIds.Add(engagement.LockedPlayerPartyMemberIds[i]);

            var retreat = engagement.DecisionSubjectRetreatLocation;
            if (retreat != null)
            {
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
                for (var i = 0; i < participants.Records.Count; i++)
                {
                    var r = participants.Records[i];
                    if (r == null)
                        continue;
                    snap.ParticipantRecords.Add(new PendingEngagementParticipantRecordDto
                    {
                        Kind = (int)r.Kind,
                        EntityId = r.EntityId.Value,
                        ArmyStackId = r.ArmyStackId,
                        FormalArmyId = r.FormalArmyId,
                        DisplayLabel = r.DisplayLabel,
                        CombatPower = r.CombatPower,
                        Selected = r.Selected,
                    });
                }
            }

            dto.PendingEngagement = snap;
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
            engagement.RequiresPlayerDecision = src.InvolvesPlayerSide;
            engagement.PrimaryPlayerFactionId = world.Strategic.PlayerFactionId ?? string.Empty;

            for (var i = 0; i < src.PlayerFormalArmyIds.Count; i++)
                engagement.AddPlayerFormalArmy(src.PlayerFormalArmyIds[i]);
            for (var i = 0; i < src.EnemyFormalArmyIds.Count; i++)
                engagement.AddEnemyFormalArmy(src.EnemyFormalArmyIds[i]);

            var partyMembers = new List<XianXia.Core.Domain.Ids.EntityId>(src.PlayerPartyMemberIds.Count);
            for (var i = 0; i < src.PlayerPartyMemberIds.Count; i++)
                partyMembers.Add(new XianXia.Core.Domain.Ids.EntityId(src.PlayerPartyMemberIds[i]));
            engagement.SetPlayerPartyMembers(partyMembers);
            // Phase 5S-B2-3.4：PlayerParty Initiator 加载后 IncludedReason 恢复为 DirectInitiator
            // （locked members 已持久化，无需重新 Gather；reason 从 InitiatorKind 推导，不加新 DTO 字段）。
            if (engagement.InitiatorKind == BattleInitiatorKind.PlayerParty &&
                engagement.PlayerPartyIncluded)
            {
                engagement.PlayerInclusionReason = BattleParticipantInclusionReason.DirectInitiator;
            }

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

            var offer = world.Strategic.BattleOffer;
            offer.Resolved = false;
            offer.OfferId = src.OfferId ?? src.EngagementId;
            offer.Title = src.OfferTitle ?? string.Empty;
            offer.ArmyStackId = src.ArmyStackId ?? string.Empty;
            offer.AttackerArmyId = src.AttackerFormalArmyId ?? string.Empty;
            offer.DefenderArmyId = src.DefenderFormalArmyId ?? string.Empty;
            offer.EncounterLocalMapId = src.EncounterLocalMapId ?? string.Empty;

            var participants = world.Strategic.Participants;
            participants.Clear();
            participants.OfferId = src.ParticipantOfferId ?? string.Empty;
            participants.AttackerArmyId = src.ParticipantAttackerArmyId ?? string.Empty;
            participants.DefenderArmyId = src.ParticipantDefenderArmyId ?? string.Empty;
            participants.PrimaryEnemyStackId = src.ParticipantPrimaryEnemyStackId ?? string.Empty;
            participants.BattleAnchorHexQ = src.ParticipantBattleAnchorHexQ;
            participants.BattleAnchorHexR = src.ParticipantBattleAnchorHexR;
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
                });
            }

            BattleOfferService.RefreshOfferPowerLabels(world);
            StrategicClockFreezeService.BeginOrPromote(world, StrategicClockFreezeReason.BattleOffer);
        }

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
                    battleArea, supportArea, presentation));
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
