using System.Collections.Generic;
using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Tests
{
    /// <summary>
    /// Phase 5R-B7A：WorldSite 是 Surface Context overlay，不改变 PlayerParty path topology。
    /// </summary>
    public sealed class WorldSiteSurfacePassabilityTests
    {
        const float HexSize = 1f;
        static readonly HexCoord Start = new HexCoord(0, 2);
        static readonly HexCoord Goal = new HexCoord(8, 2);
        static readonly HexCoord SiteA = new HexCoord(3, 2);
        static readonly HexCoord SiteB = new HexCoord(4, 2);

        static SimulationWorld BuildWorld(bool withSite = true)
        {
            var world = new SimulationWorld();
            world.HexWorld.MapId = "test:b7a";
            world.HexWorld.HexSize = HexSize;
            world.HexWorld.FillRectangle(9, 5, HexTerrainType.Plain);
            if (withSite)
            {
                var site = new WorldSite
                {
                    SiteId = "test:through_site",
                    DisplayName = "Through Site",
                    AnchorHex = SiteA,
                    PresenceHex = SiteA,
                    LocalMapId = "test:site_map",
                };
                site.SetFootprint(new[] { SiteA, SiteB });
                world.Strategic.Sites.Register(site);
            }

            return world;
        }

        static PlayerPartyRuntime NewParty()
        {
            var party = new PlayerPartyRuntime();
            Assert.IsTrue(party.TryInitialize(new EntityId(1), out _));
            return party;
        }

        static List<HexCoord> BeginRoute(SimulationWorld world, HexCoord start, HexCoord goal)
        {
            var party = NewParty();
            HexMath.ToWorldPosition(start, HexSize, out var x, out var y);
            world.PlayerPartyTravel.SetAtWorldPosition(new WorldVec2(x, y), start);
            var result = PlayerPartyHexTravelService.BeginTravel(world, party, goal);
            Assert.IsTrue(result.IsSuccess, result.IsFailure ? result.Error.ToString() : string.Empty);
            return new List<HexCoord>(world.PlayerPartyTravel.HexPath);
        }

        [Test]
        public void B7A_01_NonTargetSiteOnDirectPath_IsTraversable()
        {
            var path = BeginRoute(BuildWorld(), Start, Goal);
            Assert.Contains(SiteA, path);
            Assert.Contains(SiteB, path);
        }

        [Test]
        public void B7A_02_AddingSiteOverlay_DoesNotChangePathOrCost()
        {
            var without = BeginRoute(BuildWorld(false), Start, Goal);
            var with = BeginRoute(BuildWorld(true), Start, Goal);
            CollectionAssert.AreEqual(without, with);
            Assert.AreEqual(PathCost(BuildWorld(false), without), PathCost(BuildWorld(true), with), 0.0001f);
        }

        [Test]
        public void B7A_03_AnchorChange_DoesNotChangeThroughRoute()
        {
            var beforeWorld = BuildWorld();
            var before = BeginRoute(beforeWorld, Start, Goal);
            var afterWorld = BuildWorld();
            Assert.IsTrue(afterWorld.Strategic.Sites.TryGet("test:through_site", out var site));
            site.AnchorHex = SiteB;
            var after = BeginRoute(afterWorld, Start, Goal);
            CollectionAssert.AreEqual(before, after);
        }

        [Test]
        public void B7A_04_PresenceChange_DoesNotChangeThroughRoute()
        {
            var before = BeginRoute(BuildWorld(), Start, Goal);
            var afterWorld = BuildWorld();
            Assert.IsTrue(afterWorld.Strategic.Sites.TryGet("test:through_site", out var site));
            site.PresenceHex = SiteB;
            var after = BeginRoute(afterWorld, Start, Goal);
            CollectionAssert.AreEqual(before, after);
        }

        [Test]
        public void B7A_05_TargetSite_RemainsWholeFootprintGoalSet()
        {
            var world = BuildWorld();
            var party = NewParty();
            HexMath.ToWorldPosition(Start, HexSize, out var x, out var y);
            world.PlayerPartyTravel.SetAtWorldPosition(new WorldVec2(x, y), Start);
            var result = PlayerPartyHexTravelService.BeginTravel(
                world, party, SiteB, "test:through_site");
            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(world.Strategic.Sites.TryGet("test:through_site", out var site));
            Assert.IsTrue(site.OccupiesHex(world.PlayerPartyTravel.DestinationHex));
            Assert.AreEqual(SiteA, world.PlayerPartyTravel.DestinationHex,
                "planner chooses the lowest-cost ingress in the whole footprint");
        }

        [Test]
        public void B7A_06_WorldExecutor_PassesThroughSite_WithoutCompletingOrder()
        {
            var world = BuildWorld();
            BeginRoute(world, Start, Goal);
            var motion = world.PlayerPartyTravel;
            var sawSite = false;
            for (var i = 0; i < 200 && motion.IsMoving; i++)
            {
                PlayerPartyHexTravelService.AdvanceDistanceBudget(world, 0.25f);
                if (motion.LocationKind == PlayerPartyLocationKind.AtWorldSite)
                {
                    sawSite = true;
                    Assert.IsTrue(motion.IsMoving, "through-Site must preserve AutoTravel");
                    Assert.AreEqual(Goal, motion.DestinationHex);
                }
            }

            Assert.IsTrue(sawSite, "route entered Site Context");
            Assert.IsFalse(motion.IsMoving, "route eventually completes at final surface goal");
            Assert.AreEqual(Goal, motion.CurrentHex);
            Assert.AreEqual(PlayerPartyLocationKind.AtWorldPosition, motion.LocationKind);
        }

        [Test]
        public void B7A_07_DeparturePreview_StartsAtSameFormalExitAsExecutor()
        {
            var world = BuildWorld();
            Assert.IsTrue(world.Strategic.Sites.TryGet("test:through_site", out var site));
            var party = NewParty();
            world.PlayerPartyTravel.SetAtWorldSite(site.SiteId, SiteA, HexSize);
            var result = PlayerPartyHexTravelService.BeginTravel(world, party, Goal);
            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(PlayerPartyWorldLocationQuery.TryResolveRouteStartHex(
                world, world.PlayerPartyTravel, out _, out var pathIndex));
            Assert.Less(pathIndex, world.PlayerPartyTravel.HexPathCount);
            Assert.AreEqual(
                world.PlayerPartyTravel.SiteDepartureExitHex,
                world.PlayerPartyTravel.HexPath[pathIndex]);
            Assert.IsFalse(site.OccupiesHex(world.PlayerPartyTravel.HexPath[pathIndex]));
        }

        [Test]
        public void B7A_08_LocalVisibleThroughSite_UsesSameHexPathEgress()
        {
            var world = BuildWorld();
            BeginRoute(world, Start, Goal);
            var motion = world.PlayerPartyTravel;
            Assert.IsTrue(world.Strategic.Sites.TryGet("test:through_site", out var site));
            var entryIndex = IndexOf(motion.HexPath, SiteA);
            Assert.Greater(entryIndex, 0);
            HexMath.ToWorldPosition(SiteA, HexSize, out var x, out var y);
            Assert.IsTrue(PlayerPartyHexTravelService.TryCommitThroughSitePassage(
                world, motion, site, new WorldVec2(x, y), SiteA, HexSize, out var resolvedEntry));
            Assert.AreEqual(entryIndex, resolvedEntry);
            Assert.AreEqual(SiteB, motion.SiteDepartureFootprintHex);
            Assert.AreEqual(motion.HexPath[entryIndex + 2], motion.SiteDepartureExitHex);
            Assert.IsTrue(motion.IsMoving);
            Assert.AreEqual(Goal, motion.DestinationHex);
        }

        [Test]
        public void B7A_09_WaterWall_RemainsImpassable()
        {
            var world = BuildWorld(false);
            for (var r = 0; r < 5; r++)
            {
                Assert.IsTrue(world.HexWorld.TryGetTile(new HexCoord(4, r), out var tile));
                tile.Terrain = HexTerrainType.Water;
            }
            var party = NewParty();
            HexMath.ToWorldPosition(Start, HexSize, out var x, out var y);
            world.PlayerPartyTravel.SetAtWorldPosition(new WorldVec2(x, y), Start);
            Assert.IsTrue(PlayerPartyHexTravelService.BeginTravel(world, party, Goal).IsFailure);
        }

        [Test]
        public void B7A_10_ExplicitBlockedTerrain_RemainsImpassable()
        {
            var world = BuildWorld(false);
            for (var r = 0; r < 5; r++)
            {
                Assert.IsTrue(world.HexWorld.TryGetTile(new HexCoord(4, r), out var tile));
                tile.IsPassable = false;
            }
            var party = NewParty();
            HexMath.ToWorldPosition(Start, HexSize, out var x, out var y);
            world.PlayerPartyTravel.SetAtWorldPosition(new WorldVec2(x, y), Start);
            Assert.IsTrue(PlayerPartyHexTravelService.BeginTravel(world, party, Goal).IsFailure);
        }

        static int IndexOf(IReadOnlyList<HexCoord> path, HexCoord hex)
        {
            for (var i = 0; i < path.Count; i++)
            {
                if (path[i].Equals(hex))
                    return i;
            }
            return -1;
        }

        static float PathCost(SimulationWorld world, IReadOnlyList<HexCoord> path)
        {
            var total = 0f;
            for (var i = 0; i < path.Count; i++)
            {
                Assert.IsTrue(world.HexWorld.TryGetTile(path[i], out var tile));
                total += tile.ResolveMovementCost();
            }
            return total;
        }
    }
}
