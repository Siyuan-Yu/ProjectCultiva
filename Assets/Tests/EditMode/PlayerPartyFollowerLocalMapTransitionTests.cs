using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;
using XianXia.Unity.Host;

namespace XianXia.Tests
{
    public sealed class PlayerPartyFollowerLocalMapTransitionTests
    {
        const string MapA = "base:map_ch01_reference";
        const string MapB = "base:map_wilderness_plain_fallback";
        const string MapC = "base:map_site_chengzhen";
        const string TestFaction = "test:faction_a";

        static PlayableHostBootstrap CreateCh01Bootstrap(out GameObject hostGo)
        {
            hostGo = new GameObject("FollowerTransitionHost");
            var bootstrap = hostGo.AddComponent<PlayableHostBootstrap>();
            bootstrap.ConfigureOpeningScenario("base:scenario_ch01_reference");
            hostGo.AddComponent<EntityViewSpawner>();
            hostGo.AddComponent<HostSelectionController>();
            hostGo.AddComponent<HostCommandBridge>();
            hostGo.AddComponent<HostMapGraybox>();
            hostGo.AddComponent<HostMoveController>();
            hostGo.AddComponent<HostPlayerPartyController>();
            return bootstrap;
        }

        static void SetupParty(PlayableHostBootstrap bootstrap, out EntityId active, out EntityId follower)
        {
            var session = bootstrap.Session;
            active = session.CharacterIds[0];
            follower = session.CharacterIds[1];
            session.PlayerParty.TryInitialize(active, out _);
            session.PlayerParty.TryAddMember(session.World, session.CharacterIds, follower, out _);
        }

        static Vector3 FollowerGoalNearActive(PlayableHostBootstrap bootstrap, EntityId active, float dx = 8f)
        {
            Assert.IsTrue(bootstrap.ViewSpawner.Registry.TryGet(active, out var activeView));
            var goal = activeView.transform.position + new Vector3(dx, 0f, 0f);
            goal.z = HostPresentationSpace.EntityZ;
            return goal;
        }

        static void SeedFollowerMovingOnMap(
            PlayableHostBootstrap bootstrap,
            EntityId follower,
            string localMapId,
            Vector3 goal)
        {
            var move = bootstrap.MoveController;
            move.BindLocalMapContext(localMapId);
            Assert.IsTrue(move.OrderEntityToWorldPoint(follower, goal, null, issueStop: false));
            Assert.IsTrue(move.IsMoving(follower));
            Assert.IsTrue(move.HasMovementPath(follower));
            Assert.IsTrue(move.TryGetPathLocalMapId(follower, out var pathMap));
            Assert.AreEqual(localMapId, pathMap);
        }

        static SimulationWorld BuildTinyTravelWorld(out WorldSite siteA, out WorldSite siteB, out HexCoord midHex)
        {
            var world = new SimulationWorld();
            world.HexWorld.MapId = "test:follower_transition";
            world.HexWorld.HexSize = 1f;
            world.HexWorld.FillRectangle(20, 12, HexTerrainType.Plain);
            for (var r = 0; r < 12; r++)
            for (var q = 0; q < 20; q++)
            {
                if (!world.HexWorld.TryGetCell(new HexCoord(q, r), out var cell) || cell == null)
                    continue;
                cell.IsPassable = true;
            }

            var aAnchor = new HexCoord(2, 4);
            var aPresence = new HexCoord(3, 4);
            siteA = new WorldSite
            {
                SiteId = "test:site_huangcun",
                DisplayName = "青石荒村",
                AnchorHex = aAnchor,
                PresenceHex = aPresence,
                LocalMapId = MapA,
            };
            siteA.SetFootprint(new[]
            {
                aAnchor, aPresence, new HexCoord(2, 5), new HexCoord(3, 5),
            });
            WorldSiteRegistrationService.RegisterSiteOnGrid(world, siteA);

            var bAnchor = new HexCoord(10, 4);
            siteB = new WorldSite
            {
                SiteId = "test:site_chengzhen",
                DisplayName = "青石镇",
                AnchorHex = bAnchor,
                PresenceHex = bAnchor,
                LocalMapId = MapC,
            };
            siteB.SetFootprint(new[]
            {
                bAnchor, new HexCoord(11, 4), new HexCoord(10, 5), new HexCoord(11, 5),
            });
            WorldSiteRegistrationService.RegisterSiteOnGrid(world, siteB);

            midHex = new HexCoord(6, 4);
            return world;
        }

        static EntityId Spawn(SimulationWorld world, string name)
        {
            var created = world.Entities.CreateCharacter(new DefinitionId("test", name), name);
            Assert.IsTrue(created.IsSuccess);
            return created.Value.Id;
        }

