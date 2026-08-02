using NUnit.Framework;
using UnityEngine;
using XianXia.Core.Content;
using XianXia.Core.Simulation;
using XianXia.Unity.Host;

namespace XianXia.Tests
{
    /// <summary>Host A：FormalHud／Bridge 解析 ContentEvent 选项。</summary>
    public sealed class HostContentEventChoiceTests
    {
        [Test]
        public void Bridge_ResolveContentChoice_ClearsActiveEvent()
        {
            var host = CreateHost(out var bootstrap);
            try
            {
                var session = bootstrap.Session;
                var subject = session.CharacterIds[0];
                DismissActiveContent(bootstrap, session.World);
                var debug = new ContentDebugService();
                Assert.IsTrue(
                    debug.ForcePresentEvent(session.World, subject, "base:event_ch01_ref_woodcutter").IsSuccess,
                    "force present");
                Assert.IsTrue(session.World.ContentEvents.HasActive);
                Assert.AreEqual("base:event_ch01_ref_woodcutter", session.World.ContentEvents.ActiveEventId);

                Assert.IsTrue(
                    bootstrap.CommandBridge.ResolveContentChoice("help_listen"),
                    bootstrap.CommandBridge.LastStatus);
                Assert.IsFalse(session.World.ContentEvents.HasActive);
                Assert.IsTrue(session.World.Flags.Has("event:ch01_ref_elder_helped"));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ContentInterrupt_HoldsPause_WhileEventActive()
        {
            var host = CreateHost(out var bootstrap);
            try
            {
                var interrupt = host.GetComponent<HostContentInterruptPresenter>();
                Assert.IsNotNull(interrupt);
                interrupt.Bind(bootstrap, bootstrap.CommandBridge, bootstrap.SelectionController);

                var session = bootstrap.Session;
                session.IsPaused = false;
                var subject = session.CharacterIds[0];
                DismissActiveContent(bootstrap, session.World);
                Assert.IsTrue(
                    new ContentDebugService()
                        .ForcePresentEvent(session.World, subject, "base:event_ch01_ref_spring_whisper")
                        .IsSuccess);
                Assert.AreEqual("base:event_ch01_ref_spring_whisper", session.World.ContentEvents.ActiveEventId);

                interrupt.TickInterruptState();
                Assert.IsTrue(interrupt.HasBlockingInterrupt);
                Assert.IsTrue(session.IsPaused, "active event should force pause");

                Assert.IsTrue(bootstrap.CommandBridge.ResolveContentChoice("leave"));
                interrupt.ClearSessionState();
                session.IsPaused = false;
                interrupt.TickInterruptState();
                Assert.IsFalse(interrupt.HasBlockingInterrupt);
                Assert.IsFalse(session.IsPaused, "resolved event should release pause");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        static void DismissActiveContent(PlayableHostBootstrap bootstrap, SimulationWorld world)
        {
            while (world.ContentEvents.HasActive)
            {
                if (!world.ContentEvents.TryGet(world.ContentEvents.ActiveEventId, out var spec) ||
                    spec.Choices.Count == 0)
                    break;
                Assert.IsTrue(
                    bootstrap.CommandBridge.ResolveContentChoice(spec.Choices[0].Id),
                    bootstrap.CommandBridge.LastStatus);
            }
        }

        static GameObject CreateHost(out PlayableHostBootstrap bootstrap)
        {
            var camGo = new GameObject("EventChoiceCam");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.pixelRect = new Rect(0f, 0f, 800f, 600f);

            var host = new GameObject("EventChoiceHost");
            bootstrap = host.AddComponent<PlayableHostBootstrap>();
            bootstrap.ConfigureOpeningScenario("base:scenario_ch01_reference");
            host.AddComponent<EntityViewSpawner>();
            host.AddComponent<HostSelectionController>();
            host.AddComponent<HostCommandBridge>();
            host.AddComponent<HostMapGraybox>();
            host.AddComponent<HostContentInterruptPresenter>();
            Assert.IsTrue(bootstrap.TryInitialize(), bootstrap.StatusLine);
            bootstrap.SelectionController.Bind(bootstrap.ViewSpawner, cam);
            bootstrap.SelectionController.SetPartyFilter(bootstrap.Session.CharacterIds);
            bootstrap.CommandBridge.Bind(bootstrap.Session, bootstrap.SelectionController);
            camGo.transform.SetParent(host.transform, true);
            return host;
        }
    }
}
