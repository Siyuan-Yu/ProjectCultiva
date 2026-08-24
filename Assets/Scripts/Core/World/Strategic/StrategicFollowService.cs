using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>大地图 RTS：选中部队跟随目标 ArmyStack。</summary>
    public static class StrategicFollowService
    {
        public static void BeginFollow(
            SimulationWorld world,
            IReadOnlyList<EntityId> party,
            ArmyStack stack)
        {
            if (world?.Strategic == null || stack == null || party == null || party.Count == 0)
                return;

            StrategicPursuitService.ClearPursuit(world);
            for (var i = 0; i < party.Count; i++)
            {
                var id = party[i];
                if (id.IsNone || !world.WorldPresence.TryGet(id, out var p) || p == null)
                    continue;
                p.FollowStackId = stack.Id ?? string.Empty;
            }

            SyncFollowers(world);
        }

        public static void ClearFollow(SimulationWorld world, EntityId id)
        {
            if (world == null || id.IsNone || !world.WorldPresence.TryGet(id, out var p) || p == null)
                return;
            p.ClearFollow();
        }

        public static void ClearFollowParty(SimulationWorld world, IReadOnlyList<EntityId> party)
        {
            if (world == null || party == null)
                return;
            for (var i = 0; i < party.Count; i++)
                ClearFollow(world, party[i]);
        }

        public static void AfterTravelTick(SimulationWorld world)
        {
            if (world?.Strategic == null)
                return;
            SyncFollowers(world);
        }

        public static void SyncFollowers(SimulationWorld world)
        {
            foreach (var kv in world.WorldPresence.All)
            {
                var p = kv.Value;
                if (p == null || !p.IsFollowingStack)
                    continue;
                if (!world.Strategic.Armies.TryGet(p.FollowStackId, out var stack) || stack == null)
                {
                    p.ClearFollow();
                    continue;
                }

                SyncAgentToStack(world, p.EntityId, p, stack);
            }
        }

        static void SyncAgentToStack(
            SimulationWorld world,
            EntityId id,
            WorldAgentPresence presence,
            ArmyStack stack)
        {
            if (id.IsNone || presence == null || stack == null)
                return;
            if (presence.Mode == PartyWorldPresenceMode.InEncounter)
                return;
            if (!WorldTravelService.CanReceiveTravelOrder(world, id))
                return;

            if (stack.IsRouteAnchored &&
                StrategicNodeAccessService.IsAgentAtStackAnchor(world, presence, stack))
                return;

            // Pure Hex: legacy stack follow travel removed.
        }

        public static Result BeginFollowTravel(
            SimulationWorld world,
            IReadOnlyList<EntityId> party,
            ArmyStack stack)
        {
            if (world == null || party == null || party.Count == 0 || stack == null)
                return Result.Failure(ErrorCode.InvalidArgument, "Invalid follow order.");

            BeginFollow(world, party, stack);
            return Result.Success();
        }
    }
}
