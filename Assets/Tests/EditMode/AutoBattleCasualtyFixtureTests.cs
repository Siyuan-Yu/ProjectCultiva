using System.Collections.Generic;
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
    public sealed class AutoBattleCasualtyFixtureTests
    {
        const string FactionA = "test:faction_a";

        [Test]
        public void CasualtyTestBandit_AutoWin_GuaranteesOnePlayerIncapacitatedOrKilled()
        {
            var world = new SimulationWorld();
            world.Strategic.PlayerFactionId = FactionA;
            Ch01HexPrototypeMapBuilder.Build(world);
            Ch01ScenarioStrategicSetup.Apply(world);

            Assert.IsTrue(world.Strategic.Armies.TryGet(
                ArmyStackAdapter.BanditCasualtyTestStackId, out var enemyStack));

            var a = Spawn(world, "A");
            var b = Spawn(world, "B");
            world.WorldPresence.SetAtSite(a, Ch01HexPrototypeMapBuilder.SiteHuangcun);
            world.WorldPresence.SetAtSite(b, Ch01HexPrototypeMapBuilder.SiteHuangcun);
            var party = new List<EntityId> { a, b };

            var report = AutoBattleCasualtyService.ApplyPlayerVictory(
                world,
                party,
                enemyStack,
                playerPower: 6,
                enemyPower: 30,
                executeOnWin: true);

            Assert.IsTrue(report.CasualtyTestFixtureApplied);
            Assert.AreEqual(1, report.PlayerIncapacitated + report.PlayerKilled);
        }

        static EntityId Spawn(SimulationWorld world, string name)
        {
            var created = world.Entities.CreateCharacter(new DefinitionId("test", name), name);
            Assert.IsTrue(created.IsSuccess);
            created.Value.Get<FactionMembershipComponent>().Assign(FactionA, FactionRoleKind.Member);
            return created.Value.Id;
        }
    }
}
