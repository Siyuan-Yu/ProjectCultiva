using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using XianXia.Unity.Host;

namespace XianXia.Tests.PlayMode
{
    public sealed class EntityViewPlayModeTests
    {
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
