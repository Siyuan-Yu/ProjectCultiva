using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Lingering re-entry 敌军名单真源：Registry 内冻结 Participants，禁止重查当前 Alive Army。
    /// </summary>
    public static class LingeringBattlefieldParticipantService
    {
        /// <summary>
        /// 将 Registry 内本场冻结 Participants 写入 Offer Snapshot（保留 Enemy，刷新 Friendly）。
        /// </summary>
        public static void ApplyStoredBattlefieldToOfferSnapshot(
            SimulationWorld world,
            LingeringBattlefieldState battlefield,
            BattleParticipantSnapshot snap,
            IReadOnlyList<EntityId> playerParty,
            string offerId,
            string attackerArmyId)
        {
            if (world == null || battlefield == null || snap == null)
                return;

            battlefield.Participants.CopyInto(snap);
            snap.OfferId = offerId ?? string.Empty;
            if (!string.IsNullOrEmpty(attackerArmyId))
                snap.AttackerArmyId = attackerArmyId;
            if (!string.IsNullOrEmpty(battlefield.EncounterLocalMapId))
                snap.EncounterLocalMapId = battlefield.EncounterLocalMapId;

            snap.RemoveFriendlyRecords();
            BattleOfferService.AddMandatoryPartyRecordsForLingering(world, snap, playerParty);
        }

        public static bool TryGetActiveStoredParticipants(
            SimulationWorld world,
            out LingeringBattlefieldState battlefield,
            out BattleParticipantSnapshot storedParticipants)
        {
            battlefield = null;
            storedParticipants = null;
            if (world?.Strategic?.Encounter == null)
                return false;

            var rt = world.Strategic.Encounter;
            if (string.IsNullOrEmpty(rt.ActiveBattlefieldId))
                return false;
            if (!world.Strategic.LingeringBattlefields.TryGetById(rt.ActiveBattlefieldId, out battlefield) ||
                battlefield == null)
                return false;

            storedParticipants = battlefield.Participants;
            return storedParticipants != null;
        }
    }
}
