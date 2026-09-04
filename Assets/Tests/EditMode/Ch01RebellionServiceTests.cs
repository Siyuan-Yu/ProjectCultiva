using NUnit.Framework;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Tests.EditMode
{
    public sealed class Ch01RebellionServiceTests
    {
        [Test]
        public void TryBegin_ReleasesVassalageAndDeclaresWar()
        {
            var world = new SimulationWorld();
            world.Strategic.Ch01FormationScenarioCompat = true;
            world.Strategic.PlayerFactionId = StrategicFactionCatalog.PlayerFactionId;
            world.PartyWorld.SiteId = Ch01ScenarioProgressionHooks.HuangcunSiteId;
            Assert.IsTrue(world.Strategic.Vassalages.TryBindVassalage(
                StrategicFactionCatalog.PlayerFactionId,
                StrategicFactionCatalog.HuangcunLaborId));

            var character = world.Entities.CreateCharacter(
                new DefinitionId("test", "rebel"), "起事者").Value;
            character.Get<CultivationComponent>().Realm = RealmStage.QiRefining;
            var party = new PlayerPartyRuntime();
            Assert.IsTrue(party.TryInitialize(character.Id, out _));

            var result = Ch01RebellionService.TryBegin(world, party);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsFalse(world.Strategic.Vassalages.IsVassal(StrategicFactionCatalog.PlayerFactionId));
            Assert.IsTrue(WarGateService.IsAtWar(
                world,
                StrategicFactionCatalog.PlayerFactionId,
                StrategicFactionCatalog.HuangcunLaborId));
        }
    }
}
