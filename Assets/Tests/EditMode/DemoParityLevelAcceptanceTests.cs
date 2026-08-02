using System.IO;
using NUnit.Framework;
using UnityEngine;
using XianXia.Core.Input;
using XianXia.Data.Bootstrap;
using XianXia.Unity.Host;

namespace XianXia.Tests
{
    /// <summary>
    /// Demo [49]§5 playable checklist — automated subset on PlayableHost + Core.
    /// Attack placeholder intentionally Out.
    /// </summary>
    public sealed class DemoParityLevelAcceptanceTests
    {
        static string BaseGamePath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Content", "BaseGame"));

        [Test]
        public void DemoLayout_LocationsMatchDemoZoneCenters()
        {
            var started = new PlayableDayBootstrap().Start(
                BaseGamePath,
                new PlayableDayOptions { OpeningScenarioId = "base:scenario_ch01_reference" });
            Assert.IsTrue(started.IsSuccess, started.IsFailure ? started.Error.ToString() : "");
            var region = started.Value.World.WorldRegion;
            Assert.IsTrue(region.TryGet("base:loc_ref_forest", out var forest));
            Assert.AreEqual(-34f, forest.PresentationX, 0.01f);
            Assert.IsTrue(region.TryGet("base:loc_ref_herb_field", out var herb));
            Assert.AreEqual(-3f, herb.PresentationX, 0.01f);
            Assert.AreEqual(-15f, herb.PresentationZ, 0.01f);
            Assert.IsTrue(region.TryGet("base:loc_ref_labor_yard", out var farm));
            Assert.AreEqual(20f, farm.PresentationX, 0.01f);
        }

        [Test]
        public void Host_BuildsDemoTileMap_AndSpriteCast()
        {
            var host = CreateHost(out var bootstrap);
            try
            {
                var tiles = bootstrap.GetComponent<HostDemoTileMap>();
                Assert.IsNotNull(tiles);
                Assert.Greater(tiles.TileCount, 100);
                Assert.AreEqual(3, bootstrap.Session.CharacterIds.Count);
                Assert.GreaterOrEqual(bootstrap.ViewSpawner.SpawnedCount, 8);
                foreach (var view in bootstrap.ViewSpawner.Registry.All)
                    Assert.IsNotNull(view.GetComponent<SpriteRenderer>());
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Checklist_ThreeRoles_IndependentLaborAndCultivate()
        {
            var host = CreateHost(out var bootstrap);
            try
            {
                var a = bootstrap.Session.CharacterIds[0];
                var b = bootstrap.Session.CharacterIds[1];
                var c = bootstrap.Session.CharacterIds[2];
                Snap(bootstrap, a, "base:loc_ref_forest");
                Snap(bootstrap, b, "base:loc_ref_labor_yard");
                Snap(bootstrap, c, "base:loc_ref_cave");
                Assert.IsTrue(bootstrap.Session.World.Entities.TryGet(c, out var cultivator));
                Assert.IsTrue(cultivator.TryGet<XianXia.Core.Opportunity.KnownSitesComponent>(out var known));
                Assert.IsTrue(XianXia.Core.Domain.Ids.DefinitionId.TryParse("base:site_abandoned_cave", out var siteId));
                known.Discover(siteId);

                Assert.AreEqual(1, bootstrap.CommandBridge.IssueTo(new[] { a }, PlayerCommandKind.Labor));
                Assert.AreEqual(1, bootstrap.CommandBridge.IssueTo(new[] { b }, PlayerCommandKind.Labor));
                Assert.AreEqual(1, bootstrap.CommandBridge.IssueTo(new[] { c }, PlayerCommandKind.Cultivate));

                Assert.IsTrue(bootstrap.Session.World.Settlements.TryGetPrimary(out var s));
                var woodBefore = s.GetStock("base:resource_rough_wood");
                for (var i = 0; i < (int)HostCommandBridge.DefaultDurationTicks; i++)
                    Assert.IsTrue(bootstrap.Session.TickOnce().IsSuccess);
                Assert.Greater(s.GetStock("base:resource_rough_wood"), woodBefore);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        static void Snap(PlayableHostBootstrap bootstrap, XianXia.Core.Domain.Ids.EntityId id, string loc)
        {
            Assert.IsTrue(bootstrap.Session.World.Entities.TryGet(id, out var e));
            Assert.IsTrue(e.TryGet<XianXia.Core.Exploration.EntityLocationComponent>(out var c));
            c.LocationId = loc;
        }

        static GameObject CreateHost(out PlayableHostBootstrap bootstrap)
        {
            var camGo = new GameObject("ParityAcceptCam");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 12f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.pixelRect = new Rect(0f, 0f, 800f, 600f);

            var host = new GameObject("ParityAcceptHost");
            bootstrap = host.AddComponent<PlayableHostBootstrap>();
            bootstrap.ConfigureOpeningScenario("base:scenario_ch01_reference");
            host.AddComponent<EntityViewSpawner>();
            host.AddComponent<HostSelectionController>();
            host.AddComponent<HostCommandBridge>();
            host.AddComponent<HostMapGraybox>();
            host.AddComponent<HostDemoTileMap>();
            Assert.IsTrue(bootstrap.TryInitialize(), bootstrap.StatusLine);
            bootstrap.SelectionController.Bind(bootstrap.ViewSpawner, cam);
            bootstrap.CommandBridge.Bind(bootstrap.Session, bootstrap.SelectionController);
            camGo.transform.SetParent(host.transform, true);
            return host;
        }
    }
}
