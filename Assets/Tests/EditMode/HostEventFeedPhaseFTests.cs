using NUnit.Framework;
using UnityEngine;
using XianXia.Core.Input;
using XianXia.Unity.Host;

namespace XianXia.Tests
{
    public sealed class HostEventFeedPhaseFTests
    {
        [Test]
        public void Init_PullsBootstrapEvents()
        {
            var host = CreateHost(out var bootstrap);
            try
            {
                Assert.Greater(bootstrap.EventFeed.TotalPulled, 0);
                Assert.IsTrue(bootstrap.EventFeed.ContainsTypeName("WorldInitialized") ||
                              bootstrap.EventFeed.ContainsTypeName("EntityCreated"));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Observe_ShowsDiscoveryEvents()
        {
            var host = CreateHost(out var bootstrap);
            try
            {
                var id = bootstrap.Session.CharacterIds[0];
                bootstrap.CommandBridge.IssueTo(new[] { id }, PlayerCommandKind.Observe, 2);
                bootstrap.StepTick();
                bootstrap.StepTick();

                Assert.IsTrue(
                    bootstrap.EventFeed.ContainsTypeName("ObservationResolved") ||
                    bootstrap.EventFeed.ContainsTypeName("OpportunitySiteDiscovered") ||
                    bootstrap.EventFeed.ContainsTypeName("ActionCompleted"),
                    bootstrap.EventFeed.ToDebugText());
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Override_ShowsScheduleInterrupted()
        {
            var host = CreateHost(out var bootstrap);
            try
            {
                var id = bootstrap.Session.CharacterIds[0];
                for (var i = 0; i < 9; i++)
                    bootstrap.StepTick();

                bootstrap.CommandBridge.IssueTo(new[] { id }, PlayerCommandKind.Rest);
                // Interrupt publishes immediately on Submit — pull residual queue.
                bootstrap.EventFeed.PullFrom(bootstrap.Session.World.Events);

                Assert.IsTrue(
                    bootstrap.EventFeed.ContainsTypeName("ScheduleInterrupted"),
                    bootstrap.EventFeed.ToDebugText());
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        static GameObject CreateHost(out PlayableHostBootstrap bootstrap)
        {
            var camGo = new GameObject("EvtCam");
            var cam = camGo.AddComponent<Camera>();
            var host = new GameObject("EvtHost");
            camGo.transform.SetParent(host.transform, true);
            bootstrap = host.AddComponent<PlayableHostBootstrap>();
            host.AddComponent<EntityViewSpawner>();
            host.AddComponent<HostSelectionController>();
            host.AddComponent<HostCommandBridge>();
            host.AddComponent<HostDebugHud>();
            host.AddComponent<HostEventFeed>();
            Assert.IsTrue(bootstrap.TryInitialize(), bootstrap.StatusLine);
            bootstrap.SelectionController.Bind(bootstrap.ViewSpawner, cam);
            bootstrap.CommandBridge.Bind(bootstrap.Session, bootstrap.SelectionController);
            return host;
        }
    }
}
