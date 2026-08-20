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
            // 放在荒村→青石关路上偏关隘一侧，避免叠在荒村大标签下
            if (!world.WorldGraph.TryFindRoute("base:node_huangcun", "base:node_guanai", out var route) &&
                !world.WorldGraph.TryFindRoute("base:node_guanai", "base:node_huangcun", out route))
            {
                if (!world.WorldGraph.TryFindRoute("base:node_huangcun", "base:node_linjian", out route) &&
                    !world.WorldGraph.TryFindRoute("base:node_linjian", "base:node_huangcun", out route))
                    return;
            }

            var fromId = route.FromNodeId;
            var toId = route.ToNodeId;
            if (!string.Equals(fromId, "base:node_huangcun", StringComparison.Ordinal))
            {
                fromId = route.ToNodeId;
                toId = route.FromNodeId;
            }

            world.Strategic.Armies.Register(new ArmyStack
            {
                Id = "army:bandit_patrol_1",
                FactionId = StrategicFactionCatalog.BanditId,
                DisplayName = "荒村山匪",
                NodeId = fromId,
                DestNodeId = toId,
                RouteId = route.Id,
                RouteAnchorProgress = 0.42f,
                MemberCount = 4,
                CombatPower = 2
            });
        }
    }
}
