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
    public sealed class ArmyHexCommandTests
    {
        const string NodeHuangcun = "base:node_huangcun";
        const string NodeQingyunLu = "base:node_qingyun_lu";
        const string FactionA = StrategicFactionCatalog.PlayerFactionId;
        const string FactionB = StrategicFactionCatalog.BanditId;

        static SimulationWorld CreateWorld()
        {
            var world = new SimulationWorld();
            Ch01HexPrototypeMapBuilder.Build(world);
            world.Strategic.PlayerFactionId = FactionA;
            return world;
        }

        static (FormalArmy attacker, FormalArmy defender, ArmyStack stack) CreateCombatants(SimulationWorld world)
        {
            var attackerLeader = Spawn(world, "A", FactionA, NodeHuangcun);
            var attacker = ArmyService.CreateArmy(
                world,
                FactionA,
                NodeHuangcun,
                new[] { attackerLeader }).Value;
            ArmyHexTravelService.InitializeArmyAtHex(attacker, Ch01HexPrototypeMapBuilder.HuangcunHex);

            var defenderLeader = Spawn(world, "D", FactionB, NodeQingyunLu);
            var defender = ArmyService.CreateArmy(
                world,
                FactionB,
                NodeQingyunLu,
                new[] { defenderLeader }).Value;
            ArmyHexTravelService.InitializeArmyAtHex(defender, Ch01HexPrototypeMapBuilder.QingyunLuHex);

            var stack = new ArmyStack
            {
                Id = "army:test_enemy",
                FormalArmyId = defender.ArmyId,
                FactionId = FactionB,
                DisplayName = "Enemy",
                NodeId = NodeQingyunLu
            };
            world.Strategic.Armies.Register(stack);
            ArmyStackAdapter.SyncStackTravelFromFormalArmy(world, stack);
            WarGateService.DeclareWar(world, FactionA, FactionB);
            return (attacker, defender, stack);
        }

        static EntityId Spawn(SimulationWorld world, string name, string faction, string nodeId)
        {
            var created = world.Entities.CreateCharacter(new DefinitionId("test", name), name);
            var entity = created.Value;
            entity.Get<FactionMembershipComponent>().Assign(faction, FactionRoleKind.Member);
            world.WorldPresence.SetAtNode(entity.Id, nodeId);
            return entity.Id;
        }

        [Test]
        public void HEX_CMD02_RightClickHexMove_BuildsHexPathWithoutRoute()
        {
            var world = CreateWorld();
            var leader = Spawn(world, "P", FactionA, NodeHuangcun);
            var army = ArmyService.CreateArmy(world, FactionA, NodeHuangcun, new[] { leader }).Value;
            ArmyHexTravelService.InitializeArmyAtHex(army, Ch01HexPrototypeMapBuilder.HuangcunHex);

            var dest = Ch01HexPrototypeMapBuilder.QingyunLuHex;
            var path = new System.Collections.Generic.List<HexCoord>();
            Assert.IsTrue(ArmyHexCommandService.TryBuildPathPreview(world, army, dest, path));
            Assert.Greater(path.Count, 1);
            Assert.IsTrue(ArmyHexCommandService.MoveArmy(world, army.ArmyId, dest).IsSuccess);
            Assert.AreEqual(FormalArmyState.Moving, army.State);
        }

        [Test]
        public void HEX_CMD04_AttackMovingArmy_PursuesByTargetArmyId()
        {
            var world = CreateWorld();
            var (attacker, defender, stack) = CreateCombatants(world);
            ArmyHexTravelService.MoveArmyToHex(world, defender.ArmyId, new HexCoord(12, 7));

            Assert.IsTrue(ArmyHexCommandService.AttackStack(world, attacker.ArmyId, stack).IsSuccess);
            Assert.AreEqual(defender.ArmyId, world.Strategic.Encounter.PursueDefenderArmyId);
            Assert.AreEqual(attacker.ArmyId, world.Strategic.Encounter.PursueAttackerArmyId);
        }

        [Test]
        public void HEX_CMD05_NewMoveCancelsPursuit()
        {
            var world = CreateWorld();
            var (attacker, defender, stack) = CreateCombatants(world);
            Assert.IsTrue(ArmyHexCommandService.AttackStack(world, attacker.ArmyId, stack).IsSuccess);

            var newDest = new HexCoord(5, 5);
            Assert.IsTrue(ArmyHexCommandService.MoveArmy(world, attacker.ArmyId, newDest).IsSuccess);
            Assert.IsTrue(string.IsNullOrEmpty(world.Strategic.Encounter.PursueDefenderArmyId));
            Assert.AreEqual(newDest, attacker.DestinationHex);
        }
    }
}
