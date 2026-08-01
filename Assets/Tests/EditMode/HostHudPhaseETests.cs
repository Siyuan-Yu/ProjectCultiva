using NUnit.Framework;
using UnityEngine;
using XianXia.Core.Input;
using XianXia.Unity.Host;

namespace XianXia.Tests
{
    public sealed class HostHudPhaseETests
    {
        [Test]
        public void HudSnapshot_MatchesDayClock_AndFocusComponents()
        {
            var host = CreateHost(out var bootstrap);
            try
            {
                var id = bootstrap.Session.CharacterIds[0];
                bootstrap.SelectionController.SelectEntity(id, false);
                bootstrap.CommandBridge.IssueSelected(PlayerCommandKind.Labor);

                for (var i = 0; i < 3; i++)
                    Assert.IsTrue(bootstrap.Session.TickOnce().IsSuccess);

                bootstrap.DebugHud.SetSpeedMultiplier(2);
                var snap = bootstrap.DebugHud.Refresh();
                var day = bootstrap.Session.CurrentDayClock;

                Assert.IsTrue(snap.Ready);
                Assert.AreEqual(day.DayIndex, snap.DayIndex);
                Assert.AreEqual(day.HourOfDay, snap.HourOfDay);
                Assert.AreEqual(day.TickInDay, snap.TickInDay);
                Assert.AreEqual(2, snap.SpeedMultiplier);
                Assert.IsTrue(snap.ActionLine.Contains("LaborAction"));
                Assert.IsTrue(snap.ScheduleLine.Contains("Rest") || snap.ScheduleLine.Contains("Labor"));
                Assert.IsTrue(snap.QuotaLine.Contains("/"));
                Assert.IsTrue(snap.RealmLine.Contains("Mortal") || snap.RealmLine.Contains("QiRefining"));
                Assert.GreaterOrEqual(snap.Risk, 0);
                Assert.IsTrue(snap.ToDebugText().Contains("Day "));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Pause_And_SpeedCycle_AreHostOnly()
        {
            var host = CreateHost(out var bootstrap);
            try
            {
                Assert.IsTrue(bootstrap.Session.IsPaused);
                bootstrap.Resume();
                Assert.IsFalse(bootstrap.Session.IsPaused);
                bootstrap.Pause();
                Assert.IsTrue(bootstrap.Session.IsPaused);

                Assert.AreEqual(1, bootstrap.DebugHud.SpeedMultiplier);
                bootstrap.DebugHud.CycleSpeed();
                Assert.AreEqual(2, bootstrap.DebugHud.SpeedMultiplier);
                bootstrap.DebugHud.CycleSpeed();
                Assert.AreEqual(5, bootstrap.DebugHud.SpeedMultiplier);
                bootstrap.DebugHud.CycleSpeed();
                Assert.AreEqual(1, bootstrap.DebugHud.SpeedMultiplier);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        static GameObject CreateHost(out PlayableHostBootstrap bootstrap)
        {
            var camGo = new GameObject("HudCam");
            var cam = camGo.AddComponent<Camera>();
            cam.transform.position = new Vector3(0f, 8f, -12f);

            var host = new GameObject("HudHost");
            camGo.transform.SetParent(host.transform, true);
            bootstrap = host.AddComponent<PlayableHostBootstrap>();
            host.AddComponent<EntityViewSpawner>();
            host.AddComponent<HostSelectionController>();
            host.AddComponent<HostCommandBridge>();
            host.AddComponent<HostDebugHud>();
            Assert.IsTrue(bootstrap.TryInitialize(), bootstrap.StatusLine);
            bootstrap.SelectionController.Bind(bootstrap.ViewSpawner, cam);
            bootstrap.CommandBridge.Bind(bootstrap.Session, bootstrap.SelectionController);
            bootstrap.DebugHud.Bind(bootstrap, bootstrap.SelectionController);
            return host;
        }
    }
}
