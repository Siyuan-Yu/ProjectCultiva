using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Formal Army 战略 Travel tick 推进（Phase D）。</summary>
    public static class ArmyTravelService
    {
        public static void AdvanceAll(SimulationWorld world, int ticks, List<EntityId> arrivedOut = null)
        {
            if (world?.Strategic?.FormalArmies == null || ticks < 1)
                return;

            using (FormalArmyStrategicMutationDiagnostics.Scope(
                       FormalArmyStrategicMutationDiagnostics.MutationAllowance.TravelTick,
                       nameof(AdvanceAll)))
            {
                AdvanceAllCore(world, ticks, arrivedOut);
            }
        }

        static void AdvanceAllCore(SimulationWorld world, int ticks, List<EntityId> arrivedOut)
        {
            foreach (var kv in world.Strategic.FormalArmies.Armies)
            {
                var army = kv.Value;
                if (army == null || !army.IsTraveling)
                    continue;

                army.RemainingTravelTicks -= ticks;
                if (army.RemainingTravelTicks > 0)
                {
                    ArmyPresenceAdapter.SyncFromArmy(world, army);
                    continue;
                }

                CompleteArmyTravelLeg(world, army, arrivedOut);
            }
        }

        static void CompleteArmyTravelLeg(
            SimulationWorld world,
            FormalArmy army,
            List<EntityId> arrivedOut)
        {
            if (army.RouteSegmentEndProgress >= 0f && army.RouteSegmentOriginProgress >= 0f)
            {
                if (army.RouteSegmentEndProgress > 0.01f && army.RouteSegmentEndProgress < 0.99f)
                {
                    army.RouteAnchorProgress = army.RouteSegmentEndProgress;
                    army.State = FormalArmyState.AtNode;
                    army.RemainingTravelTicks = 0;
                    army.TravelTotalTicks = 0;
                    army.ClearRouteSegment();
                    ArmyPresenceAdapter.SyncFromArmy(world, army);
                    ArmyTravelCommandService.TryContinueQueuedTravel(world, army.ArmyId);
                    return;
                }

                if (world.WorldGraph.TryGetRoute(army.RouteId, out var route) && route != null)
                {
                    army.NodeId = army.RouteSegmentEndProgress >= 0.99f
                        ? route.ToNodeId ?? army.DestNodeId ?? army.NodeId
                        : route.FromNodeId ?? army.NodeId;
                }
                else if (!string.IsNullOrEmpty(army.DestNodeId))
                {
                    army.NodeId = army.RouteSegmentEndProgress >= 0.99f
                        ? army.DestNodeId
                        : army.NodeId;
                }

                army.State = FormalArmyState.AtNode;
                army.RouteId = string.Empty;
                army.DestNodeId = string.Empty;
                army.RouteAnchorProgress = -1f;
                army.RemainingTravelTicks = 0;
                army.TravelTotalTicks = 0;
                army.ClearRouteSegment();
                ArmyPresenceAdapter.SyncFromArmy(world, army);
                ArmyTravelCommandService.TryContinueQueuedTravel(world, army.ArmyId);
                return;
            }

            if (!string.IsNullOrEmpty(army.DestNodeId))
                army.NodeId = army.DestNodeId;
            army.State = FormalArmyState.AtNode;
            army.RouteAnchorProgress = -1f;
            army.ClearTravel();
            ArmyPresenceAdapter.SyncFromArmy(world, army);

            CollectArrivedMembers(world, army, arrivedOut);
            ArmyTravelCommandService.TryContinueQueuedTravel(world, army.ArmyId);
        }

        static void CollectArrivedMembers(
            SimulationWorld world,
            FormalArmy army,
            List<EntityId> arrivedOut)
        {
            if (arrivedOut == null)
                return;
            for (var i = 0; i < army.MemberCharacterIds.Count; i++)
            {
                var id = new EntityId(army.MemberCharacterIds[i]);
                if (id.IsNone)
                    continue;
                if (world.WorldPresence.TryGet(id, out var p) &&
                    p != null &&
                    p.Mode == PartyWorldPresenceMode.AtNode)
                    arrivedOut.Add(id);
            }
        }
    }
}
