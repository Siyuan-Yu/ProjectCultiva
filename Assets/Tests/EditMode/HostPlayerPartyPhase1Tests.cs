using NUnit.Framework;
using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Input;
using XianXia.Core.World;
using XianXia.Unity.Host;

namespace XianXia.Tests
{
    public sealed class HostPlayerPartyPhase1Tests
    {
        [Test]
        public void PlayerInput_OnlyActiveReceivesIssueSelected()
        {
            var camGo = new GameObject("PartyCam");
            var cam = camGo.AddComponent<Camera>();
            cam.pixelRect = new Rect(0f, 0f, 800f, 600f);
            var host = new GameObject("PartyHost");
            try
            {
                var bootstrap = host.AddComponent<PlayableHostBootstrap>();
                bootstrap.ConfigureOpeningScenario("base:scenario_ch01_reference");
                host.AddComponent<EntityViewSpawner>();
                host.AddComponent<HostSelectionController>();
                host.AddComponent<HostCommandBridge>();
                host.AddComponent<HostMapGraybox>();
                Assert.IsTrue(bootstrap.TryInitialize(), bootstrap.StatusLine);

                var session = bootstrap.Session;
                var a = session.CharacterIds[0];
                var b = session.CharacterIds[1];
                session.PlayerParty.TryInitialize(a, out _);
                session.PlayerParty.TryAddMember(session.World, session.CharacterIds, b, out _);

                bootstrap.SelectionController.Bind(bootstrap.ViewSpawner, cam);
                bootstrap.SelectionController.State.Replace(new[] { a, b });
                bootstrap.CommandBridge.Bind(session, bootstrap.SelectionController);

                Assert.AreEqual(1, bootstrap.CommandBridge.IssueSelected(PlayerCommandKind.Labor));
                Assert.IsNotNull(ActiveOf(bootstrap, a));
                Assert.IsNull(ActiveOf(bootstrap, b));
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(camGo);
            }
        }

        [Test]
        public void DragSelection_CommandReceiverStillOnlyActive()
        {
            var camGo = new GameObject("PartySelCam");
            var cam = camGo.AddComponent<Camera>();
            cam.pixelRect = new Rect(0f, 0f, 800f, 600f);
            var host = new GameObject("PartySelHost");
            try
            {
                var bootstrap = host.AddComponent<PlayableHostBootstrap>();
                bootstrap.ConfigureOpeningScenario("base:scenario_ch01_reference");
                host.AddComponent<EntityViewSpawner>();
                host.AddComponent<HostSelectionController>();
                host.AddComponent<HostCommandBridge>();
                host.AddComponent<HostMapGraybox>();
                Assert.IsTrue(bootstrap.TryInitialize(), bootstrap.StatusLine);

                var session = bootstrap.Session;
                var a = session.CharacterIds[0];
                var b = session.CharacterIds[1];
                session.PlayerParty.TryInitialize(a, out _);
                session.PlayerParty.TryAddMember(session.World, session.CharacterIds, b, out _);

                bootstrap.SelectionController.Bind(bootstrap.ViewSpawner, cam);
                bootstrap.SelectionController.State.Replace(new[] { a, b });
                bootstrap.CommandBridge.Bind(session, bootstrap.SelectionController);

                Assert.AreEqual(a, session.PlayerParty.ActiveCharacterId);
                Assert.AreEqual(2, bootstrap.SelectionController.State.Count);
                Assert.AreEqual(1, bootstrap.CommandBridge.IssueSelected(PlayerCommandKind.Rest));
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(camGo);
            }
        }

        [Test]
        public void ViewSelection_NonPartyCharacterStillSelectable()
        {
            var camGo = new GameObject("PartyViewCam");
            var cam = camGo.AddComponent<Camera>();
            cam.pixelRect = new Rect(0f, 0f, 800f, 600f);
            var host = new GameObject("PartyViewHost");
            try
            {
                var bootstrap = host.AddComponent<PlayableHostBootstrap>();
                bootstrap.ConfigureOpeningScenario("base:scenario_ch01_reference");
                host.AddComponent<EntityViewSpawner>();
                host.AddComponent<HostSelectionController>();
                host.AddComponent<HostMapGraybox>();
                Assert.IsTrue(bootstrap.TryInitialize(), bootstrap.StatusLine);

                var session = bootstrap.Session;
                var active = session.CharacterIds[0];
                session.PlayerParty.TryInitialize(active, out _);

                var other = session.CharacterIds[2];
                bootstrap.SelectionController.Bind(bootstrap.ViewSpawner, cam);
                Assert.IsTrue(bootstrap.SelectionController.SelectEntity(other, false));
                Assert.IsTrue(bootstrap.SelectionController.State.Contains(other));
                Assert.IsFalse(session.PlayerParty.IsMember(other));
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(camGo);
            }
        }

