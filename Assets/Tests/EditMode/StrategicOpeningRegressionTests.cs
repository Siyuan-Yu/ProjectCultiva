using NUnit.Framework;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;

namespace XianXia.Tests
{
    /// <summary>Strategic Opening 的最小 Core 回归：WarBoard 权威、缓存投影与旧 fixture 不可回退。</summary>
    public sealed class StrategicOpeningRegressionTests
    {
        const string Player = "base:faction_player";
        const string Huangcun = "base:sect_huangcun_labor";
        const string Bandits = "base:faction_bandits";

        [Test]
        public void CoreCh01Fixture_StrategicDefaultsRemainCompatible()
        {
            var world = new SimulationWorld();
            Assert.IsTrue(StrategicBootstrap.ApplyCh01Defaults(world).IsSuccess);
            Assert.AreEqual(Player, world.Strategic.PlayerFactionId);
            Assert.IsTrue(world.Strategic.Vassalages.TryGetOverlord(Player, out var overlord));
            Assert.AreEqual(Huangcun, overlord);
            Assert.IsTrue(WarGateService.IsAtWar(world, Player, Bandits));
            Assert.IsTrue(WarGateService.IsAtWar(world, Huangcun, Bandits));
        }

        [Test]
        public void RebuildWarStances_ClearsStaleWar()
        {
            var world = new SimulationWorld();
            world.Strategic.Diplomacy.SetStance(Player, Bandits, FactionStance.War);
            StrategicDiplomacyProjection.RebuildWarStances(world);
            Assert.AreNotEqual(FactionStance.War, world.Strategic.Diplomacy.GetStance(Player, Bandits));
            Assert.IsFalse(WarGateService.IsAtWar(world, Player, Bandits));
        }

        [Test]
        public void RebuildWarStances_ProjectsActiveWars()
        {
            var world = new SimulationWorld();
            Assert.IsTrue(WarGateService.DeclareWar(world, Player, Bandits).IsSuccess);
            world.Strategic.Diplomacy.Clear();
            StrategicDiplomacyProjection.RebuildWarStances(world);
            Assert.IsTrue(WarGateService.IsAtWar(world, Player, Bandits));
            Assert.AreEqual(FactionStance.War, world.Strategic.Diplomacy.GetStance(Player, Bandits));
        }
    }
}
