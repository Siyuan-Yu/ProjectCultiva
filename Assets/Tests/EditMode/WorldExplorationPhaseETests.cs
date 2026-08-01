using System.IO;
using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Exploration;
using XianXia.Core.Input;
using XianXia.Core.Opportunity;
using XianXia.Core.Settlement;
using XianXia.Data.Bootstrap;
using XianXia.Data.Content;
using CoreEventType = XianXia.Core.Events.EventType;

namespace XianXia.Tests
{
    public sealed class WorldExplorationPhaseETests
    {
        static string BaseGamePath =>
            Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));

        [Test]
        public void Loader_LoadsWorldRegion()
        {
            var loaded = new ContentPackageLoader().Load(new[] { BaseGamePath });
            Assert.IsTrue(loaded.IsSuccess, loaded.IsFailure ? loaded.Error.ToString() : "");
            Assert.IsTrue(loaded.Value.Registry.TryGetWorldRegion(
                new DefinitionId("base", "region_qingshi"), out var region));
            Assert.AreEqual(4, region.Locations.Count);
        }

        [Test]
        public void PlayableDay_PlacesEntities_AndNpcAtVillage()
        {
            var started = new PlayableDayBootstrap().Start(BaseGamePath);
            Assert.IsTrue(started.IsSuccess, started.IsFailure ? started.Error.ToString() : "");
            Assert.AreEqual("base:region_qingshi", started.Value.World.WorldRegion.RegionId);
            Assert.AreEqual(4, started.Value.World.WorldRegion.Locations.Count);

            foreach (var id in started.Value.CharacterIds)
            {
                Assert.IsTrue(started.Value.World.Entities.TryGet(id, out var e));
                Assert.IsTrue(e.TryGet<EntityLocationComponent>(out var loc));
                Assert.AreEqual("base:loc_labor_camp", loc.LocationId);
            }

            Assert.IsTrue(started.Value.World.Entities.TryGet(
                started.Value.RecruitableNpcId, out var npc));
            Assert.AreEqual("base:loc_village_edge", npc.Get<EntityLocationComponent>().LocationId);
        }

        [Test]
        public void Travel_And_Explore_YieldResourceAndSite()
        {
            var started = new PlayableDayBootstrap().Start(BaseGamePath);
            Assert.IsTrue(started.IsSuccess);
            var actor = started.Value.CharacterIds[0];
            var port = started.Value.Port;
            var world = started.Value.World;

            Assert.IsTrue(world.Settlements.TryGetPrimary(out var settlement));
            var herbBefore = settlement.GetStock("base:resource_spirit_herb");

            Assert.IsTrue(port.Submit(new PlayerCommandRequest(
                actor, PlayerCommandKind.Travel, 1, EntityId.None, WorkRoleKind.None,
                "base:loc_cave_mouth")).IsSuccess);

            Assert.AreEqual(
                "base:loc_cave_mouth",
                world.Entities.TryGet(actor, out var e1)
                    ? e1.Get<EntityLocationComponent>().LocationId
                    : "");

            Assert.IsTrue(port.Submit(new PlayerCommandRequest(
                actor, PlayerCommandKind.Explore, 1)).IsSuccess);

            Assert.IsTrue(world.Entities.TryGet(actor, out var e2));
            Assert.IsTrue(e2.Get<KnownSitesComponent>().Knows(
                new DefinitionId("base", "site_abandoned_cave")));

            Assert.IsTrue(port.Submit(new PlayerCommandRequest(
                actor, PlayerCommandKind.Travel, 1, EntityId.None, WorkRoleKind.None,
                "base:loc_herb_slope")).IsSuccess);
            Assert.IsTrue(port.Submit(new PlayerCommandRequest(
                actor, PlayerCommandKind.Explore, 1)).IsSuccess);
            Assert.AreEqual(herbBefore + 2, settlement.GetStock("base:resource_spirit_herb"));

            var events = world.Events.Drain();
            Assert.IsTrue(events.Exists(ev => ev.Type == CoreEventType.LocationChanged));
            Assert.IsTrue(events.Exists(ev => ev.Type == CoreEventType.LocationExplored));
            Assert.IsTrue(events.Exists(ev => ev.Type == CoreEventType.OpportunitySiteDiscovered));
        }
    }
}
