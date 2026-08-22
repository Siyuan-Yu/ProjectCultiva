using NUnit.Framework;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;

namespace XianXia.Tests
{
    public sealed class ArmyPhaseGTests
    {
        const string FactionA = "test:faction_a";
        const string FactionB = "test:faction_b";

        [Test]
        public void WarGate_BlocksAttackWithoutWar()
        {
            var world = new SimulationWorld();
            Assert.IsFalse(WarGateService.CanAttack(world, FactionA, FactionB));
        }

        [Test]
        public void WarGate_AllowsAttackWhenAtWar()
        {
            var world = new SimulationWorld();
            WarGateService.DeclareWar(world, FactionA, FactionB);
            Assert.IsTrue(WarGateService.CanAttack(world, FactionA, FactionB));
        }

        [Test]
        public void DeclareWar_SetsActiveWarBetweenFactions()
        {
            var world = new SimulationWorld();
            Assert.IsTrue(WarGateService.DeclareWar(world, FactionA, FactionB).IsSuccess);
            Assert.IsTrue(WarGateService.IsAtWar(world, FactionA, FactionB));
        }

        [Test]
        public void IsAtWar_ReturnsCorrectState()
        {
            var world = new SimulationWorld();
            Assert.IsFalse(WarGateService.IsAtWar(world, FactionA, FactionB));
            WarGateService.DeclareWar(world, FactionA, FactionB);
            Assert.IsTrue(WarGateService.IsAtWar(world, FactionA, FactionB));
            Assert.IsTrue(WarGateService.IsAtWar(world, FactionB, FactionA));
        }
    }
}
