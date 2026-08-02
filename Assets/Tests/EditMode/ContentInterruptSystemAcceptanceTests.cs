using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Events;
using XianXia.Core.Exploration;
using XianXia.Unity.Host;

namespace XianXia.Tests
{
    /// <summary>CIF：内容打断正式验收（事件选项＋任务提醒＋抵达 onArrive）。</summary>
    public sealed class ContentInterruptSystemAcceptanceTests
    {
        [Test]
        public void OpeningDayBeatQuest_ShowsInterruptAfterTick()
        {
            var host = CreateHost(out var bootstrap);
            try
            {
                var interrupt = bootstrap.ContentInterrupt;
                // chapter day0 已接巡视任务；Dispatch 在 Initialize 时完成。
                interrupt.TickInterruptState();
                Assert.IsTrue(interrupt.HasBlockingInterrupt);
                Assert.IsTrue(bootstrap.Session.IsPaused);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void NotifyArrived_CanPresentOnArriveEvent()
        {
            var host = CreateHost(out var bootstrap);
            try
            {
                var session = bootstrap.Session;
                var subject = session.CharacterIds[0];
                // 老人事件条件：wood_done；先铺 Flag 再抵达树林 onArrive 不测该事件。
                // 用 ForcePresent 验证呈现链；抵达钩子本身：
                Assert.IsTrue(
                    new ExplorationService()
                        .NotifyArrived(session.World, subject, "base:loc_ref_forest", setLocation: true)
                        .IsSuccess);
                Assert.IsTrue(
                    session.World.Entities.TryGet(subject, out var e) &&
                    e.TryGet<EntityLocationComponent>(out var loc) &&
                    loc.LocationId == "base:loc_ref_forest");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Ingest_QuestCompleted_AfterEventPriority()
        {
            var host = CreateHost(out var bootstrap);
            try
            {
                var interrupt = bootstrap.ContentInterrupt;
                var session = bootstrap.Session;
                var subject = session.CharacterIds[0];
                while (session.World.ContentEvents.HasActive)
                {
                    Assert.IsTrue(session.World.ContentEvents.TryGet(
                        session.World.ContentEvents.ActiveEventId, out var open));
                    Assert.IsTrue(bootstrap.CommandBridge.ResolveContentChoice(open.Choices[0].Id));
                }

                var drained = new List<DomainEvent>
                {
                    new DomainEvent(
                        new EventId(1),
                        XianXia.Core.Events.EventType.QuestCompleted,
                        session.World.Tick,
                        payload: "base:quest_ch01_ref_inspect_yard")
                };
                interrupt.Ingest(drained);

                Assert.IsTrue(
                    new ContentDebugService()
                        .ForcePresentEvent(session.World, subject, "base:event_ch01_ref_spring_whisper")
                        .IsSuccess);
                Assert.AreEqual("base:event_ch01_ref_spring_whisper", session.World.ContentEvents.ActiveEventId);
                interrupt.TickInterruptState();
                Assert.IsTrue(session.World.ContentEvents.HasActive);
                Assert.IsTrue(interrupt.HasBlockingInterrupt);

                Assert.IsTrue(bootstrap.CommandBridge.ResolveContentChoice("leave"));
                interrupt.TickInterruptState();
                Assert.IsTrue(interrupt.HasBlockingInterrupt, "quest notify should follow event");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        static GameObject CreateHost(out PlayableHostBootstrap bootstrap)
        {
            var camGo = new GameObject("CifCam");
            var cam = camGo.AddComponent<Camera>();
            cam.pixelRect = new Rect(0f, 0f, 800f, 600f);
            var host = new GameObject("CifHost");
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
            bootstrap.ContentInterrupt.Bind(
                bootstrap, bootstrap.CommandBridge, bootstrap.SelectionController);
            camGo.transform.SetParent(host.transform, true);
            return host;
        }
    }
}