        static PlayerPartyRuntime BuildParty(SimulationWorld world, WorldSite site, params EntityId[] members)
        {
            world.LocalMap.ActiveMapLayoutId = site.LocalMapId;
            world.PartyWorld.SiteId = site.SiteId;
            world.PartyWorld.LocalMapId = site.LocalMapId;
            for (var i = 0; i < members.Length; i++)
            {
                world.WorldPresence.SetAtSite(members[i], site.SiteId);
                world.LocalMap.AddOccupant(members[i]);
            }

            var party = new PlayerPartyRuntime();
            Assert.IsTrue(party.TryInitialize(members[0], out _));
            for (var i = 1; i < members.Length; i++)
                Assert.IsTrue(party.TryAddMember(world, members, members[i], out _));
            return party;
        }

        static void FinishEdgeGateForTests(SimulationWorld world, int exitDir)
        {
            var bounds = WildernessLocalWorldProjection.WildernessLocalMapBounds.FromOriginSize(
                -20f, -20f, 1f, 40, 40);
            var entry = WildernessLocalWorldProjection.OppositeDirection(exitDir);
            WildernessLocalWorldProjection.GetLocalPositionNearEdge(bounds, entry, out var x, out var y);
            var gate = world.PlayerPartyTravel.SurfaceEdgeGate;
            if (gate != null && !gate.TransitionInProgress && gate.LastExitDirection < 0)
                gate.BeginTransition(exitDir);
            PlayerPartyWildernessTransitionService.CompleteEdgeTransitionPresentation(world, bounds, x, y);
        }

        static void AssertFollowerCanIssueFreshPath(
            PlayableHostBootstrap bootstrap,
            EntityId active,
            EntityId follower,
            float dx = 10f)
        {
            Assert.IsFalse(bootstrap.MoveController.IsMoving(follower));
            var goal = FollowerGoalNearActive(bootstrap, active, dx);
            Assert.IsTrue(
                bootstrap.MoveController.OrderEntityToWorldPoint(follower, goal, null, issueStop: false));
            Assert.IsTrue(bootstrap.MoveController.IsMoving(follower));
        }

        static void SimulateTransitionRecovery(
            PlayableHostBootstrap bootstrap,
            EntityId active,
            EntityId follower,
            string fromMapId,
            string toMapId)
        {
            SeedFollowerMovingOnMap(
                bootstrap,
                follower,
                fromMapId,
                FollowerGoalNearActive(bootstrap, active));
            bootstrap.ViewSpawner.Rebuild(bootstrap.Session);
            bootstrap.PlayerPartyController.OnLocalMapMaterialized(toMapId);
        }

        static WildernessLocalWorldProjection.WildernessLocalMapBounds DefaultWildernessBounds() =>
            WildernessLocalWorldProjection.WildernessLocalMapBounds.FromOriginSize(
                -20f, -20f, 1f, 40, 40);

        [Test]
        public void FollowerWorldPresenceUpdatesWhenTravelingMembersWasActiveOnly()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            var b = Spawn(world, "WangChen");
            var party = BuildParty(world, siteA, a, b);
            world.PlayerPartyTravel.SnapToHexCenter(mid, world.HexWorld.HexSize);
            world.WorldPresence.SetAtHex(a, mid);
            world.WorldPresence.SetAtHex(b, mid);

            // 模拟开局 bootstrap：TravelingMembers 仅含 Active。
            world.PlayerPartyTravel.CaptureTravelingMembers(new List<EntityId> { a });

            var neighbor = HexMath.Neighbor(mid, 1);
            Assert.IsTrue(PlayerPartyWildernessTransitionService.TryCrossWildernessEdge(
                world, party, neighbor).IsSuccess);

            Assert.IsTrue(world.WorldPresence.TryGet(b, out var followerPresence));
            Assert.AreEqual(PartyWorldPresenceMode.AtHex, followerPresence.Mode);
            Assert.AreEqual(neighbor, followerPresence.ResidualHex);

            var foundFollower = false;
            for (var i = 0; i < world.PlayerPartyTravel.TravelingMembers.Count; i++)
            {
                if (world.PlayerPartyTravel.TravelingMembers[i] == b)
                    foundFollower = true;
            }

            Assert.IsTrue(foundFollower, "Follower must be in TravelingMembers after edge cross.");
        }

        static EntityId SpawnWithFaction(SimulationWorld world, string name, string factionId)
        {
            var id = Spawn(world, name);
            if (world.Entities.TryGet(id, out var entity) && entity != null)
                entity.Get<FactionMembershipComponent>().Assign(factionId, FactionRoleKind.Member);
            return id;
        }

