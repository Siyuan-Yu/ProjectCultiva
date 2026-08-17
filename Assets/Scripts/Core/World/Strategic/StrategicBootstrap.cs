using System;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Ch01 战略层默认归属／外交／演示栈。</summary>
    public static class StrategicBootstrap
    {
        public static void ApplyCh01Defaults(SimulationWorld world)
        {
            if (world?.Strategic == null || !world.WorldGraph.HasGraph)
                return;

            ApplyDefaultOwners(world);
            ApplyDefaultDiplomacy(world.Strategic.Diplomacy);
            SeedDemoArmies(world);
        }

        static void ApplyDefaultOwners(SimulationWorld world)
        {
            SetOwnerIfEmpty(world, "base:node_huangcun", StrategicFactionCatalog.HuangcunLaborId);
            SetOwnerIfEmpty(world, "base:node_yucun", StrategicFactionCatalog.FisherVillageId);
            SetOwnerIfEmpty(world, "base:node_linjian", StrategicFactionCatalog.BanditId);
            SetOwnerIfEmpty(world, "base:node_kuangshan", StrategicFactionCatalog.BanditId);
        }

        static void SetOwnerIfEmpty(SimulationWorld world, string nodeId, string ownerId)
        {
            if (!world.WorldGraph.TryGetNode(nodeId, out var node) || node == null)
                return;
            if (string.IsNullOrEmpty(node.OwnerId))
                node.OwnerId = ownerId;
        }

        static void ApplyDefaultDiplomacy(FactionDiplomacyBoard diplomacy)
        {
            diplomacy.SetStance(
                StrategicFactionCatalog.PlayerFactionId,
                StrategicFactionCatalog.HuangcunLaborId,
                FactionStance.Hostile);
            diplomacy.SetStance(
                StrategicFactionCatalog.PlayerFactionId,
                StrategicFactionCatalog.FisherVillageId,
                FactionStance.Neutral);
            diplomacy.SetStance(
                StrategicFactionCatalog.PlayerFactionId,
                StrategicFactionCatalog.BanditId,
                FactionStance.War);
        }

        static void SeedDemoArmies(SimulationWorld world)
        {
            world.Strategic.Armies.Clear();
            if (!world.WorldGraph.TryGetNode("base:node_linjian", out _))
                return;

            world.Strategic.Armies.Register(new ArmyStack
            {
                Id = "army:bandit_patrol_1",
                FactionId = StrategicFactionCatalog.BanditId,
                DisplayName = "山匪 patrol",
                NodeId = "base:node_linjian",
                MemberCount = 3,
                CombatPower = 2
            });
        }

        public static void CaptureNodeForPlayer(SimulationWorld world, string nodeId)
        {
            if (world == null || string.IsNullOrEmpty(nodeId))
                return;
            if (!world.WorldGraph.TryGetNode(nodeId, out var node) || node == null)
                return;
            node.OwnerId = StrategicFactionCatalog.PlayerFactionId;
        }
    }
}
