using System.IO;
using NUnit.Framework;
using UnityEngine;
using XianXia.Core.Input;
using XianXia.Core.Labor;
using XianXia.Core.Opportunity;
using XianXia.Unity.Host;

namespace XianXia.Tests
{
    public sealed class HostSnapshotPhaseGTests
    {
        [Test]
        public void Save_Load_RestoresTick_Quota_AndViews()
        {
            var host = CreateHost(out var bootstrap);
            var slot = bootstrap.SnapshotPanel.SlotPath;
            try
            {
                if (File.Exists(slot))
                    File.Delete(slot);

                var id = bootstrap.Session.CharacterIds[0];
                bootstrap.CommandBridge.IssueTo(new[] { id }, PlayerCommandKind.Observe, 2);
                bootstrap.StepTick();
                bootstrap.StepTick();

                Assert.IsTrue(bootstrap.Session.World.Entities.TryGet(id, out var before));
                Assert.Greater(before.Get<KnownSitesComponent>().KnownIds.Count, 0);
                var tickBefore = bootstrap.Session.World.Tick.Value;
                var completedBefore = before.Get<DailyTaskComponent>().CompletedAmount;

                Assert.IsTrue(bootstrap.SnapshotPanel.TrySave(), bootstrap.SnapshotPanel.Status);

                // Mutate runtime then load.
                bootstrap.CommandBridge.IssueTo(new[] { id }, PlayerCommandKind.Labor);
                for (var i = 0; i < 4; i++)
                    bootstrap.StepTick();

                Assert.IsTrue(bootstrap.SnapshotPanel.TryLoad(), bootstrap.SnapshotPanel.Status);
                Assert.AreEqual(tickBefore, bootstrap.Session.World.Tick.Value);
                Assert.AreEqual(bootstrap.Session.CharacterIds.Count, bootstrap.ViewSpawner.SpawnedCount);

                Assert.IsTrue(bootstrap.Session.World.Entities.TryGet(id, out var after));
                Assert.AreEqual(completedBefore, after.Get<DailyTaskComponent>().CompletedAmount);
                Assert.Greater(after.Get<KnownSitesComponent>().KnownIds.Count, 0);
            }
            finally
            {
                if (File.Exists(slot))
                    File.Delete(slot);
                Object.DestroyImmediate(host);
            }
        }

        static GameObject CreateHost(out PlayableHostBootstrap bootstrap)
        {
            var camGo = new GameObject("SnapCam");
            var cam = camGo.AddComponent<Camera>();
            var host = new GameObject("SnapHost");
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
            bootstrap.SnapshotPanel.Bind(bootstrap);
            return host;
        }
    }
}
