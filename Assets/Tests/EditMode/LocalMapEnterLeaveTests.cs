using NUnit.Framework;
using XianXia.Core.Attributes;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Opportunity;
using XianXia.Core.Simulation;

namespace XianXia.Tests
{
    public sealed class LocalMapEnterLeaveTests
    {
        [Test]
        public void Enter_Requires_Known_Site_Then_Switches_Map()
        {
            var world = BuildCaveWorld(out var subject);
            var exploration = new ExplorationService();

            Assert.IsTrue(exploration.EnterLocalMap(world, subject.Id).IsFailure);

            subject.Get<KnownSitesComponent>().Discover(new DefinitionId("base", "site_abandoned_cave"));
            Assert.IsTrue(exploration.EnterLocalMap(world, subject.Id).IsSuccess);
            Assert.AreEqual("base:map_ch01_cave", world.LocalMap.ActiveMapLayoutId);
            Assert.IsTrue(world.LocalMap.IsInInterior);
            Assert.AreEqual("base:loc_cave_chamber", subject.Get<EntityLocationComponent>().LocationId);

            Assert.IsTrue(exploration.LeaveLocalMap(world, subject.Id).IsSuccess);
            Assert.AreEqual("base:map_ch01_reference", world.LocalMap.ActiveMapLayoutId);
            Assert.IsFalse(world.LocalMap.IsInInterior);
            Assert.AreEqual("base:loc_ref_cave", subject.Get<EntityLocationComponent>().LocationId);
        }

        [Test]
        public void Survey_Around_Uses_Double_SpiritSense_As_Radius()
        {
            var world = BuildCaveWorld(out var subject);
            var exploration = new ExplorationService();
            subject.Get<AttributesComponent>().SetBase(AttributeId.SpiritSense, 3);

            // 洞口 (10,0)；在 (0,0) 距离 10。神识3→半径6（+padding2.5=8.5）仍够不着
            Assert.IsTrue(exploration.SurveyEntrance(world, subject.Id, "0,0").IsSuccess);
            Assert.IsFalse(OpportunityEntranceRules.IsRevealed(
                world, world.WorldRegion.Locations["base:loc_ref_cave"]));

            // 神识5→半径10（+padding）可命中；或显式探针半径
            subject.Get<AttributesComponent>().SetBase(AttributeId.SpiritSense, 5);
            Assert.IsTrue(exploration.SurveyEntrance(world, subject.Id, "0,0").IsSuccess);
            Assert.IsTrue(OpportunityEntranceRules.IsRevealed(
                world, world.WorldRegion.Locations["base:loc_ref_cave"]));
            Assert.IsTrue(exploration.EnterLocalMap(world, subject.Id).IsSuccess);
        }

        [Test]
        public void Survey_MultiProbe_Hits_From_Second_Center()
        {
            var world = BuildCaveWorld(out var subject);
            subject.Get<AttributesComponent>().SetBase(AttributeId.SpiritSense, 3);
            // 第一人在远处，第二人在洞口旁（显式半径）
            Assert.IsTrue(new ExplorationService().SurveyEntrance(
                world, subject.Id, "0,0,1;9,0,3").IsSuccess);
            Assert.IsTrue(OpportunityEntranceRules.IsRevealed(
                world, world.WorldRegion.Locations["base:loc_ref_cave"]));
        }

        [Test]
        public void ExploreHere_Does_Not_Reveal_Hidden_Entrance()
        {
            var world = BuildCaveWorld(out var subject);
            subject.Get<AttributesComponent>().SetBase(AttributeId.SpiritSense, 99);
            Assert.IsTrue(new ExplorationService().ExploreHere(world, subject.Id).IsSuccess);
            Assert.IsFalse(OpportunityEntranceRules.IsRevealed(
                world, world.WorldRegion.Locations["base:loc_ref_cave"]));
        }

        static SimulationWorld BuildCaveWorld(out Entity subject)
        {
            var world = new SimulationWorld();
            world.LocalMap.EnsureOverworld("base:map_ch01_reference");
            var siteId = new DefinitionId("base", "site_abandoned_cave");
            world.RegisterOpportunitySite(new OpportunitySite(
                siteId,
                allowsCultivation: true,
                offeredManualId: null,
                nameKey: "site.abandoned_cave",
                description: "test"));

            world.WorldRegion.Register(new WorldLocationState
            {
                Id = "base:loc_ref_cave",
                Name = "废弃洞府",
                OpportunitySiteId = "base:site_abandoned_cave",
                EnterLocalMapId = "base:map_ch01_cave",
                EnterSpawnLocationId = "base:loc_cave_chamber",
                PresentationX = 10f,
                PresentationZ = 0f
            });
            world.WorldRegion.Register(new WorldLocationState
            {
                Id = "base:loc_cave_chamber",
                Name = "洞府内室",
                LocalMapId = "base:map_ch01_cave"
            });

            subject = world.Entities.CreateCharacter(
                new DefinitionId("base", "character_protagonist"), "主角").Value;
            if (!subject.TryGet<EntityLocationComponent>(out var loc))
            {
                loc = new EntityLocationComponent();
                subject.AddComponent(loc);
            }

            loc.LocationId = "base:loc_ref_cave";
            return world;
        }
    }
}
