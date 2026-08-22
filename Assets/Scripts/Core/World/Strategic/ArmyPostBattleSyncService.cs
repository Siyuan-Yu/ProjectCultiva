using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;

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
            if (army.IsTraveling)
                return;

            ArmyTravelCommandService.ReconcileArmyWithLivingMembers(world, army);
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

            army.RemainingTravelTicks = 0;
            army.TravelTotalTicks = 0;
            army.ClearRouteSegment();
            army.State = FormalArmyState.AtNode;

            if (!string.IsNullOrEmpty(snap.BattleAnchorRouteId) &&
                snap.BattleAnchorProgress >= 0f)
            {
                army.RouteId = snap.BattleAnchorRouteId;
                if (world?.WorldGraph != null &&
                    world.WorldGraph.TryGetRoute(snap.BattleAnchorRouteId, out var route) &&
                    route != null)
                {
                    ArmyTravelCommandService.NormalizeFormalArmyRouteEndpoints(world, army, route);
                    army.RouteAnchorProgress = ArmyTravelCommandService.ToGraphRouteProgress(
                        route,
                        snap.BattleAnchorNodeId ?? string.Empty,
                        snap.BattleAnchorDestNodeId ?? ResolveAnchorDest(world, snap),
                        snap.BattleAnchorProgress);
                }
                else
                {
                    army.RouteAnchorProgress = snap.BattleAnchorProgress;
                    army.NodeId = snap.BattleAnchorNodeId ?? string.Empty;
                    army.DestNodeId = ResolveAnchorDest(world, snap);
                }
            }
            else
            {
                army.RouteId = string.Empty;
                army.RouteAnchorProgress = -1f;
                army.NodeId = snap.BattleAnchorNodeId ?? army.NodeId ?? string.Empty;
                army.DestNodeId = string.Empty;
            }
        }

        static string ResolveAnchorDest(SimulationWorld world, BattleParticipantSnapshot snap)
        {
            if (!string.IsNullOrEmpty(snap.BattleAnchorDestNodeId))
                return snap.BattleAnchorDestNodeId;
            if (world?.WorldGraph != null &&
                !string.IsNullOrEmpty(snap.BattleAnchorRouteId) &&
                world.WorldGraph.TryGetRoute(snap.BattleAnchorRouteId, out var route) &&
                route != null)
            {
                if (string.Equals(route.FromNodeId, snap.BattleAnchorNodeId, StringComparison.Ordinal))
                    return route.ToNodeId ?? string.Empty;
                return route.FromNodeId ?? string.Empty;
            }

            return string.Empty;
        }
    }
}
