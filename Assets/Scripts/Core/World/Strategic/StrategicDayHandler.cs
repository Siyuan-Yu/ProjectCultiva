using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>日界：AI 帮派派兵（Phase 4）。</summary>
    public sealed class StrategicDayHandler : IDayBoundaryHandler
    {
        int _spawnCooldown;

        public void OnDayStarted(SimulationWorld world, ulong startedDayIndex)
        {
            if (world?.Strategic == null || !world.WorldGraph.HasGraph)
                return;

            _spawnCooldown--;
            if (_spawnCooldown > 0)
                return;

            TrySpawnBanditPatrol(world);
            _spawnCooldown = 2;
        }

        public void OnDayEnded(SimulationWorld world, ulong endedDayIndex)
        {
        }

        static void TrySpawnBanditPatrol(SimulationWorld world)
        {
            const string stackId = "army:bandit_patrol_auto";
            if (world.Strategic.Armies.TryGet(stackId, out var existing) &&
                existing != null &&
                (existing.IsTraveling || !string.IsNullOrEmpty(existing.NodeId)))
                return;

            if (!world.WorldGraph.TryFindRoute("base:node_linjian", "base:node_huangcun", out var route) &&
                !world.WorldGraph.TryFindRoute("base:node_huangcun", "base:node_linjian", out route))
                return;

            var stack = new ArmyStack
            {
                Id = stackId,
                FactionId = StrategicFactionCatalog.BanditId,
                DisplayName = "山匪斥候",
                NodeId = "base:node_linjian",
                MemberCount = 4,
                CombatPower = 2,
                RouteId = route.Id,
                DestNodeId = route.FromNodeId == "base:node_linjian" ? route.ToNodeId : route.FromNodeId,
                TravelTotalTicks = System.Math.Max(8, route.TravelCost * WorldTravelService.TravelTicksPerCostAtSpeed8),
                RemainingTravelTicks = System.Math.Max(8, route.TravelCost * WorldTravelService.TravelTicksPerCostAtSpeed8)
            };
            world.Strategic.Armies.Register(stack);
        }
    }
}
