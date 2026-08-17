using System;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Ch01 战略层默认归属／演示栈（外交／占点玩法暂关）。</summary>
    public static class StrategicBootstrap
    {
        public static void ApplyCh01Defaults(SimulationWorld world)
        {
            if (world?.Strategic == null || !world.WorldGraph.HasGraph)
                return;

            ApplyDefaultOwners(world);
            SeedDemoArmies(world);
        }

        static void ApplyDefaultOwners(SimulationWorld world)
        {
            // 暂不做战略势力归属／外交门槛；清空演示 Owner
            ClearOwner(world, "base:node_huangcun");
            ClearOwner(world, "base:node_yucun");
            ClearOwner(world, "base:node_linjian");
            ClearOwner(world, "base:node_kuangshan");
        }

        static void ClearOwner(SimulationWorld world, string nodeId)
        {
            if (!world.WorldGraph.TryGetNode(nodeId, out var node) || node == null)
                return;
            node.OwnerId = string.Empty;
        }

        static void SeedDemoArmies(SimulationWorld world)
        {
            world.Strategic.Armies.Clear();
            if (!world.WorldGraph.TryFindRoute("base:node_huangcun", "base:node_linjian", out var route) &&
                !world.WorldGraph.TryFindRoute("base:node_linjian", "base:node_huangcun", out route))
                return;

            var fromId = route.FromNodeId;
            var toId = route.ToNodeId;
            if (string.Equals(fromId, "base:node_linjian", StringComparison.Ordinal))
            {
                fromId = route.ToNodeId;
                toId = route.FromNodeId;
            }

            world.Strategic.Armies.Register(new ArmyStack
            {
                Id = "army:bandit_patrol_1",
                FactionId = StrategicFactionCatalog.BanditId,
                DisplayName = "林间山匪",
                NodeId = fromId,
                DestNodeId = toId,
                RouteId = route.Id,
                RouteAnchorProgress = 0.5f,
                MemberCount = 3,
                CombatPower = 2
            });
        }
    }
}