        [Test]
        public void FormalArmyMemberExcludedFromPartyTransition()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            siteA.OwnerFactionId = TestFaction;
            world.Strategic.PlayerFactionId = TestFaction;
            var a = SpawnWithFaction(world, "LinQing", TestFaction);
            var b = SpawnWithFaction(world, "Soldier", TestFaction);
            var party = BuildParty(world, siteA, a);

            world.WorldPresence.SetAtSite(b, siteA.SiteId);
            var armyResult = ArmyService.CreateArmy(
                world, TestFaction, siteA.SiteId, new List<EntityId> { b }, b);
            Assert.IsTrue(armyResult.IsSuccess, armyResult.IsFailure ? armyResult.Error.ToString() : string.Empty);

            world.PlayerPartyTravel.SnapToHexCenter(mid, world.HexWorld.HexSize);
            world.WorldPresence.SetAtHex(a, mid);
            world.WorldPresence.SetAtHex(b, mid);
            world.PlayerPartyTravel.CaptureTravelingMembers(new List<EntityId> { a });

            var neighbor = HexMath.Neighbor(mid, 1);
            Assert.IsTrue(PlayerPartyWildernessTransitionService.TryCrossWildernessEdge(
                world, party, neighbor).IsSuccess);

            Assert.IsTrue(world.WorldPresence.TryGet(b, out var armyMemberPresence));
            Assert.AreEqual(mid, armyMemberPresence.ResidualHex,
                "FormalArmy member must not follow PlayerParty edge transition.");
        }

        [Test]
        public void FollowerStillBelongsToPartyAfterLocalMapTransition()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            var b = Spawn(world, "WangChen");
            var party = BuildParty(world, siteA, a, b);
            world.PlayerPartyTravel.SnapToHexCenter(mid, world.HexWorld.HexSize);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);
            PlayerPartyHexTravelService.ApplyMembersAtHex(world, party, mid);

            Assert.IsTrue(PlayerPartyWildernessTransitionService.TryAttemptSurfaceEdgeTransition(
                world, party, 1).IsSuccess);
            FinishEdgeGateForTests(world, 1);
            PlayerPartyLocalMapMaterializationService.MaterializePartyOnResolvedLocalMap(
                world, party.Members, DefaultWildernessBounds());

            Assert.AreEqual(2, party.Count);
            Assert.AreEqual(a, party.ActiveCharacterId);
            Assert.IsTrue(party.IsFollower(b));
            Assert.IsTrue(world.LocalMap.ContainsOccupant(b));
        }

        [Test]
        public void FollowerOldLocalPathIsClearedOnTransition()
        {
            var hostGo = (GameObject)null;
            try
            {
                var bootstrap = CreateCh01Bootstrap(out hostGo);
                Assert.IsTrue(bootstrap.TryInitialize(), bootstrap.StatusLine);
                SetupParty(bootstrap, out var active, out var follower);

                SeedFollowerMovingOnMap(
                    bootstrap,
                    follower,
                    MapA,
                    FollowerGoalNearActive(bootstrap, active));

                bootstrap.PlayerPartyController.OnLocalMapMaterialized(MapB);

                Assert.IsFalse(bootstrap.MoveController.IsMoving(follower));
                Assert.IsFalse(bootstrap.MoveController.HasMovementPath(follower));
            }
            finally
            {
                if (hostGo != null)
                    UnityEngine.Object.DestroyImmediate(hostGo);
            }
        }

        [Test]
        public void FollowerNavigationContextRebindsToDestinationLocalMap()
        {
            var hostGo = (GameObject)null;
            try
            {
                var bootstrap = CreateCh01Bootstrap(out hostGo);
                Assert.IsTrue(bootstrap.TryInitialize(), bootstrap.StatusLine);
                SetupParty(bootstrap, out _, out var follower);

                bootstrap.MoveController.BindLocalMapContext(MapA);
                bootstrap.PlayerPartyController.OnLocalMapMaterialized(MapB);

                Assert.AreEqual(MapB, bootstrap.MoveController.BoundLocalMapId);
                Assert.IsNotNull(bootstrap.MoveController.WalkGrid);
                Assert.IsFalse(bootstrap.MoveController.HasMovementPath(follower));
            }
            finally
            {
                if (hostGo != null)
                    UnityEngine.Object.DestroyImmediate(hostGo);
            }
        }

        [Test]
        public void FollowerFollowTargetRestoredAfterTransition()
        {
            var hostGo = (GameObject)null;
            try
            {
                var bootstrap = CreateCh01Bootstrap(out hostGo);
                Assert.IsTrue(bootstrap.TryInitialize(), bootstrap.StatusLine);
                SetupParty(bootstrap, out var active, out var follower);

                SeedFollowerMovingOnMap(
                    bootstrap,
                    follower,
                    MapA,
                    FollowerGoalNearActive(bootstrap, active));
                bootstrap.ViewSpawner.Rebuild(bootstrap.Session);

                bootstrap.PlayerPartyController.OnLocalMapMaterialized(MapB);

                Assert.IsFalse(bootstrap.MoveController.IsMoving(follower));
                var goal = FollowerGoalNearActive(bootstrap, active);
                Assert.IsTrue(
                    bootstrap.MoveController.OrderEntityToWorldPoint(follower, goal, null, issueStop: false));
                Assert.IsTrue(bootstrap.MoveController.IsMoving(follower));
            }
            finally
            {
                if (hostGo != null)
                    UnityEngine.Object.DestroyImmediate(hostGo);
            }
        }

        [Test]
        public void FollowerCanPhysicallyMoveAfterSiteToWildernessTransition()
        {
            var hostGo = (GameObject)null;
            try
            {
                var bootstrap = CreateCh01Bootstrap(out hostGo);
                Assert.IsTrue(bootstrap.TryInitialize(), bootstrap.StatusLine);
                SetupParty(bootstrap, out var active, out var follower);

                SimulateTransitionRecovery(bootstrap, active, follower, MapA, MapB);
                Assert.AreEqual(MapB, bootstrap.MoveController.BoundLocalMapId);
                AssertFollowerCanIssueFreshPath(bootstrap, active, follower);
            }
            finally
            {
                if (hostGo != null)
                    UnityEngine.Object.DestroyImmediate(hostGo);
            }
        }

        [Test]
        public void FollowerCanPhysicallyMoveAfterWildernessToWildernessTransition()
        {
            var hostGo = (GameObject)null;
            try
            {
                var bootstrap = CreateCh01Bootstrap(out hostGo);
                Assert.IsTrue(bootstrap.TryInitialize(), bootstrap.StatusLine);
                SetupParty(bootstrap, out var active, out var follower);

                SimulateTransitionRecovery(bootstrap, active, follower, MapB, MapB);
                AssertFollowerCanIssueFreshPath(bootstrap, active, follower, 12f);

                SimulateTransitionRecovery(
                    bootstrap,
                    active,
                    follower,
                    MapB,
                    WildernessLocalMapFallback.PlainsWildernessLocalMapId);
                Assert.AreEqual(
                    WildernessLocalMapFallback.PlainsWildernessLocalMapId,
                    bootstrap.MoveController.BoundLocalMapId);
                AssertFollowerCanIssueFreshPath(bootstrap, active, follower, 14f);
            }
            finally
            {
                if (hostGo != null)
                    UnityEngine.Object.DestroyImmediate(hostGo);
            }
        }

        [Test]
        public void FollowerCanPhysicallyMoveAfterWildernessToSiteTransition()
        {
            var hostGo = (GameObject)null;
            try
            {
                var bootstrap = CreateCh01Bootstrap(out hostGo);
                Assert.IsTrue(bootstrap.TryInitialize(), bootstrap.StatusLine);
                SetupParty(bootstrap, out var active, out var follower);

                SimulateTransitionRecovery(bootstrap, active, follower, MapB, MapC);
                Assert.AreEqual(MapC, bootstrap.MoveController.BoundLocalMapId);
                AssertFollowerCanIssueFreshPath(bootstrap, active, follower, 8f);
            }
            finally
            {
                if (hostGo != null)
                    UnityEngine.Object.DestroyImmediate(hostGo);
            }
        }

        [Test]
        public void FollowerStaleIsMovingStateDoesNotSurviveInvalidPath()
        {
            var hostGo = (GameObject)null;
            try
            {
                var bootstrap = CreateCh01Bootstrap(out hostGo);
                Assert.IsTrue(bootstrap.TryInitialize(), bootstrap.StatusLine);
                SetupParty(bootstrap, out var active, out var follower);

                SeedFollowerMovingOnMap(
                    bootstrap,
                    follower,
                    MapA,
                    FollowerGoalNearActive(bootstrap, active));
                bootstrap.ViewSpawner.Rebuild(bootstrap.Session);

                Assert.IsFalse(bootstrap.MoveController.IsMoving(follower));
                Assert.IsTrue(bootstrap.ViewSpawner.Registry.TryGet(follower, out var view));
                Assert.AreNotEqual("\u79fb\u52a8\u4e2d", view.ActivityText);
            }
            finally
            {
                if (hostGo != null)
                    UnityEngine.Object.DestroyImmediate(hostGo);
            }
        }

        [Test]
        public void FollowerPathCannotReferencePreviousLocalMap()
        {
            var hostGo = (GameObject)null;
            try
            {
                var bootstrap = CreateCh01Bootstrap(out hostGo);
                Assert.IsTrue(bootstrap.TryInitialize(), bootstrap.StatusLine);
                SetupParty(bootstrap, out var active, out var follower);

                bootstrap.MoveController.BindLocalMapContext(MapA);
                SeedFollowerMovingOnMap(
                    bootstrap,
                    follower,
                    MapA,
                    FollowerGoalNearActive(bootstrap, active));

                bootstrap.PlayerPartyController.OnLocalMapMaterialized(MapB);

                Assert.IsFalse(bootstrap.MoveController.TryGetPathLocalMapId(follower, out _));
                Assert.IsFalse(bootstrap.MoveController.HasMovementPath(follower));
                bootstrap.MoveController.BindLocalMapContext(MapB);
                Assert.IsTrue(
                    bootstrap.MoveController.OrderEntityToWorldPoint(
                        follower,
                        FollowerGoalNearActive(bootstrap, active),
                        null,
                        issueStop: false));
                Assert.IsTrue(bootstrap.MoveController.TryGetPathLocalMapId(follower, out var rebound));
                Assert.AreEqual(MapB, rebound);
            }
            finally
            {
                if (hostGo != null)
                    UnityEngine.Object.DestroyImmediate(hostGo);
            }
        }

        [Test]
        public void MultipleConsecutiveLocalMapTransitionsPreserveFollowerMovement()
        {
            var hostGo = (GameObject)null;
            try
            {
                var bootstrap = CreateCh01Bootstrap(out hostGo);
                Assert.IsTrue(bootstrap.TryInitialize(), bootstrap.StatusLine);
                SetupParty(bootstrap, out var active, out var follower);

                var world = bootstrap.Session.World;
                var party = bootstrap.Session.PlayerParty;
                var maps = new[] { MapA, MapB, MapB, MapC };
                for (var i = 0; i < maps.Length; i++)
                {
                    bootstrap.PlayerPartyController.OnLocalMapMaterialized(maps[i]);
                    if (i > 0)
                    {
                        SeedFollowerMovingOnMap(
                            bootstrap,
                            follower,
                            maps[i - 1],
                            FollowerGoalNearActive(bootstrap, active));
                        bootstrap.ViewSpawner.Rebuild(bootstrap.Session);
                    }

                    bootstrap.PlayerPartyController.OnLocalMapMaterialized(maps[i]);
                    Assert.AreEqual(maps[i], bootstrap.MoveController.BoundLocalMapId);
                    Assert.IsFalse(bootstrap.MoveController.IsMoving(follower));
                    Assert.IsTrue(
                        bootstrap.MoveController.OrderEntityToWorldPoint(
                            follower,
                            FollowerGoalNearActive(bootstrap, active, 6f + i),
                            null,
                            issueStop: false),
                        "Follower should accept a fresh path on map " + maps[i]);
                    Assert.IsTrue(bootstrap.MoveController.IsMoving(follower));
                }
            }
            finally
            {
                if (hostGo != null)
                    UnityEngine.Object.DestroyImmediate(hostGo);
            }
        }

        [Test]
        public void ActiveAndFollowerRemainSameDomainCharacters()
        {
            var hostGo = (GameObject)null;
            try
            {
                var bootstrap = CreateCh01Bootstrap(out hostGo);
                Assert.IsTrue(bootstrap.TryInitialize(), bootstrap.StatusLine);
                SetupParty(bootstrap, out var active, out var follower);

                var activeBefore = active;
                var followerBefore = follower;

                SeedFollowerMovingOnMap(
                    bootstrap,
                    follower,
                    MapA,
                    FollowerGoalNearActive(bootstrap, active));
                bootstrap.PlayerPartyController.OnLocalMapMaterialized(MapB);

                Assert.AreEqual(activeBefore, bootstrap.Session.PlayerParty.ActiveCharacterId);
                Assert.AreEqual(followerBefore, bootstrap.Session.PlayerParty.Members[1]);
                Assert.IsTrue(bootstrap.Session.World.Entities.TryGet(followerBefore, out var ent));
                Assert.IsTrue(ent.TryGet<IdentityComponent>(out _));
            }
            finally
            {
                if (hostGo != null)
                    UnityEngine.Object.DestroyImmediate(hostGo);
            }
        }
    }
}
