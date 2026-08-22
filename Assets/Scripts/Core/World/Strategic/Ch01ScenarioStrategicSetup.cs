using System;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Ch01 Opening Scenario 战略初始化（Final Closure）。
    /// Generic StrategicBootstrap 不拥有剧情外交决定权；本类负责 Ch01 兼容与 Scenario Hook 注册。
    /// </summary>
    public static class Ch01ScenarioStrategicSetup
    {
        public static void Apply(SimulationWorld world)
        {
            if (world?.Strategic == null || !world.WorldGraph.HasGraph)
                return;

            world.Strategic.Ch01FormationScenarioCompat = true;
            ApplyPlayerFactionAndVassalage(world);
            ApplyPrototypeNodeOwners(world);
            SeedPrototypeBanditArmies(world);
            ApplyPrototypeRegressionDiplomacy(world);
            Ch01ScenarioProgressionHooks.Register(world);
        }

        /// <summary>主角团（PlayerFaction）为压迫宗门附庸；战略 UI 只认 PlayerFaction 成员。</summary>
        static void ApplyPlayerFactionAndVassalage(SimulationWorld world)
        {
            world.Strategic.PlayerFactionId = StrategicFactionCatalog.PlayerFactionId;
            world.Strategic.Vassalages.TryBindVassalage(
                StrategicFactionCatalog.PlayerFactionId,
                StrategicFactionCatalog.HuangcunLaborId);
        }

        /// <summary>Ch01 Prototype：演示节点保持无战略归属；已有 Owner 不覆盖。</summary>
        static void ApplyPrototypeNodeOwners(SimulationWorld world)
        {
            ClearOwnerIfEmpty(world, "base:node_huangcun");
            ClearOwnerIfEmpty(world, "base:node_yucun");
            ClearOwnerIfEmpty(world, "base:node_linjian");
            ClearOwnerIfEmpty(world, "base:node_kuangshan");
        }

        static void ClearOwnerIfEmpty(SimulationWorld world, string nodeId)
        {
            if (!world.WorldGraph.TryGetNode(nodeId, out var node) || node == null)
                return;
            if (!string.IsNullOrEmpty(node.OwnerId))
                return;
            node.OwnerId = string.Empty;
        }

        static void SeedPrototypeBanditArmies(SimulationWorld world)
        {
            world.Strategic.Armies.Clear();
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

            ArmyStackAdapter.EnsureBanditPatrolArmy(world, fromId, route.Id, toId, 0.42f);
        }

        /// <summary>
        /// Prototype 回归用 Bandit 敌对 — 非 Ch01 正式剧情战争。
        /// 正式剧情 War（Player vs FormerOverlordSect）须在荒村 Capture 后由 Scenario Progression 触发。
        /// </summary>
        static void ApplyPrototypeRegressionDiplomacy(SimulationWorld world)
        {
            WarGateService.DeclareWar(world, StrategicFactionCatalog.HuangcunLaborId, StrategicFactionCatalog.BanditId);
            WarGateService.DeclareWar(world, StrategicFactionCatalog.PlayerFactionId, StrategicFactionCatalog.BanditId);
        }
    }
}
