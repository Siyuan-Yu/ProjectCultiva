using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// 战后 Formal Army 与接战锚点对齐：伤亡脱离军团、幸存者位置同步，避免 SyncFromArmy 把弥留者拉回军团路线。
    /// </summary>
    public static class ArmyPostBattleSyncService
    {
        public static void SyncAttackerArmyAfterBattle(SimulationWorld world, BattleParticipantSnapshot snap)
        {
            if (world?.Strategic?.FormalArmies == null || snap == null)
                return;
            if (!TryResolveAttackerArmy(world, snap, out var army) || army == null)
                return;

            ArmyService.DetachNonLivingMembersAtBattlefield(world, army);
            if (!TryResolveAttackerArmy(world, snap, out army) || army == null)
                return;

            if (!HasMacroOrderLivingMember(world, army))
                return;

            ParkArmyAtBattleAnchor(world, army, snap);
            ArmyPresenceAdapter.SyncFromArmy(world, army);
            StrategicPursuitService.ClearPursuit(world);
        }

        /// <summary>
        /// 敌军 FormalArmy 战后：先 Detach 非活员，再钉 Residual Hex。
        /// Detach 期间不得把弥留者送回编组 Site（见 ArmyService.RemoveMemberInternal）。
        /// </summary>
        public static void SyncEnemyArmyAfterBattle(SimulationWorld world, BattleParticipantSnapshot snap)
        {
            if (world?.Strategic == null || snap == null)
                return;

            if (!TryResolveEnemyFormalArmy(world, snap, out var army) || army == null)
                return;

            HexCoord encounterHex = default;
            var hasHex = ArmyHexBattleAnchorService.IsHexAnchorMode(world) &&
                         StrategicResidualPresenceService.TryResolveEncounterHex(world, snap, out encounterHex);

            // Detach 前先收齐成员并钉 Hex（Detach 可能 ForceRemoveArmy）
            var memberSnapshot = new List<EntityId>(army.MemberCharacterIds.Count);
            for (var i = 0; i < army.MemberCharacterIds.Count; i++)
            {
                var id = new EntityId(army.MemberCharacterIds[i]);
                if (!id.IsNone)
                    memberSnapshot.Add(id);
            }

            ArmyService.DetachNonLivingMembersAtBattlefield(world, army);

            // Detach 后再钉 Hex：RemoveMemberInternal 不再盖掉 Residual，Query 也不再被 FormalArmy 挡住。
            if (hasHex)
            {
                for (var i = 0; i < memberSnapshot.Count; i++)
                {
                    var id = memberSnapshot[i];
                    if (!LingeringBattlefieldPartyService.IsLingeringDowned(world, id))
                        continue;
                    StrategicResidualPresenceService.PlaceCharacterAtResidualHex(world, id, encounterHex);
                }
            }

            var stackId = ResolveEnemyStackId(world, snap);
            if (!string.IsNullOrEmpty(stackId) &&
                world.Strategic.Armies.TryGet(stackId, out var stack) &&
                stack != null)
            {
                // FormalArmy 可能已移除：保留栈上弥留／尸体计数供残留战场再入
                var incap = 0;
                var corpse = 0;
                for (var i = 0; i < memberSnapshot.Count; i++)
                {
                    var id = memberSnapshot[i];
                    if (LingeringBattlefieldPartyService.IsIncapacitated(world, id))
                        incap++;
                    else if (LingeringBattlefieldPartyService.IsVisibleCorpse(world, id))
                        corpse++;
                }

                if (incap > 0 || corpse > 0)
                {
                    stack.IsBattlefieldRemnant = true;
                    if (incap > 0)
                        stack.IncapacitatedMemberCount = Math.Max(stack.IncapacitatedMemberCount, incap);
                    if (corpse > 0)
                        stack.CorpseMemberCount = Math.Max(stack.CorpseMemberCount, corpse);
                    stack.MemberCount = Math.Max(
                        stack.MemberCount,
                        Math.Max(stack.IncapacitatedMemberCount, stack.CorpseMemberCount));
                }
            }
        }

        static bool TryResolveEnemyFormalArmy(
            SimulationWorld world,
            BattleParticipantSnapshot snap,
            out FormalArmy army)
        {
            army = null;
            var stackId = ResolveEnemyStackId(world, snap);
            if (string.IsNullOrEmpty(stackId) ||
                !world.Strategic.Armies.TryGet(stackId, out var stack) ||
                stack == null)
                return false;
            return ArmyStackAdapter.TryGetFormalArmy(world, stack, out army) && army != null;
        }

        static string ResolveEnemyStackId(SimulationWorld world, BattleParticipantSnapshot snap)
        {
            if (snap != null && !string.IsNullOrEmpty(snap.PrimaryEnemyStackId))
                return snap.PrimaryEnemyStackId;
            return world?.Strategic?.Encounter?.ArmyStackId ?? string.Empty;
        }

        /// <summary>清场后／手动战未 Resolve 前：用 living 成员路锚对齐 FormalArmy（仅位置，不 Detach）。</summary>
        public static void RefreshAttackerArmyFromMembers(SimulationWorld world)
        {
            if (world?.Strategic?.FormalArmies == null)
                return;
            var snap = world.Strategic.Participants;
            if (!TryResolveAttackerArmy(world, snap, out var army) || army == null)
                return;
            if (!HasMacroOrderLivingMember(world, army))
                return;
            if (army.State == FormalArmyState.Moving)
                return;

            ArmyPresenceAdapter.SyncFromArmy(world, army);
        }

        public static bool HasMacroOrderLivingMember(SimulationWorld world, FormalArmy army)
        {
            if (world == null || army == null)
                return false;
            for (var i = 0; i < army.MemberCharacterIds.Count; i++)
            {
                var id = new EntityId(army.MemberCharacterIds[i]);
                if (LingeringBattlefieldPartyService.IsLivingForMacroOrder(world, id))
                    return true;
            }

            return false;
        }

        static bool TryResolveAttackerArmy(
            SimulationWorld world,
            BattleParticipantSnapshot snap,
            out FormalArmy army)
        {
            army = null;
            if (world?.Strategic?.FormalArmies == null || snap == null)
                return false;

            if (!string.IsNullOrEmpty(snap.AttackerArmyId) &&
                world.Strategic.FormalArmies.TryGet(snap.AttackerArmyId, out army) &&
                army != null)
                return true;

            var party = CollectMandatoryFriendlyParty(world, snap);
            if (party.Count == 0)
                return false;

            if (!ArmyStackAdapter.TryResolveAttackerArmyId(world, party, out var armyId) ||
                string.IsNullOrEmpty(armyId))
                return false;

            snap.AttackerArmyId = armyId;
            return world.Strategic.FormalArmies.TryGet(armyId, out army) && army != null;
        }

        static List<EntityId> CollectMandatoryFriendlyParty(
            SimulationWorld world,
            BattleParticipantSnapshot snap)
        {
            var list = new List<EntityId>(8);
            if (world == null || snap == null)
                return list;

            for (var i = 0; i < snap.Records.Count; i++)
            {
                var rec = snap.Records[i];
                if (rec.EntityId.IsNone)
                    continue;
                if (rec.Kind != BattleParticipantKind.MandatoryFriendly)
                    continue;
                if (!LingeringBattlefieldPartyService.IsLivingForMacroOrder(world, rec.EntityId))
                    continue;
                list.Add(rec.EntityId);
            }

            if (list.Count > 0)
                return list;

            var engaged = world.Strategic?.Encounter;
            if (engaged == null || !engaged.HasEngagedParty)
                return list;
            for (var i = 0; i < engaged.EngagedPartyIds.Count; i++)
            {
                var id = new EntityId(engaged.EngagedPartyIds[i]);
                if (id.IsNone || !LingeringBattlefieldPartyService.IsLivingForMacroOrder(world, id))
                    continue;
                list.Add(id);
            }

            return list;
        }

        static void ParkArmyAtBattleAnchor(
            SimulationWorld world,
            FormalArmy army,
            BattleParticipantSnapshot snap)
        {
            if (army == null || snap == null)
                return;

            ArmyHexBattleAnchorService.ParkArmyAtBattleAnchor(world, army, snap);
        }
    }
}