        [Test]
        public void RoutePreview_CommandAuthorityIgnoresSelection()
        {
            var camGo = new GameObject("RoutePreviewCam");
            var cam = camGo.AddComponent<Camera>();
            cam.pixelRect = new Rect(0f, 0f, 800f, 600f);
            var host = new GameObject("RoutePreviewHost");
            try
            {
                var bootstrap = host.AddComponent<PlayableHostBootstrap>();
                bootstrap.ConfigureOpeningScenario("base:scenario_ch01_reference");
                host.AddComponent<EntityViewSpawner>();
                host.AddComponent<HostSelectionController>();
                host.AddComponent<HostMapGraybox>();
                Assert.IsTrue(bootstrap.TryInitialize(), bootstrap.StatusLine);

                var session = bootstrap.Session;
                var active = session.CharacterIds[0];
                var follower = session.CharacterIds[1];
                session.PlayerParty.TryInitialize(active, out _);
                session.PlayerParty.TryAddMember(session.World, session.CharacterIds, follower, out _);

                bootstrap.SelectionController.Bind(bootstrap.ViewSpawner, cam);
                bootstrap.SelectionController.State.Replace(new[] { follower });

                Assert.AreEqual(active, session.PlayerParty.ActiveCharacterId);
                Assert.IsFalse(
                    HostPlayerMoveCommandGate.IsActiveCommandContext(
                        bootstrap.SelectionController,
                        session.PlayerParty));
                Assert.AreEqual(
                    EntityId.None,
                    HostPlayerMoveCommandGate.ResolveActiveForWorldMove(
                        bootstrap.SelectionController,
                        session.PlayerParty));
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(camGo);
            }
        }

        [Test]
        public void WorldMove_GateAllowsEmptySelectionOrActiveSelected()
        {
            var camGo = new GameObject("MoveGateCam");
            var cam = camGo.AddComponent<Camera>();
            cam.pixelRect = new Rect(0f, 0f, 800f, 600f);
            var host = new GameObject("MoveGateHost");
            try
            {
                var bootstrap = host.AddComponent<PlayableHostBootstrap>();
                bootstrap.ConfigureOpeningScenario("base:scenario_ch01_reference");
                host.AddComponent<EntityViewSpawner>();
                host.AddComponent<HostSelectionController>();
                host.AddComponent<HostMapGraybox>();
                Assert.IsTrue(bootstrap.TryInitialize(), bootstrap.StatusLine);

                var session = bootstrap.Session;
                var active = session.CharacterIds[0];
                var follower = session.CharacterIds[1];
                session.PlayerParty.TryInitialize(active, out _);
                session.PlayerParty.TryAddMember(session.World, session.CharacterIds, follower, out _);
                bootstrap.SelectionController.Bind(bootstrap.ViewSpawner, cam);

                bootstrap.SelectionController.ClearSelection();
                Assert.IsTrue(
                    HostPlayerMoveCommandGate.IsActiveCommandContext(
                        bootstrap.SelectionController,
                        session.PlayerParty));

                bootstrap.SelectionController.State.Replace(new[] { active, follower });
                Assert.IsTrue(
                    HostPlayerMoveCommandGate.IsActiveCommandContext(
                        bootstrap.SelectionController,
                        session.PlayerParty));
                Assert.AreEqual(
                    active,
                    HostPlayerMoveCommandGate.ResolveActiveForWorldMove(
                        bootstrap.SelectionController,
                        session.PlayerParty));

                bootstrap.SelectionController.State.Replace(new[] { follower });
                Assert.IsFalse(
                    HostPlayerMoveCommandGate.IsActiveCommandContext(
                        bootstrap.SelectionController,
                        session.PlayerParty));
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(camGo);
            }
        }

        [Test]
        public void PartySharedActivity_ChangeDetectsFarmToWoodcut()
        {
            var farm = HostPartySharedActivity.Farming("loc_field_a");
            var chop = HostPartySharedActivity.Woodcutting(42);
            Assert.AreNotEqual(farm, chop);
            Assert.IsTrue(farm.IsShareable);
            Assert.IsTrue(chop.IsShareable);
            Assert.IsFalse(HostPartySharedActivity.Movement.IsShareable);
        }

        [Test]
        public void PartySharedActivity_SameFarmLocationIsStable()
        {
            var a = HostPartySharedActivity.Farming("loc_field_a");
            var b = HostPartySharedActivity.Farming("loc_field_a");
            Assert.AreEqual(a, b);
            Assert.AreNotEqual(a, HostPartySharedActivity.Farming("loc_field_b"));
        }

        [Test]
        public void CameraFollowPolicy_WasdOnly_RtsNeverFollows()
        {
            var free = HostActiveCameraFollowMode.Free;
            var wasd = HostActiveCameraFollowMode.WasdHardFollow;

            // CAMERA-A/B: no WASD → Free (RTS path irrelevant)
            Assert.AreEqual(free, HostPlayerPartyController.ResolveCameraFollowMode(false));

            // CAMERA-C: WASD → Hard Follow
            Assert.AreEqual(wasd, HostPlayerPartyController.ResolveCameraFollowMode(true));

            // CAMERA-D: release WASD → Free
            Assert.AreEqual(free, HostPlayerPartyController.ResolveCameraFollowMode(wasd, wasdDirectActive: false));

            // CAMERA-E: WASD from Free
            Assert.AreEqual(wasd, HostPlayerPartyController.ResolveCameraFollowMode(free, wasdDirectActive: true));
        }

        static XianXia.Core.Actions.IAction ActiveOf(PlayableHostBootstrap bootstrap, XianXia.Core.Domain.Ids.EntityId id)
        {
            if (!bootstrap.Session.World.Entities.TryGet(id, out var entity))
                return null;
            var actionId = entity.Get<XianXia.Core.Entities.ActionStateComponent>().ActiveActionId;
            if (actionId.IsNone)
                return null;
            return bootstrap.Session.World.ActiveActions.TryGetValue(actionId, out var action) ? action : null;
        }
    }
}
