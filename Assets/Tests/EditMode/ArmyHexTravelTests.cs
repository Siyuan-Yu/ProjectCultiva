using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Tests
{
    public sealed class ArmyHexTravelTests
    {
        const string FactionA = "base:faction_player";

        static SimulationWorld CreateHexWorld()
        {
            var world = new SimulationWorld();
            world.HexWorld.FillRectangle(12, 8);
            for (var q = 2; q <= 8; q++)
            {
                var tile = world.HexWorld.GetOrCreate(new HexCoord(q, 4));
                tile.IsRoad = true;
            }

            return world;
        }

        static EntityId SpawnCharacter(SimulationWorld world, string name, HexCoord hex)
        {
            var created = world.Entities.CreateCharacter(new DefinitionId("test", name), name);
            Assert.IsTrue(created.IsSuccess);
            var entity = created.Value;
            entity.Get<FactionMembershipComponent>().Assign(FactionA, FactionRoleKind.Member);
            world.WorldPresence.SetAtNode(entity.Id, "legacy:hex:" + hex);
            return entity.Id;
        }

        [Test]
        public void ARMY_HEX01_CreateArmyAtSiteHex()
        {
            var world = CreateHexWorld();
            var hex = new HexCoord(2, 4);
            var leader = SpawnCharacter(world, "Leader", hex);
            var army = ArmyService.CreateArmy(world, FactionA, "legacy:hex:" + hex, new[] { leader }).Value;
            ArmyHexTravelService.InitializeArmyAtHex(army, hex);

            Assert.IsTrue(army.UsesHexStrategicPosition);
            Assert.AreEqual(hex, army.CurrentHex);
            Assert.AreEqual(FormalArmyState.Idle, army.State);
        }

        [Test]
        public void ARMY_HEX02_ArmyMovesHexToHex()
        {
            var world = CreateHexWorld();
            var start = new HexCoord(2, 4);
            var dest = new HexCoord(6, 4);
            var leader = SpawnCharacter(world, "Leader", start);
            var army = ArmyService.CreateArmy(world, FactionA, "legacy:hex:" + start, new[] { leader }).Value;
            ArmyHexTravelService.InitializeArmyAtHex(army, start);

            Assert.IsTrue(ArmyHexTravelService.MoveArmyToHex(world, army.ArmyId, dest).IsSuccess);
            Assert.AreEqual(FormalArmyState.Moving, army.State);
            var guard = 0;
            while (army.State == FormalArmyState.Moving && guard++ < 64)
                ArmyHexTravelService.AdvanceHexTravel(world, army, 1);
            Assert.AreEqual(dest, army.CurrentHex);
            Assert.AreEqual(FormalArmyState.Idle, army.State);
        }

        [Test]
        public void ARMY_HEX03_ArmyPathAcrossMultipleHex()
        {
            var world = CreateHexWorld();
            var start = new HexCoord(2, 4);
            var dest = new HexCoord(8, 4);
            var leader = SpawnCharacter(world, "Leader", start);
            var army = ArmyService.CreateArmy(world, FactionA, "legacy:hex:" + start, new[] { leader }).Value;
            ArmyHexTravelService.InitializeArmyAtHex(army, start);

            Assert.IsTrue(ArmyHexTravelService.MoveArmyToHex(world, army.ArmyId, dest).IsSuccess);
            Assert.Greater(army.HexPathCount, 2);

            var guard = 0;
            while (army.State == FormalArmyState.Moving && guard++ < 256)
                ArmyHexTravelService.AdvanceHexTravel(world, army, 1);

            Assert.AreEqual(dest, army.CurrentHex);
        }

        [Test]
        public void ARMY_HEX04_ArmyMovementVisuallyContinuous()
        {
            var world = CreateHexWorld();
            var start = new HexCoord(2, 4);
            var dest = new HexCoord(5, 4);
            var leader = SpawnCharacter(world, "Leader", start);
            var army = ArmyService.CreateArmy(world, FactionA, "legacy:hex:" + start, new[] { leader }).Value;
            ArmyHexTravelService.InitializeArmyAtHex(army, start);
            Assert.IsTrue(ArmyHexTravelService.MoveArmyToHex(world, army.ArmyId, dest).IsSuccess);

            Assert.IsTrue(FormalArmyHexWorldPositionResolver.TryResolve(world, army, out var x0, out _));
            ArmyHexTravelService.AdvanceHexTravel(world, army, 1);
            Assert.IsTrue(FormalArmyHexWorldPositionResolver.TryResolve(world, army, out var x1, out _));
            Assert.AreNotEqual(x0, x1);
        }

        [Test]
        public void ARMY_HEX06_GarrisonAtSiteHex()
        {
            var world = CreateHexWorld();
            var hex = Ch01HexPrototypeMapBuilder.HuangcunHex;
            Ch01HexPrototypeMapBuilder.Build(world);
            var leader = SpawnCharacter(world, "Leader", hex);
            var army = ArmyService.CreateArmy(world, FactionA, "legacy:hex:" + hex, new[] { leader }).Value;
            ArmyHexTravelService.InitializeArmyAtHex(army, hex, garrisoned: true);

            Assert.AreEqual(FormalArmyState.Garrisoned, army.State);
            Assert.AreEqual(hex, army.CurrentHex);
        }
    }
}
