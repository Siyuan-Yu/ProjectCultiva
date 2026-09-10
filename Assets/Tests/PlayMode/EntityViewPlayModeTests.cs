using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using XianXia.Unity.Host;
using XianXia.Core.Entities;

namespace XianXia.Tests.PlayMode
{
    public sealed class EntityViewPlayModeTests
    {
        [UnityTest]
        public IEnumerator NormalNewGame_StartsMainContinuousOutdoorWithoutAcceptanceCheat()
        {
            var host = new GameObject("ContinuousNewGameHost");
            var bootstrap = host.AddComponent<PlayableHostBootstrap>();
            host.AddComponent<HostDemoTileMap>();
            host.AddComponent<EntityViewSpawner>();
            host.AddComponent<PlayableHostCameraRig>();

            Assert.IsTrue(bootstrap.TryInitialize(), bootstrap.StatusLine);
            var runtime = bootstrap.ContinuousOutdoorSurfaceRuntime;
            Assert.IsNotNull(runtime);
            Assert.IsTrue(runtime.IsActive);
            Assert.AreEqual("base:surface_main_wilderness_v1", runtime.ActiveSurfaceId);
            Assert.Greater(runtime.LoadedChunkCount, 0);
            Assert.IsTrue(runtime.TryGetCompositeWalkGrid(out var grid));
            Assert.IsNotNull(grid);
            Assert.IsTrue(bootstrap.ViewSpawner.Registry.TryGet(
                bootstrap.Session.PlayerParty.ActiveCharacterId, out var active));
            Assert.IsNotNull(active);
            Assert.IsTrue(grid.TryWorldToCell(active.transform.position.x, active.transform.position.y, out _, out _));
            Assert.IsTrue(bootstrap.Session.World.ContinuousOutdoorMaterialization.IsSiteLoaded("base:site_huangcun"));
            Assert.Greater(bootstrap.Session.World.ContinuousOutdoorMaterialization.PlaceCount, 0);
            var npcViews = 0;
            foreach (var view in bootstrap.ViewSpawner.Registry.All)
                if (bootstrap.Session.World.Entities.TryGet(view.EntityId, out var entity) &&
                    (entity.Tags & EntityTag.Npc) != 0)
                    npcViews++;
            Assert.Greater(npcViews, 0);
            Assert.IsTrue(runtime.TryValidateStartupPostconditions(out var failure), failure);

            yield return null;
            Object.Destroy(host);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayableHost_SpawnsThreeVisibleViews()
        {
            var host = new GameObject("PlayModeHost");
            var bootstrap = host.AddComponent<PlayableHostBootstrap>();
            host.AddComponent<EntityViewSpawner>();
            host.AddComponent<PlayableHostCameraRig>();

            Assert.IsTrue(bootstrap.TryInitialize(), bootstrap.StatusLine);
            Assert.AreEqual(3, bootstrap.ViewSpawner.SpawnedCount);

            foreach (var view in bootstrap.ViewSpawner.Registry.All)
            {
                Assert.IsTrue(view.IsBound);
                Assert.IsNotNull(view.GetComponent<Renderer>());
                Assert.IsTrue(view.GetComponent<Renderer>().enabled);
            }

            yield return null;

            Object.Destroy(host);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayableHost_Rebuild_ReplacesViewsWithoutDuplicates()
        {
            var host = new GameObject("PlayModeHostRebuild");
            var bootstrap = host.AddComponent<PlayableHostBootstrap>();
            host.AddComponent<EntityViewSpawner>();

            Assert.IsTrue(bootstrap.TryInitialize());
            var firstIds = new System.Collections.Generic.List<int>();
            foreach (var view in bootstrap.ViewSpawner.Registry.All)
                firstIds.Add(view.GetInstanceID());

            Assert.IsTrue(bootstrap.TryInitialize());
            Assert.AreEqual(3, bootstrap.ViewSpawner.SpawnedCount);

            foreach (var view in bootstrap.ViewSpawner.Registry.All)
                Assert.IsFalse(firstIds.Contains(view.GetInstanceID()));

            yield return null;
            Object.Destroy(host);
            yield return null;
        }
    }
}
