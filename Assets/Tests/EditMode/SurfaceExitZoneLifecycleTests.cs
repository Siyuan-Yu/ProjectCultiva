using NUnit.Framework;
using UnityEngine;
using XianXia.Core.World.Strategic;
using XianXia.Unity.Host;

namespace XianXia.Tests
{
    public sealed class SurfaceExitZoneLifecycleTests
    {
        static PlayableHostBootstrap CreateCh01Bootstrap(out GameObject hostGo)
        {
            hostGo = new GameObject("SurfaceExitLifecycleHost");
            var bootstrap = hostGo.AddComponent<PlayableHostBootstrap>();
            bootstrap.ConfigureOpeningScenario("base:scenario_ch01_reference");
            hostGo.AddComponent<EntityViewSpawner>();
            hostGo.AddComponent<HostSelectionController>();
            hostGo.AddComponent<HostCommandBridge>();
            hostGo.AddComponent<HostMapGraybox>();
            hostGo.AddComponent<HostMoveController>();
            return bootstrap;
        }

        [Test]
        public void NewGameStartingInsideSurfaceWorldSiteShowsExitZones()
        {
            var hostGo = (GameObject)null;
            try
            {
                var bootstrap = CreateCh01Bootstrap(out hostGo);
                Assert.IsTrue(bootstrap.TryInitialize(), bootstrap.StatusLine);

                var presenter = hostGo.GetComponent<HostSurfaceExitZonePresenter>();
                Assert.IsNotNull(presenter);
                Assert.Greater(presenter.VisibleZoneCount, 0);
            }
            finally
            {
                if (hostGo != null)
                    Object.DestroyImmediate(hostGo);
            }
        }

        [Test]
        public void InitialSurfaceMapMaterializationBuildsExitPresentation()
        {
            var hostGo = (GameObject)null;
            try
            {
                var bootstrap = CreateCh01Bootstrap(out hostGo);
                Assert.IsTrue(bootstrap.TryInitialize(), bootstrap.StatusLine);

                var presenter = hostGo.GetComponent<HostSurfaceExitZonePresenter>();
                Assert.IsNotNull(presenter);
                Assert.IsFalse(string.IsNullOrWhiteSpace(presenter.CachedMapLayoutId));
                Assert.Greater(presenter.CachedExitTriggerDepth, 0.01f);
                Assert.Greater(presenter.VisibleZoneCount, 0);
            }
            finally
            {
                if (hostGo != null)
                    Object.DestroyImmediate(hostGo);
            }
        }

        [Test]
        public void ReturningToSameSurfaceMapProducesSameExitGeometry()
        {
            var hostGo = (GameObject)null;
            try
            {
                var bootstrap = CreateCh01Bootstrap(out hostGo);
                Assert.IsTrue(bootstrap.TryInitialize(), bootstrap.StatusLine);

                var presenter = hostGo.GetComponent<HostSurfaceExitZonePresenter>();
                var firstCount = presenter.VisibleZoneCount;
                var firstDepth = presenter.CachedExitTriggerDepth;
                var firstMap = presenter.CachedMapLayoutId;

                bootstrap.ActivateSurfaceLocalMapPresentation();

                Assert.AreEqual(firstCount, presenter.VisibleZoneCount);
                Assert.AreEqual(firstDepth, presenter.CachedExitTriggerDepth, 0.0001f);
                Assert.AreEqual(firstMap, presenter.CachedMapLayoutId);
            }
            finally
            {
                if (hostGo != null)
                    Object.DestroyImmediate(hostGo);
            }
        }

        [Test]
        public void InitialAndReenteredSiteHaveSameZoneCountAndBounds()
        {
            var hostGo = (GameObject)null;
            try
            {
                var bootstrap = CreateCh01Bootstrap(out hostGo);
                Assert.IsTrue(bootstrap.TryInitialize(), bootstrap.StatusLine);

                var presenter = hostGo.GetComponent<HostSurfaceExitZonePresenter>();
                var initialCount = presenter.VisibleZoneCount;
                var initialDepth = presenter.CachedExitTriggerDepth;

                presenter.Clear();
                Assert.AreEqual(0, presenter.VisibleZoneCount);

                bootstrap.ActivateSurfaceLocalMapPresentation();
                Assert.AreEqual(initialCount, presenter.VisibleZoneCount);
                Assert.AreEqual(initialDepth, presenter.CachedExitTriggerDepth, 0.0001f);
            }
            finally
            {
                if (hostGo != null)
                    Object.DestroyImmediate(hostGo);
            }
        }

        [Test]
        public void SurfaceExitPresentationDoesNotRequirePriorWorldTransition()
        {
            var hostGo = (GameObject)null;
            try
            {
                var bootstrap = CreateCh01Bootstrap(out hostGo);
                Assert.IsTrue(bootstrap.TryInitialize(), bootstrap.StatusLine);

                var world = bootstrap.Session.World;
                Assert.IsFalse(world.LocalMap.IsInInterior);
                Assert.IsTrue(SurfaceExitZoneCalculator.ShouldPresent(world));

                var presenter = hostGo.GetComponent<HostSurfaceExitZonePresenter>();
                Assert.Greater(presenter.VisibleZoneCount, 0);
            }
            finally
            {
                if (hostGo != null)
                    Object.DestroyImmediate(hostGo);
            }
        }

        [Test]
        public void InteriorInitialMaterializationStillShowsNoSurfaceExitZones()
        {
            var hostGo = (GameObject)null;
            try
            {
                var bootstrap = CreateCh01Bootstrap(out hostGo);
                Assert.IsTrue(bootstrap.TryInitialize(), bootstrap.StatusLine);

                var world = bootstrap.Session.World;
                world.LocalMap.OverworldMapLayoutId = world.LocalMap.ActiveMapLayoutId;
                world.LocalMap.ActiveMapLayoutId = "base:map_cave_interior";

                bootstrap.ActivateSurfaceLocalMapPresentation();

                var presenter = hostGo.GetComponent<HostSurfaceExitZonePresenter>();
                Assert.IsNotNull(presenter);
                Assert.AreEqual(0, presenter.VisibleZoneCount);
            }
            finally
            {
                if (hostGo != null)
                    Object.DestroyImmediate(hostGo);
            }
        }
    }
}
