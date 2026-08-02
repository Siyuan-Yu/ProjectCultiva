using NUnit.Framework;
using UnityEngine;
using XianXia.Core.Actions;
using XianXia.Core.Input;
using XianXia.Unity.Host;

namespace XianXia.Tests
{
    /// <summary>PKG-A/B smoke: 2D Sprite views + Stop／敛息草.</summary>
    public sealed class DemoParityHostPresentationTests
    {
        [Test]
        public void Spawner_UsesSpriteRenderer_NotCapsuleMesh()
        {
            var host = CreateHost(out var bootstrap, out _);
            try
            {
                Assert.Greater(bootstrap.ViewSpawner.SpawnedCount, 0);
                foreach (var view in bootstrap.ViewSpawner.Registry.All)
                {
                    Assert.IsNotNull(view.GetComponent<SpriteRenderer>());
                    Assert.IsNull(view.GetComponent<MeshFilter>());
                    Assert.AreEqual(0f, view.transform.position.z, 0.001f);
                }

                Assert.Greater(bootstrap.GetComponent<HostMapGraybox>().ZoneCount, 0);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Stop_CancelsActiveLabor()
        {
            var host = CreateHost(out var bootstrap, out _);
            try
            {
                var id = bootstrap.Session.CharacterIds[0];
                Assert.AreEqual(1, bootstrap.CommandBridge.IssueTo(new[] { id }, PlayerCommandKind.Labor));
                Assert.IsInstanceOf<LaborAction>(ActiveOf(bootstrap, id));
                Assert.AreEqual(1, bootstrap.CommandBridge.IssueTo(new[] { id }, PlayerCommandKind.Stop, 0));
                Assert.IsNull(ActiveOf(bootstrap, id));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Labor_AtResourceLocation_AddsStock()
        {
            var host = CreateHost(out var bootstrap, out _);
            try
            {
                var id = bootstrap.Session.CharacterIds[0];
                Assert.IsTrue(bootstrap.Session.World.Entities.TryGet(id, out var entity));
                Assert.IsTrue(entity.TryGet<XianXia.Core.Exploration.EntityLocationComponent>(out var loc));
                loc.LocationId = "base:loc_ref_forest";
                Assert.IsTrue(bootstrap.Session.World.Settlements.TryGetPrimary(out var s));
                var before = s.GetStock("base:resource_rough_wood");
                Assert.AreEqual(1, bootstrap.CommandBridge.IssueTo(new[] { id }, PlayerCommandKind.Labor));
                for (var i = 0; i < (int)HostCommandBridge.DefaultDurationTicks; i++)
                    Assert.IsTrue(bootstrap.Session.TickOnce().IsSuccess);
                Assert.Greater(s.GetStock("base:resource_rough_wood"), before);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ConcealGrass_LowersRisk()
        {
            var host = CreateHost(out var bootstrap, out _);
            try
            {
                var id = bootstrap.Session.CharacterIds[0];
                Assert.IsTrue(bootstrap.Session.World.Entities.TryGet(id, out var entity));
                var risk = entity.Get<XianXia.Core.Concealment.PersonalConcealmentRiskComponent>();
                risk.Value = 40;
                Assert.IsTrue(bootstrap.Session.World.Settlements.TryGetPrimary(out var s));
                Assert.GreaterOrEqual(s.GetStock("base:resource_conceal_grass"), 1);
                Assert.AreEqual(
                    1,
                    bootstrap.CommandBridge.IssueTo(new[] { id }, PlayerCommandKind.UseConcealGrass, 0));
                Assert.AreEqual(25, risk.Value);
                Assert.AreEqual(2, s.GetStock("base:resource_conceal_grass"));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        static IAction ActiveOf(PlayableHostBootstrap bootstrap, XianXia.Core.Domain.Ids.EntityId id)
        {
            if (!bootstrap.Session.World.Entities.TryGet(id, out var entity))
                return null;
            var actionId = entity.Get<XianXia.Core.Entities.ActionStateComponent>().ActiveActionId;
            if (actionId.IsNone)
                return null;
            return bootstrap.Session.World.ActiveActions.TryGetValue(actionId, out var action) ? action : null;
        }

        static GameObject CreateHost(out PlayableHostBootstrap bootstrap, out Camera cam)
        {
            var camGo = new GameObject("ParityCam");
            cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 12f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.pixelRect = new Rect(0f, 0f, 800f, 600f);

            var host = new GameObject("ParityHost");
            bootstrap = host.AddComponent<PlayableHostBootstrap>();
            bootstrap.ConfigureOpeningScenario("base:scenario_ch01_reference");
            host.AddComponent<EntityViewSpawner>();
            host.AddComponent<HostSelectionController>();
            host.AddComponent<HostCommandBridge>();
            host.AddComponent<HostMapGraybox>();
            Assert.IsTrue(bootstrap.TryInitialize(), bootstrap.StatusLine);
            bootstrap.SelectionController.Bind(bootstrap.ViewSpawner, cam);
            bootstrap.SelectionController.SetPartyFilter(bootstrap.Session.CharacterIds);
            bootstrap.CommandBridge.Bind(bootstrap.Session, bootstrap.SelectionController);
            camGo.transform.SetParent(host.transform, true);
            return host;
        }
    }
}
