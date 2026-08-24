using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Tests
{
    public sealed class ArmyHexBattleAnchorTests
    {
        const string FactionA = StrategicFactionCatalog.PlayerFactionId;
        const string NodeA = "base:node_huangcun";

        static SimulationWorld CreateWorld()
        {
            var world = new SimulationWorld();
            Ch01HexPrototypeMapBuilder.BuildMinimalTwoSitePrototype(world);
            return world;
        }

        [Test]
        public void H7_ParkArmyAtBattleAnchor_UsesHexNotRoute()
        {
            var world = CreateWorld();
            var leader = Spawn(world, "L");
            var army = ArmyService.CreateArmy(world, FactionA, NodeA, new[] { leader }).Value;
            ArmyHexTravelService.InitializeArmyAtHex(army, Ch01HexPrototypeMapBuilder.HuangcunHex);

            var snap = new BattleParticipantSnapshot();
            ArmyHexBattleAnchorService.SetBattleAnchorHex(snap, Ch01HexPrototypeMapBuilder.QingyunLuHex);
            snap.BattleAnchorNodeId = NodeA;

            ArmyHexBattleAnchorService.ParkArmyAtBattleAnchor(world, army, snap);

            Assert.IsTrue(army.UsesHexStrategicPosition);
            Assert.AreEqual(Ch01HexPrototypeMapBuilder.QingyunLuHex, army.CurrentHex);
            Assert.AreEqual(string.Empty, army.RouteId);
            Assert.AreEqual(-1f, army.RouteAnchorProgress);
        }

        [Test]
        public void H6_AdjacentHexContact_DetectsBattleRange()
        {
            var world = CreateWorld();
            var pursuer = MakeArmy(world, Ch01HexPrototypeMapBuilder.HuangcunHex);
            var target = MakeArmy(world, HexMath.Neighbor(Ch01HexPrototypeMapBuilder.HuangcunHex, 0));

            Assert.IsTrue(ArmyHexBattleAnchorService.TryDetectHexContact(pursuer, target));
        }

        [Test]
        public void H8_RouteMovement_RejectedWhenHexActive()
        {
            var world = CreateWorld();
            var leader = Spawn(world, "L");
            var army = ArmyService.CreateArmy(world, FactionA, NodeA, new[] { leader }).Value;
            ArmyHexTravelService.InitializeArmyAtHex(army, Ch01HexPrototypeMapBuilder.HuangcunHex);

            var result = ArmyHexCommandService.MoveArmyToSite(world, army.ArmyId, "base:node_qingyun_lu");
            Assert.IsFalse(result.IsSuccess);
        }

        static EntityId Spawn(SimulationWorld world, string name)
        {
            var created = world.Entities.CreateCharacter(new DefinitionId("test", name), name);
            created.Value.Get<FactionMembershipComponent>().Assign(FactionA, FactionRoleKind.Member);
            world.WorldPresence.SetAtNode(created.Value.Id, NodeA);
            return created.Value.Id;
        }

        static FormalArmy MakeArmy(SimulationWorld world, HexCoord hex)
        {
            var leader = Spawn(world, "X" + hex);
            var army = ArmyService.CreateArmy(world, FactionA, NodeA, new[] { leader }).Value;
            ArmyHexTravelService.InitializeArmyAtHex(army, hex);
            return army;
        }
    }
}
