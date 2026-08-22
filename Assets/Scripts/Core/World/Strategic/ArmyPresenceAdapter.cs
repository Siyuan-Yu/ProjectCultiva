using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// FormalArmy StrategicPosition → 成员 WorldAgentPresence 单向投影（Phase D）。
    /// 禁止从 Character Presence 反推 Army 位置。
    /// </summary>
    public static class ArmyPresenceAdapter
    {
        public static void SyncFromArmy(SimulationWorld world, FormalArmy army)
        {
            if (world == null || army == null)
                return;

            for (var i = 0; i < army.MemberCharacterIds.Count; i++)
            {
                var memberId = new EntityId(army.MemberCharacterIds[i]);
                if (memberId.IsNone)
                    continue;
                if (!LingeringBattlefieldPartyService.IsLivingForMacroOrder(world, memberId))
                    continue;
                if (!world.WorldPresence.TryGet(memberId, out var presence) || presence == null)
                    continue;

                ProjectMemberPresence(world, army, presence);
            }
        }

        public static void SyncAll(SimulationWorld world)
        {
            if (world?.Strategic?.FormalArmies == null)
                return;
            foreach (var kv in world.Strategic.FormalArmies.Armies)
                SyncFromArmy(world, kv.Value);
        }

        static void ProjectMemberPresence(
            SimulationWorld world,
            FormalArmy army,
            WorldAgentPresence presence)
        {
            var pursueStackId = ResolvePursuitStackId(world, army);
            if (string.IsNullOrEmpty(pursueStackId))
                presence.ClearCombatPursuit();
            else
                presence.CombatPursuitStackId = pursueStackId;

            if (army.IsTraveling)
            {
                presence.Mode = PartyWorldPresenceMode.Traveling;
                presence.NodeId = army.NodeId;
                presence.RouteId = army.RouteId;
                presence.DestNodeId = army.DestNodeId;
                presence.TravelTotalTicks = army.TravelTotalTicks;
                presence.RemainingTravelTicks = army.RemainingTravelTicks;
                presence.RouteAnchorProgress = -1f;
                if (army.RouteSegmentOriginProgress >= 0f && army.RouteSegmentEndProgress >= 0f)
                {
                    presence.RouteSegmentOriginProgress = army.RouteSegmentOriginProgress;
                    presence.RouteSegmentEndProgress = army.RouteSegmentEndProgress;
                }
                else
                    presence.ClearRouteSegment();
                return;
            }

            if (army.IsRouteAnchored)
            {
                presence.Mode = PartyWorldPresenceMode.RouteAnchored;
                presence.NodeId = army.NodeId;
                presence.RouteId = army.RouteId;
                presence.DestNodeId = army.DestNodeId;
                presence.RouteAnchorProgress = army.RouteAnchorProgress;
                presence.RemainingTravelTicks = 0;
                presence.TravelTotalTicks = 0;
                presence.ClearRouteSegment();
                return;
            }

            presence.Mode = PartyWorldPresenceMode.AtNode;
            presence.NodeId = army.NodeId;
            presence.RouteId = string.Empty;
            presence.DestNodeId = string.Empty;
            presence.RouteAnchorProgress = -1f;
            presence.RemainingTravelTicks = 0;
            presence.TravelTotalTicks = 0;
            presence.ClearRouteSegment();
        }

        static string ResolvePursuitStackId(SimulationWorld world, FormalArmy army)
        {
            var rt = world?.Strategic?.Encounter;
            if (rt == null || army == null || string.IsNullOrEmpty(army.ArmyId))
                return string.Empty;
            if (!string.Equals(rt.PursueAttackerArmyId, army.ArmyId, System.StringComparison.Ordinal))
                return string.Empty;
            return rt.PursueStackId ?? string.Empty;
        }
    }
}
