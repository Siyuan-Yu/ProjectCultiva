using NUnit.Framework;
using UnityEngine;
using XianXia.Core.Cultivation;
using XianXia.Core.Input;
using XianXia.Core.Labor;
using XianXia.Core.Opportunity;
using XianXia.Unity.Host;

namespace XianXia.Tests
{
    /// <summary>
    /// VS0.4 Phase H: automated stand-in for the playable-day checklist (Host adapters only).
    /// </summary>
    public sealed class HostPlayableDayPhaseHTests
    {
        [Test]
        public void PlayableDay_Schedule_Observe_Cultivate_DayBoundary_Snapshot()
        {
            var host = CreateHost(out var bootstrap);
            try
            {
                Assert.AreEqual(3, bootstrap.Session.CharacterIds.Count);
                Assert.GreaterOrEqual(bootstrap.ViewSpawner.SpawnedCount, 4); // party＋NPCs (Ch01 ref cast)

                var id = bootstrap.Session.CharacterIds[0];
                bootstrap.SelectionController.SelectEntity(id, false);
                Assert.AreEqual(1, bootstrap.SelectionController.State.Count);

                // Default Schedule advances into Labor block.
                for (var i = 0; i < 9; i++)
                    bootstrap.StepTick();

                // Override with Observe → discover site.
                Assert.AreEqual(1, bootstrap.CommandBridge.IssueSelected(PlayerCommandKind.Observe, 2));
                bootstrap.StepTick();
                bootstrap.StepTick();
                Assert.IsTrue(bootstrap.Session.World.Entities.TryGet(id, out var entity));
                Assert.Greater(entity.Get<KnownSitesComponent>().KnownIds.Count, 0);
                Assert.IsTrue(bootstrap.EventFeed.ContainsTypeName("OpportunitySiteDiscovered") ||
                              bootstrap.EventFeed.ContainsTypeName("ObservationResolved"));

                Assert.AreEqual(1, bootstrap.CommandBridge.IssueSelected(PlayerCommandKind.Cultivate, 3));
                bootstrap.StepTick();
                bootstrap.StepTick();
                bootstrap.StepTick();
                Assert.Greater(entity.Get<CultivationComponent>().Progress, 0);
                Assert.Greater(entity.Get<XianXia.Core.Concealment.PersonalConcealmentRiskComponent>().Value, 0);

                var hud = bootstrap.DebugHud.Refresh();
                Assert.IsTrue(hud.Ready);
                Assert.IsTrue(hud.QuotaLine.Contains("/"));
                Assert.IsTrue(hud.RealmLine.Length > 0);

                // Jump near day end and cross boundary for quota consequence.
                bootstrap.Session.World.Tick = new XianXia.Core.Domain.Time.WorldTick(
                    (ulong)(XianXia.Core.Domain.Time.WorldTick.TicksPerDay - 1));
                bootstrap.Session.World.Events.Drain();
                bootstrap.EventFeed.Clear();
                bootstrap.StepTick();
                Assert.IsTrue(
                    bootstrap.EventFeed.ContainsTypeName("DayEnded") ||
                    bootstrap.EventFeed.ContainsTypeName("QuotaConsequenceApplied") ||
                    entity.Get<DailyTaskComponent>().PendingReprimand,
                    bootstrap.EventFeed.ToDebugText());

                Assert.IsTrue(bootstrap.SnapshotPanel.TrySave(), bootstrap.SnapshotPanel.Status);
                var tick = bootstrap.Session.World.Tick.Value;
                Assert.IsTrue(bootstrap.SnapshotPanel.TryLoad(), bootstrap.SnapshotPanel.Status);
                Assert.AreEqual(tick, bootstrap.Session.World.Tick.Value);
                Assert.AreEqual(3, bootstrap.Session.CharacterIds.Count);
                // Snapshot restore does not persist Npc social spawn; views rebuild from restored world tags.
                Assert.GreaterOrEqual(bootstrap.ViewSpawner.SpawnedCount, 3);
            }
            finally
            {
                var slot = bootstrap.SnapshotPanel.SlotPath;
                if (System.IO.File.Exists(slot))
                    System.IO.File.Delete(slot);
                Object.DestroyImmediate(host);
            }
        }

        static GameObject CreateHost(out PlayableHostBootstrap bootstrap)
        {
            var camGo = new GameObject("DayCam");
            var cam = camGo.AddComponent<Camera>();
            var host = new GameObject("DayHost");
            camGo.transform.SetParent(host.transform, true);
            bootstrap = host.AddComponent<PlayableHostBootstrap>();
            host.AddComponent<EntityViewSpawner>();
            host.AddComponent<HostSelectionController>();
            host.AddComponent<HostCommandBridge>();
            host.AddComponent<HostDebugHud>();
            host.AddComponent<HostEventFeed>();
            host.AddComponent<HostSnapshotPanel>();
            Assert.IsTrue(bootstrap.TryInitialize(), bootstrap.StatusLine);
            bootstrap.SelectionController.Bind(bootstrap.ViewSpawner, cam);
            bootstrap.CommandBridge.Bind(bootstrap.Session, bootstrap.SelectionController);
            bootstrap.DebugHud.Bind(bootstrap, bootstrap.SelectionController);
            bootstrap.SnapshotPanel.Bind(bootstrap);
            return host;
        }
    }
}
