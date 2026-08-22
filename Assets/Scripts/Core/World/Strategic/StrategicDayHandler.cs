using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>日界：AI 帮派派兵（Phase 4）。FormalArmy 真源；禁止 anonymous cultivator ArmyStack。</summary>
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

            TrySpawnBanditScout(world);
            _spawnCooldown = 2;
        }

        public void OnDayEnded(SimulationWorld world, ulong endedDayIndex)
        {
        }

        static void TrySpawnBanditScout(SimulationWorld world)
        {
            if (world.Strategic.Armies.TryGet(ArmyStackAdapter.BanditScoutStackId, out var existing) &&
                existing != null &&
                (existing.IsTraveling || !string.IsNullOrEmpty(existing.NodeId)))
                return;

            if (!world.WorldGraph.TryFindRoute("base:node_linjian", "base:node_huangcun", out var route) &&
                !world.WorldGraph.TryFindRoute("base:node_huangcun", "base:node_linjian", out route))
                return;

            var fromId = route.FromNodeId;
            var toId = route.ToNodeId;
            if (!string.Equals(fromId, "base:node_linjian", System.StringComparison.Ordinal))
            {
                fromId = route.ToNodeId;
                toId = route.FromNodeId;
            }

            var ticks = System.Math.Max(8, route.TravelCost * WorldTravelService.TravelTicksPerCostAtSpeed8);
            ArmyStackAdapter.EnsureBanditScoutArmy(
                world,
                fromId,
                route.Id,
                toId,
                routeAnchorProgress: -1f,
                travelTicks: ticks);
        }
    }
}
