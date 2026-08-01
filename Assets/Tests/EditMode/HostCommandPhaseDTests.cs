using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using XianXia.Core.Actions;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Events;
using XianXia.Core.Input;
using XianXia.Core.Opportunity;
using XianXia.Core.Orders;
using XianXia.Unity.Host;

namespace XianXia.Tests
{
    public sealed class HostCommandPhaseDTests
    {
        [Test]
        public void EmptySelection_IssuesNothing()
        {
            var host = CreateHost(out var bootstrap, out _);
            try
            {
                var bridge = bootstrap.CommandBridge;
                Assert.AreEqual(0, bridge.IssueSelected(PlayerCommandKind.Labor));
                Assert.AreEqual(0, bridge.LastSuccessCount);
                Assert.AreEqual(0, bridge.LastFailureCount);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Labor_And_Rest_GoThroughPort_SetActiveAction()
        {
            var host = CreateHost(out var bootstrap, out _);
            try
            {
                var id = bootstrap.Session.CharacterIds[0];
                var bridge = bootstrap.CommandBridge;
                Assert.AreEqual(1, bridge.IssueTo(new[] { id }, PlayerCommandKind.Labor));
                Assert.IsInstanceOf<LaborAction>(ActiveOf(bootstrap, id));

                // Finish player Labor so Rest can start (Player does not interrupt Player).
                for (var i = 0; i < (int)HostCommandBridge.DefaultDurationTicks; i++)
                    Assert.IsTrue(bootstrap.Session.TickOnce().IsSuccess);

                Assert.AreEqual(1, bridge.IssueTo(new[] { id }, PlayerCommandKind.Rest));
                Assert.IsInstanceOf<RestAction>(ActiveOf(bootstrap, id));
                Assert.AreEqual(
                    OrderSource.Player,
                    bootstrap.Session.World.Entities.TryGet(id, out var e)
                        ? e.Get<ActionStateComponent>().ActiveOrderSource
                        : OrderSource.Schedule);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void MultiSelect_IssuesOneRequestPerEntity_ContinuesOnFailure()
        {
            var host = CreateHost(out var bootstrap, out _);
            try
            {
                var ids = bootstrap.Session.CharacterIds;
                var targets = new List<EntityId> { ids[0], ids[1], new EntityId(99999) };
                var ok = bootstrap.CommandBridge.IssueTo(targets, PlayerCommandKind.Labor);
                Assert.AreEqual(2, ok);
                Assert.AreEqual(2, bootstrap.CommandBridge.LastSuccessCount);
                Assert.AreEqual(1, bootstrap.CommandBridge.LastFailureCount);
                Assert.IsInstanceOf<LaborAction>(ActiveOf(bootstrap, ids[0]));
                Assert.IsInstanceOf<LaborAction>(ActiveOf(bootstrap, ids[1]));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void PlayerCommand_OverridesScheduleLabor()
        {
            var host = CreateHost(out var bootstrap, out _);
            try
            {
                var id = bootstrap.Session.CharacterIds[0];
                Assert.IsTrue(bootstrap.Session.World.Entities.TryGet(id, out var entity));
                // Advance into a Labor schedule block (default starts Rest 0-8).
                for (var i = 0; i < 9; i++)
                    Assert.IsTrue(bootstrap.Session.TickOnce().IsSuccess);

                Assert.IsInstanceOf<LaborAction>(ActiveOf(bootstrap, id));
                Assert.AreEqual(OrderSource.Schedule, entity.Get<ActionStateComponent>().ActiveOrderSource);

                Assert.AreEqual(1, bootstrap.CommandBridge.IssueTo(new[] { id }, PlayerCommandKind.Rest));
                Assert.IsInstanceOf<RestAction>(ActiveOf(bootstrap, id));
                Assert.AreEqual(OrderSource.Player, entity.Get<ActionStateComponent>().ActiveOrderSource);
                Assert.IsTrue(bootstrap.Session.World.Events.Drain().Exists(
                    e => e.Type == XianXia.Core.Events.EventType.ScheduleInterrupted));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Observe_Then_Cultivate_ViaBridge()
        {
            var host = CreateHost(out var bootstrap, out _);
            try
            {
                var id = bootstrap.Session.CharacterIds[0];
                var bridge = bootstrap.CommandBridge;
                Assert.AreEqual(1, bridge.IssueTo(new[] { id }, PlayerCommandKind.Observe, 2));
                for (var i = 0; i < 2; i++)
                    Assert.IsTrue(bootstrap.Session.TickOnce().IsSuccess);

                Assert.IsTrue(bootstrap.Session.World.Entities.TryGet(id, out var entity));
                Assert.Greater(entity.Get<KnownSitesComponent>().KnownIds.Count, 0);

                Assert.AreEqual(1, bridge.IssueTo(new[] { id }, PlayerCommandKind.Cultivate, 2));
                Assert.IsInstanceOf<CultivateAction>(ActiveOf(bootstrap, id));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void SelectedIssue_UsesSelectionState()
        {
            var host = CreateHost(out var bootstrap, out _);
            try
            {
                var id = bootstrap.Session.CharacterIds[1];
                bootstrap.SelectionController.SelectEntity(id, false);
                Assert.AreEqual(1, bootstrap.CommandBridge.IssueSelected(PlayerCommandKind.Labor));
                Assert.IsInstanceOf<LaborAction>(ActiveOf(bootstrap, id));
                Assert.IsNull(ActiveOf(bootstrap, bootstrap.Session.CharacterIds[0]));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        static IAction ActiveOf(PlayableHostBootstrap bootstrap, EntityId id)
        {
            if (!bootstrap.Session.World.Entities.TryGet(id, out var entity))
                return null;
            var actionId = entity.Get<ActionStateComponent>().ActiveActionId;
            if (actionId.IsNone)
                return null;
            return bootstrap.Session.World.ActiveActions.TryGetValue(actionId, out var action) ? action : null;
        }

        static GameObject CreateHost(out PlayableHostBootstrap bootstrap, out Camera cam)
        {
            var camGo = new GameObject("CmdCam");
            cam = camGo.AddComponent<Camera>();
            cam.transform.position = new Vector3(0f, 8f, -12f);
            cam.transform.LookAt(Vector3.zero);

            var host = new GameObject("CmdHost");
            bootstrap = host.AddComponent<PlayableHostBootstrap>();
            host.AddComponent<EntityViewSpawner>();
            host.AddComponent<HostSelectionController>();
            host.AddComponent<HostCommandBridge>();
            Assert.IsTrue(bootstrap.TryInitialize(), bootstrap.StatusLine);
            bootstrap.SelectionController.Bind(bootstrap.ViewSpawner, cam);
            bootstrap.CommandBridge.Bind(bootstrap.Session, bootstrap.SelectionController);
            // Destroy cam with host for cleanup convenience.
            camGo.transform.SetParent(host.transform, true);
            return host;
        }
    }
}
