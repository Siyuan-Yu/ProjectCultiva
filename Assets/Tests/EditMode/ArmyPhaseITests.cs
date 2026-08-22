using NUnit.Framework;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;

namespace XianXia.Tests
{
    public sealed class ArmyPhaseITests
    {
        const string FactionA = "test:faction_a";
        const string FactionB = "test:faction_b";
        const string FactionC = "test:faction_c";

        [Test]
        public void Alliance_UniqueMembership()
        {
            var world = new SimulationWorld();
            Assert.IsTrue(world.Strategic.Alliances.FormAlliance(FactionA, FactionC, out _));
            Assert.IsFalse(world.Strategic.Alliances.FormAlliance(FactionA, FactionB, out _));
        }

        [Test]
        public void Vassal_CannotFormAlliance()
        {
            var world = new SimulationWorld();
            Assert.IsTrue(world.Strategic.Vassalages.TryBindVassalage(FactionB, FactionA));
            Assert.IsFalse(world.Strategic.Alliances.FormAlliance(FactionB, FactionC, out _));
        }

        [Test]
        public void AllianceWarBinding_OnDeclareWar()
        {
            var world = new SimulationWorld();
            Assert.IsTrue(world.Strategic.Alliances.FormAlliance(FactionA, FactionC, out _));
            WarGateService.DeclareWar(world, FactionA, FactionB);
            Assert.IsTrue(WarGateService.IsAtWar(world, FactionC, FactionB));
        }

        [Test]
        public void TributeHook_ReturnsPlaceholder()
        {
            var world = new SimulationWorld();
            Assert.IsTrue(TributeService.TryCollectTribute(world, FactionB, FactionA, out var amount).IsSuccess);
            Assert.AreEqual(TributeService.PlaceholderTributeAmount, amount);
        }
    }
}
