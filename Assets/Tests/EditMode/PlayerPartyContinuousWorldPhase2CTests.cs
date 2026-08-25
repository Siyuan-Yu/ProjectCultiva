using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Persistence;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;
using XianXia.Data.Bootstrap;
using XianXia.Data.Serialization;

namespace XianXia.Tests
{
    public sealed class PlayerPartyContinuousWorldPhase2CTests
    {
        const string FactionA = "test:faction_a";
        const float FloatTol = 0.05f;

        static SimulationWorld BuildTinyTravelWorld(
            out WorldSite siteA,
            out WorldSite siteB,
            out HexCoord midHex)
        {
            var world = new SimulationWorld();
            world.HexWorld.MapId = "test:tiny_travel_world";
            world.HexWorld.HexSize = 1f;
            world.HexWorld.FillRectangle(20, 12, HexTerrainType.Plain);
            for (var r = 0; r < 12; r++)
            for (var q = 0; q < 20; q++)
            {
                if (!world.HexWorld.TryGetCell(new HexCoord(q, r), out var cell) || cell == null)
                    continue;
                cell.IsPassable = true;
                if (q >= 8 && q <= 11 && r >= 2 && r <= 5)
                    cell.Terrain = HexTerrainType.Forest;
                if (q >= 14 && q <= 15 && r >= 6 && r <= 9)
                    cell.Terrain = HexTerrainType.Mountain;
            }

            var aAnchor = new HexCoord(2, 4);
            var aPresence = new HexCoord(3, 4);
            siteA = new WorldSite
            {
                SiteId = "test:site_huangcun",
                DisplayName = "青石荒村",
                AnchorHex = aAnchor,
                PresenceHex = aPresence,
                LocalMapId = "base:map_ch01_reference",
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
                LocalMapId = "base:map_site_chengzhen",
            };
            siteB.SetFootprint(new[]
            {
                bAnchor, new HexCoord(11, 4), new HexCoord(10, 5), new HexCoord(11, 5),
            });
            WorldSiteRegistrationService.RegisterSiteOnGrid(world, siteB);

            midHex = new HexCoord(6, 4);
            Assert.IsTrue(world.HexWorld.TryGetTile(midHex, out var mid) && mid.IsPassable);
            return world;
        }

        static EntityId Spawn(SimulationWorld world, string name)
        {
            var created = world.Entities.CreateCharacter(new DefinitionId("test", name), name);
            Assert.IsTrue(created.IsSuccess);
            created.Value.Get<FactionMembershipComponent>().Assign(FactionA, FactionRoleKind.Member);
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
                Assert.IsTrue(party.TryAddMember(world, new System.Collections.Generic.List<EntityId>(members), members[i], out var err), err);
            return party;
        }

        static void ForceAdvanceToDestination(SimulationWorld world, int maxTicks = 5000)
        {
            for (var i = 0; i < maxTicks && world.PlayerPartyTravel.IsMoving; i++)
                PlayerPartyHexTravelService.AdvanceAll(world, 1);
        }

        static WorldVec2 HexCenter(SimulationWorld world, HexCoord hex)
        {
            var size = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            HexMath.ToWorldPosition(hex, size, out var x, out var y);
            return new WorldVec2(x, y);
        }

        static bool IsNearHexCenter(SimulationWorld world, WorldVec2 pos, HexCoord hex, float tol = FloatTol)
        {
            return WorldVec2.Distance(pos, HexCenter(world, hex)) < tol;
        }

        static void SetPartyOffCenter(SimulationWorld world, HexCoord hex, float normX, float normY)
        {
            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            HexMath.ToWorldPosition(hex, hexSize, out var cx, out var cy);
            var radius = hexSize * WildernessLocalWorldProjection.InteriorRadiusFactor;
            var pos = new WorldVec2(cx + normX * radius, cy + normY * radius);
            var derived = HexMath.WorldToHex(pos.X, pos.Y, hexSize);
            world.PlayerPartyTravel.SetAtWorldPosition(pos, derived);
        }

        static WildernessLocalWorldProjection.WildernessLocalMapBounds DefaultWildernessBounds() =>
            WildernessLocalWorldProjection.WildernessLocalMapBounds.FromOriginSize(0f, 0f, 1f, 16, 16);

        static void BeginTravelFromResolved(
            SimulationWorld world,
            PlayerPartyRuntime party,
            WorldMapPartyTravelCommand.Resolved resolved)
        {
            Assert.IsTrue(PlayerPartyHexTravelService.BeginTravel(
                world,
                party,
                resolved.DestinationHex,
                resolved.TargetSiteId).IsSuccess);
        }

        static void SetHexTerrain(SimulationWorld world, HexCoord hex, HexTerrainType terrain)
        {
            Assert.IsTrue(world.HexWorld.TryGetCell(hex, out var cell) && cell != null);
            cell.Terrain = terrain;
            cell.IsPassable = true;
        }

        static WorldVec2 PositionNearEdgeTowardNeighbor(
            SimulationWorld world,
            HexCoord fromHex,
            HexCoord toHex,
            float inwardFactor = 0.92f)
        {
            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            var center = HexCenter(world, fromHex);
            var crossPos = WildernessLocalWorldProjection.ComputeCrossEdgeWorldPosition(
                fromHex, toHex, center, hexSize);
            return new WorldVec2(
                center.X + (crossPos.X - center.X) * inwardFactor,
                center.Y + (crossPos.Y - center.Y) * inwardFactor);
        }

        static WorldVec2 PositionAcrossEdgeInNeighbor(
            SimulationWorld world,
            HexCoord fromHex,
            HexCoord toHex)
        {
            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            return WildernessLocalWorldProjection.ComputeCrossEdgeWorldPosition(
                fromHex, toHex, HexCenter(world, fromHex), hexSize);
        }

        static void SetPartyAtSiteOpen(SimulationWorld world, WorldSite site, PlayerPartyRuntime party)
        {
            Assert.IsTrue(PlayerPartyHexTravelService.EnterWorldSiteAsParty(world, party, site).IsSuccess);
            Assert.AreEqual(PlayerPartyLocationKind.AtWorldSite, world.PlayerPartyTravel.LocationKind);
        }

        [Test]
        public void CONTINUOUS_01_WorldMapResolve_SameHexDifferentPixels_YieldsSameTargetHex()
        {
            var world = BuildTinyTravelWorld(out _, out _, out var mid);
            const float clickA = 99.123f;
            const float clickB = -42.987f;

            Assert.IsTrue(WorldMapPartyTravelCommand.TryResolve(world, mid, clickA, clickA, out var rA));
            Assert.IsTrue(WorldMapPartyTravelCommand.TryResolve(world, mid, clickB, clickB, out var rB));

            Assert.AreEqual(rA.TargetHex, rB.TargetHex);
            Assert.AreEqual(rA.DestinationHex, rB.DestinationHex);
            Assert.AreEqual(rA.TargetSiteId, rB.TargetSiteId);
            Assert.AreEqual(rA.CanonicalDestinationWorld.X, rB.CanonicalDestinationWorld.X, FloatTol);
            Assert.AreEqual(rA.CanonicalDestinationWorld.Y, rB.CanonicalDestinationWorld.Y, FloatTol);
        }

        [Test]
        public void CONTINUOUS_02_WorldMapResolve_IgnoresClickedWorldPosition()
        {
            var world = BuildTinyTravelWorld(out _, out _, out var mid);
            Assert.IsTrue(WorldMapPartyTravelCommand.TryResolve(world, mid, 500f, -300f, out var resolved));

            var center = HexCenter(world, resolved.DestinationHex);
            Assert.AreEqual(center.X, resolved.CanonicalDestinationWorld.X, FloatTol);
            Assert.AreEqual(center.Y, resolved.CanonicalDestinationWorld.Y, FloatTol);
            Assert.Greater(WorldVec2.Distance(resolved.CanonicalDestinationWorld, new WorldVec2(500f, -300f)), 0.1f);
        }

        [Test]
        public void CONTINUOUS_03_WorldMapResolve_CanonicalDestination_IsHexCenter()
        {
            var world = BuildTinyTravelWorld(out _, out _, out var mid);
            Assert.IsTrue(WorldMapPartyTravelCommand.TryResolve(world, mid, out var resolved));

            HexMath.ToWorldPosition(resolved.DestinationHex, world.HexWorld.HexSize, out var cx, out var cy);
            Assert.AreEqual(cx, resolved.CanonicalDestinationWorld.X, FloatTol);
            Assert.AreEqual(cy, resolved.CanonicalDestinationWorld.Y, FloatTol);
        }

        [Test]
        public void CONTINUOUS_04_WorldPosition_CanExistAwayFromHexCenter()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out _);
            var a = Spawn(world, "LinQing");
            BuildParty(world, siteA, a);
            world.PlayerPartyTravel.SetIdleAt(siteA.PresenceHex);

            SetPartyOffCenter(world, siteA.PresenceHex, 0.35f, -0.2f);
            var pos = world.PlayerPartyTravel.WorldPosition;
            Assert.IsFalse(IsNearHexCenter(world, pos, siteA.PresenceHex));
            Assert.AreEqual(siteA.PresenceHex, world.PlayerPartyTravel.CurrentHex);
        }

        [Test]
        public void CONTINUOUS_05_CurrentHex_DerivesFromWorldPosition()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out _);
            var a = Spawn(world, "LinQing");
            BuildParty(world, siteA, a);
            SetPartyOffCenter(world, siteA.PresenceHex, -0.25f, 0.3f);

            var pos = world.PlayerPartyTravel.WorldPosition;
            var expected = HexMath.WorldToHex(pos.X, pos.Y, world.HexWorld.HexSize);
            Assert.AreEqual(expected, world.PlayerPartyTravel.CurrentHex);
        }

        [Test]
        public void CONTINUOUS_06_AutoTravel_DoesNotTeleportOneHexPerTick()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            world.PlayerPartyTravel.SetIdleAt(siteA.PresenceHex);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);

            Assert.IsTrue(WorldMapPartyTravelCommand.TryResolve(world, mid, out var resolved));
            BeginTravelFromResolved(world, party, resolved);
            var startPos = world.PlayerPartyTravel.WorldPosition;
            var startDist = 0f;
            var sawBetween = false;

            for (var i = 0; i < 4 && world.PlayerPartyTravel.IsMoving; i++)
            {
                PlayerPartyHexTravelService.AdvanceAll(world, 1);
                var pos = world.PlayerPartyTravel.WorldPosition;
                var distFromStart = WorldVec2.Distance(startPos, pos);
                Assert.Greater(distFromStart, startDist - 0.001f, "Distance from start should increase gradually.");
                startDist = distFromStart;

                if (!IsNearHexCenter(world, pos, world.PlayerPartyTravel.CurrentHex))
                    sawBetween = true;
            }

            Assert.IsTrue(sawBetween, "After a few ticks WorldPosition should lie between hex centers.");
        }

        [Test]
        public void CONTINUOUS_07_AutoTravel_WorldPositionAdvancesContinuouslyAlongRoute()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            world.PlayerPartyTravel.SetIdleAt(siteA.PresenceHex);

            Assert.IsTrue(PlayerPartyHexTravelService.BeginTravel(world, party, mid).IsSuccess);
            Assert.IsTrue(world.PlayerPartyTravel.TryGetActiveSegmentWorld(
                world.HexWorld.HexSize, out _, out var segmentEnd));

            var prevDistToEnd = WorldVec2.Distance(world.PlayerPartyTravel.WorldPosition, segmentEnd);
            for (var i = 0; i < 6 && world.PlayerPartyTravel.IsMoving; i++)
            {
                PlayerPartyHexTravelService.AdvanceAll(world, 1);
                if (!world.PlayerPartyTravel.TryGetActiveSegmentWorld(
                        world.HexWorld.HexSize, out _, out segmentEnd))
                    break;

                var distToEnd = WorldVec2.Distance(world.PlayerPartyTravel.WorldPosition, segmentEnd);
                Assert.LessOrEqual(distToEnd, prevDistToEnd + FloatTol);
                prevDistToEnd = distToEnd;
            }
        }

        [Test]
        public void CONTINUOUS_08_AutoTravelFromOffCenter_DoesNotSnapToCurrentHexCenterFirst()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            world.PlayerPartyTravel.SetIdleAt(siteA.PresenceHex);
            SetPartyOffCenter(world, siteA.PresenceHex, 0.4f, 0.1f);
            var offCenter = world.PlayerPartyTravel.WorldPosition;
            Assert.IsFalse(IsNearHexCenter(world, offCenter, siteA.PresenceHex));

            Assert.IsTrue(PlayerPartyHexTravelService.BeginTravel(world, party, mid).IsSuccess);
            Assert.IsFalse(IsNearHexCenter(world, world.PlayerPartyTravel.WorldPosition, siteA.PresenceHex));

            PlayerPartyHexTravelService.AdvanceAll(world, 1);
            var afterTick = world.PlayerPartyTravel.WorldPosition;
            Assert.IsFalse(IsNearHexCenter(world, afterTick, siteA.PresenceHex));
            Assert.Greater(WorldVec2.Distance(offCenter, afterTick), 0.001f);
        }

        [Test]
        public void CONTINUOUS_09_CancelAutoTravel_PreservesExactWorldPosition()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            SetPartyOffCenter(world, siteA.PresenceHex, -0.15f, 0.35f);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);

            Assert.IsTrue(PlayerPartyHexTravelService.BeginTravel(world, party, mid).IsSuccess);
            for (var i = 0; i < 3 && world.PlayerPartyTravel.IsMoving; i++)
                PlayerPartyHexTravelService.AdvanceAll(world, 1);

            var frozen = world.PlayerPartyTravel.WorldPosition;
            Assert.IsTrue(PlayerPartyHexTravelService.CancelTravel(world, party).IsSuccess);
            Assert.AreEqual(frozen.X, world.PlayerPartyTravel.WorldPosition.X, FloatTol);
            Assert.AreEqual(frozen.Y, world.PlayerPartyTravel.WorldPosition.Y, FloatTol);
            Assert.IsFalse(world.PlayerPartyTravel.IsMoving);
        }

        [Test]
        public void CONTINUOUS_10_CloseWorldMapTakeover_CancelsAndPreservesWorldPosition()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            SetPartyOffCenter(world, siteA.PresenceHex, 0.2f, -0.25f);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);

            Assert.IsTrue(PlayerPartyHexTravelService.BeginTravel(world, party, mid).IsSuccess);
            for (var i = 0; i < 4 && world.PlayerPartyTravel.IsMoving; i++)
                PlayerPartyHexTravelService.AdvanceAll(world, 1);

            var frozen = world.PlayerPartyTravel.WorldPosition;
            Assert.IsTrue(PlayerPartyHexTravelService.CloseWorldMapTakeover(world, party).IsSuccess);
            Assert.AreEqual(frozen.X, world.PlayerPartyTravel.WorldPosition.X, FloatTol);
            Assert.AreEqual(frozen.Y, world.PlayerPartyTravel.WorldPosition.Y, FloatTol);
            Assert.IsFalse(world.PlayerPartyTravel.IsMoving);
        }

        [Test]
        public void CONTINUOUS_11_CloseWorldMapTakeover_ResolvesCorrectWildernessHex()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            SetPartyOffCenter(world, siteA.PresenceHex, 0.3f, 0.05f);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);

            Assert.IsTrue(PlayerPartyHexTravelService.BeginTravel(world, party, mid).IsSuccess);
            for (var i = 0; i < 5 && world.PlayerPartyTravel.IsMoving; i++)
                PlayerPartyHexTravelService.AdvanceAll(world, 1);

            var pos = world.PlayerPartyTravel.WorldPosition;
            var expectedHex = HexMath.WorldToHex(pos.X, pos.Y, world.HexWorld.HexSize);
            Assert.IsTrue(PlayerPartyHexTravelService.CloseWorldMapTakeover(world, party).IsSuccess);
            Assert.AreEqual(expectedHex, world.PlayerPartyTravel.CurrentHex);
            Assert.AreNotEqual(siteA.PresenceHex, world.PlayerPartyTravel.CurrentHex);
        }

        [Test]
        public void CONTINUOUS_12_WildernessLocalWorldProjection_RoundtripApproximate()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out _);
            var hex = siteA.PresenceHex;
            var bounds = DefaultWildernessBounds();
            var hexSize = world.HexWorld.HexSize;
            const float localX = 9.5f;
            const float localY = 6.25f;

            Assert.IsTrue(WildernessLocalWorldProjection.TryProjectLocalToWorld(
                hex, localX, localY, bounds, hexSize, out var worldPos));
            Assert.IsTrue(WildernessLocalWorldProjection.TryProjectWorldToLocal(
                worldPos, bounds, hexSize, out var roundX, out var roundY));

            Assert.AreEqual(localX, roundX, 0.15f);
            Assert.AreEqual(localY, roundY, 0.15f);
        }

        [Test]
        public void CONTINUOUS_13_TryCrossWildernessEdge_NE_EntersNeNeighbor()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);

            const int neDir = 1;
            var neighbor = HexMath.Neighbor(mid, neDir);
            SetPartyOffCenter(world, mid, 0.4f, -0.35f);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);

            Assert.IsTrue(PlayerPartyWildernessTransitionService.TryCrossWildernessEdge(world, party, neDir).IsSuccess);
            Assert.AreEqual(neighbor, world.PlayerPartyTravel.CurrentHex);
            Assert.AreEqual(PlayerPartyLocationKind.AtWorldPosition, world.PlayerPartyTravel.LocationKind);
        }

        [Test]
        public void CONTINUOUS_14_NeighborSpawn_OppositeEdgeViaGetLocalPositionNearEdge()
        {
            var bounds = DefaultWildernessBounds();
            const int entryDir = 1;
            var opposite = WildernessLocalWorldProjection.OppositeDirection(entryDir);
            WildernessLocalWorldProjection.GetLocalPositionNearEdge(bounds, opposite, out var lx, out var ly);

            Assert.Less(lx, bounds.MaxX);
            Assert.Greater(lx, bounds.MinX);
            Assert.Less(ly, bounds.MaxY);
            Assert.Greater(ly, bounds.MinY);

            var world = BuildTinyTravelWorld(out _, out _, out var mid);
            var neighbor = HexMath.Neighbor(mid, entryDir);
            Assert.IsTrue(WildernessLocalWorldProjection.TryProjectLocalToWorld(
                neighbor, lx, ly, bounds, world.HexWorld.HexSize, out var worldPos));
            Assert.AreEqual(neighbor, HexMath.WorldToHex(worldPos.X, worldPos.Y, world.HexWorld.HexSize));
        }

        [Test]
        public void CONTINUOUS_15_ManualBoundaryTransition_DoesNotSnapToNeighborCenter()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);

            const int neDir = 1;
            var neighbor = HexMath.Neighbor(mid, neDir);
            SetPartyOffCenter(world, mid, 0.35f, -0.3f);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);

            Assert.IsTrue(PlayerPartyWildernessTransitionService.TryCrossWildernessEdge(world, party, neDir).IsSuccess);
            Assert.IsFalse(IsNearHexCenter(world, world.PlayerPartyTravel.WorldPosition, neighbor));
        }

        [Test]
        public void CONTINUOUS_16_TravelToWorldSite_ArrivalSetsAtWorldSite()
        {
            var world = BuildTinyTravelWorld(out var siteA, out var siteB, out _);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            world.PlayerPartyTravel.SetIdleAt(siteA.PresenceHex);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);

            Assert.IsTrue(WorldMapPartyTravelCommand.TryResolve(world, siteB.PresenceHex, out var resolved));
            BeginTravelFromResolved(world, party, resolved);
            ForceAdvanceToDestination(world);

            Assert.AreEqual(PlayerPartyLocationKind.AtWorldSite, world.PlayerPartyTravel.LocationKind);
            Assert.AreEqual(siteB.SiteId, world.PlayerPartyTravel.SiteId);
            Assert.AreEqual(siteB.PresenceHex, world.PlayerPartyTravel.CurrentHex);
        }

        [Test]
        public void CONTINUOUS_17_SiteInternalLocalMovement_DoesNotChangeWorldProjection()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out _);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            Assert.IsTrue(PlayerPartyHexTravelService.EnterWorldSiteAsParty(world, party, siteA).IsSuccess);

            var beforeHex = world.PlayerPartyTravel.CurrentHex;
            var bounds = DefaultWildernessBounds();
            Assert.IsTrue(PlayerPartyWildernessTransitionService.TrySyncLocalMovementToWorldPosition(
                world, 12f, 3f, bounds).IsSuccess);

            Assert.AreEqual(PlayerPartyLocationKind.AtWorldSite, world.PlayerPartyTravel.LocationKind);
            Assert.AreEqual(beforeHex, world.PlayerPartyTravel.CurrentHex);
            Assert.AreEqual(siteA.PresenceHex, world.PlayerPartyTravel.CurrentHex);
        }

        [Test]
        public void CONTINUOUS_18_WorldSiteProjection_EqualsPresenceHex()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out _);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            Assert.IsTrue(PlayerPartyHexTravelService.EnterWorldSiteAsParty(world, party, siteA).IsSuccess);

            Assert.IsTrue(PlayerPartyHexTravelService.TryResolvePartyWorldPosition(world, party, out var pos));
            var center = HexCenter(world, siteA.PresenceHex);
            Assert.AreEqual(center.X, pos.X, FloatTol);
            Assert.AreEqual(center.Y, pos.Y, FloatTol);
            Assert.AreNotEqual(siteA.AnchorHex, siteA.PresenceHex);
        }

        [Test]
        public void CONTINUOUS_19_LeaveWorldSite_TransitionsToAtWorldPosition()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out _);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            Assert.IsTrue(PlayerPartyHexTravelService.EnterWorldSiteAsParty(world, party, siteA).IsSuccess);

            const int exitDir = 3;
            Assert.IsTrue(PlayerPartyWildernessTransitionService.TryExitWorldSiteByDirection(
                world, party, exitDir).IsSuccess);

            Assert.AreEqual(PlayerPartyLocationKind.AtWorldPosition, world.PlayerPartyTravel.LocationKind);
            Assert.IsTrue(string.IsNullOrEmpty(world.PlayerPartyTravel.SiteId));
            Assert.IsFalse(world.Strategic.Sites.TryGetAtHex(world.PlayerPartyTravel.CurrentHex, out _));
        }

        [Test]
        public void CONTINUOUS_20_SingleHexAndMultiHexSite_ShareAtWorldSiteSemantics()
        {
            var world = BuildTinyTravelWorld(out var siteA, out var siteB, out _);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);

            Assert.IsTrue(PlayerPartyHexTravelService.EnterWorldSiteAsParty(world, party, siteA).IsSuccess);
            Assert.AreEqual(PlayerPartyLocationKind.AtWorldSite, world.PlayerPartyTravel.LocationKind);
            Assert.AreEqual(siteA.PresenceHex, world.PlayerPartyTravel.CurrentHex);
            Assert.AreNotEqual(siteA.AnchorHex, siteA.PresenceHex);

            Assert.IsTrue(PlayerPartyHexTravelService.EnterWorldSiteAsParty(world, party, siteB).IsSuccess);
            Assert.AreEqual(PlayerPartyLocationKind.AtWorldSite, world.PlayerPartyTravel.LocationKind);
            Assert.AreEqual(siteB.PresenceHex, world.PlayerPartyTravel.CurrentHex);
            Assert.AreEqual(siteB.AnchorHex, siteB.PresenceHex);
        }

        [Test]
        public void CONTINUOUS_21_PartyMembers_ShareOneWorldLocation()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            var b = Spawn(world, "WangChen");
            var party = BuildParty(world, siteA, a, b);
            SetPartyOffCenter(world, siteA.PresenceHex, 0.1f, -0.2f);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);

            Assert.IsTrue(PlayerPartyHexTravelService.TryResolvePartyWorldPosition(world, party, out var partyPos));
            Assert.IsTrue(CharacterWorldPresenceQuery.TryGetWorldHex(world, a, out var hexA));
            Assert.IsTrue(CharacterWorldPresenceQuery.TryGetWorldHex(world, b, out var hexB));
            Assert.AreEqual(hexA, hexB);
            Assert.AreEqual(world.PlayerPartyTravel.CurrentHex, hexA);

            Assert.IsTrue(PlayerPartyHexTravelService.BeginTravel(world, party, mid).IsSuccess);
            PlayerPartyHexTravelService.AdvanceAll(world, 2);
            Assert.IsTrue(CharacterWorldPresenceQuery.TryGetWorldHex(world, a, out hexA));
            Assert.IsTrue(CharacterWorldPresenceQuery.TryGetWorldHex(world, b, out hexB));
            Assert.AreEqual(hexA, hexB);
            Assert.Less(WorldVec2.Distance(partyPos, world.PlayerPartyTravel.WorldPosition), 2f);
        }

        [Test]
        public void CONTINUOUS_22_ContinuousTravel_DoesNotCreateFormalArmy()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            world.PlayerPartyTravel.SetIdleAt(siteA.PresenceHex);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);

            var before = world.Strategic.FormalArmies.Armies.Count;
            Assert.IsTrue(PlayerPartyHexTravelService.BeginTravel(world, party, mid).IsSuccess);
            ForceAdvanceToDestination(world);
            Assert.AreEqual(before, world.Strategic.FormalArmies.Armies.Count);
            Assert.IsFalse(ArmyService.TryGetArmyForCharacter(world, a, out _));
        }

        [Test]
        public void CONTINUOUS_23_BackgroundCharacter_DoesNotMoveWithParty()
        {
            var world = BuildTinyTravelWorld(out var siteA, out var siteB, out _);
            var a = Spawn(world, "LinQing");
            var b = Spawn(world, "WangChen");
            var party = BuildParty(world, siteA, a, b);
            world.PlayerPartyTravel.SetIdleAt(siteA.PresenceHex);

            Assert.IsTrue(party.TryRemoveMember(b, out _));
            SetPartyOffCenter(world, siteA.PresenceHex, 0.25f, 0.1f);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);
            Assert.IsTrue(PlayerPartyHexTravelService.BeginTravel(world, party, siteB.PresenceHex, siteB.SiteId).IsSuccess);
            ForceAdvanceToDestination(world);

            Assert.IsTrue(world.WorldPresence.TryGet(b, out var left));
            Assert.AreEqual(PartyWorldPresenceMode.AtSite, left.Mode);
            Assert.AreEqual(siteA.SiteId, left.SiteId);
            Assert.IsTrue(CharacterWorldPresenceQuery.TryGetWorldHex(world, b, out var leftHex));
            Assert.AreEqual(siteA.PresenceHex, leftHex);
        }

        [Test]
        public void CONTINUOUS_24_Snapshot_PreservesContinuousWorldPosition()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            SetPartyOffCenter(world, siteA.PresenceHex, -0.3f, 0.15f);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);

            Assert.IsTrue(PlayerPartyHexTravelService.BeginTravel(world, party, mid).IsSuccess);
            for (var i = 0; i < 3 && world.PlayerPartyTravel.IsMoving; i++)
                PlayerPartyHexTravelService.AdvanceAll(world, 1);

            var expectedPos = world.PlayerPartyTravel.WorldPosition;
            var expectedHex = world.PlayerPartyTravel.CurrentHex;

            var dto = StrategicSnapshotHelper.Capture(world);
            Assert.IsNotNull(dto.PlayerPartyTravel);
            Assert.IsTrue(dto.PlayerPartyTravel.HasPosition);
            Assert.AreEqual((int)PlayerPartyLocationKind.AtWorldPosition, dto.PlayerPartyTravel.LocationKind);
            Assert.AreEqual(expectedPos.X, dto.PlayerPartyTravel.WorldX, FloatTol);
            Assert.AreEqual(expectedPos.Y, dto.PlayerPartyTravel.WorldY, FloatTol);

            var json = new JsonSnapshotSerializer().Serialize(new WorldSnapshot { Strategic = dto });
            Assert.IsTrue(json.IsSuccess);
            StringAssert.Contains("\"playerPartyTravel\"", json.Value);

            var parsed = new JsonSnapshotSerializer().Deserialize(json.Value);
            Assert.IsTrue(parsed.IsSuccess);
            Assert.IsNotNull(parsed.Value.Strategic.PlayerPartyTravel);

            var restored = new SimulationWorld();
            restored.HexWorld.MapId = world.HexWorld.MapId;
            restored.HexWorld.HexSize = world.HexWorld.HexSize;
            restored.HexWorld.FillRectangle(20, 12, HexTerrainType.Plain);
            StrategicSnapshotHelper.Restore(restored, parsed.Value.Strategic);

            Assert.IsFalse(restored.PlayerPartyTravel.IsMoving);
            Assert.AreEqual(PlayerPartyLocationKind.AtWorldPosition, restored.PlayerPartyTravel.LocationKind);
            Assert.AreEqual(expectedHex, restored.PlayerPartyTravel.CurrentHex);
            Assert.AreEqual(expectedPos.X, restored.PlayerPartyTravel.WorldPosition.X, FloatTol);
            Assert.AreEqual(expectedPos.Y, restored.PlayerPartyTravel.WorldPosition.Y, FloatTol);
        }

        [Test]
        public void ACCEPT_01_OpenCloseWorldMapAtSitePreservesAtWorldSite()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out _);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            SetPartyAtSiteOpen(world, siteA, party);

            Assert.IsTrue(PlayerPartyHexTravelService.PartyLocalMapMatchesAuthoritativeLocation(world, party));
            Assert.IsTrue(PlayerPartyHexTravelService.EnterLocalViewAtCurrentHex(world, party).IsSuccess);

            Assert.AreEqual(PlayerPartyLocationKind.AtWorldSite, world.PlayerPartyTravel.LocationKind);
            Assert.AreNotEqual(PlayerPartyLocationKind.AtWorldPosition, world.PlayerPartyTravel.LocationKind);
        }

        [Test]
        public void ACCEPT_02_OpenCloseWorldMapAtSitePreservesSiteId()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out _);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            SetPartyAtSiteOpen(world, siteA, party);
            var siteIdBefore = world.PlayerPartyTravel.SiteId;

            Assert.IsTrue(PlayerPartyHexTravelService.PartyLocalMapMatchesAuthoritativeLocation(world, party));
            Assert.IsTrue(PlayerPartyHexTravelService.EnterLocalViewAtCurrentHex(world, party).IsSuccess);

            Assert.AreEqual(siteIdBefore, world.PlayerPartyTravel.SiteId);
            Assert.AreEqual(siteA.SiteId, world.PlayerPartyTravel.SiteId);
        }

        [Test]
        public void ACCEPT_03_OpenCloseReturnsSameWorldSiteLocalMapIdOnPartyWorld()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out _);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            SetPartyAtSiteOpen(world, siteA, party);
            var mapBefore = world.PartyWorld.LocalMapId;

            Assert.IsTrue(PlayerPartyHexTravelService.EnterLocalViewAtCurrentHex(world, party).IsSuccess);

            Assert.AreEqual(mapBefore, world.PartyWorld.LocalMapId);
            Assert.AreEqual(siteA.LocalMapId, world.PartyWorld.LocalMapId);
        }

        [Test]
        public void ACCEPT_04_SiteLocalMovementDoesNotChangeWorldLocation()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out _);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            SetPartyAtSiteOpen(world, siteA, party);

            var kindBefore = world.PlayerPartyTravel.LocationKind;
            var siteIdBefore = world.PlayerPartyTravel.SiteId;
            var hexBefore = world.PlayerPartyTravel.CurrentHex;
            var bounds = DefaultWildernessBounds();

            Assert.IsTrue(PlayerPartyWildernessTransitionService.TrySyncLocalMovementToWorldPosition(
                world, 12f, 3f, bounds).IsSuccess);

            Assert.AreEqual(kindBefore, world.PlayerPartyTravel.LocationKind);
            Assert.AreEqual(siteIdBefore, world.PlayerPartyTravel.SiteId);
            Assert.AreEqual(hexBefore, world.PlayerPartyTravel.CurrentHex);
            Assert.AreEqual(siteA.PresenceHex, world.PlayerPartyTravel.CurrentHex);
            Assert.IsTrue(PlayerPartyWorldLocationQuery.TryResolve(world, party, out var resolved));
            Assert.AreEqual(siteA.PresenceHex, resolved.DerivedHex);
        }

        [Test]
        public void ACCEPT_05_CrossHexBoundaryImmediatelyChangesDerivedCurrentHex()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var midHex);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            const int neDir = 1;
            var neighbor = HexMath.Neighbor(midHex, neDir);
            var hexSize = world.HexWorld.HexSize;

            var nearEdge = PositionNearEdgeTowardNeighbor(world, midHex, neighbor);
            var derivedNear = HexMath.WorldToHex(nearEdge.X, nearEdge.Y, hexSize);
            Assert.AreEqual(midHex, derivedNear);
            world.PlayerPartyTravel.SetAtWorldPosition(nearEdge, derivedNear);

            var across = PositionAcrossEdgeInNeighbor(world, midHex, neighbor);
            var derivedAcross = HexMath.WorldToHex(across.X, across.Y, hexSize);
            Assert.AreEqual(neighbor, derivedAcross);
            world.PlayerPartyTravel.SetWorldPositionInternal(across, derivedAcross);
            Assert.AreEqual(neighbor, world.PlayerPartyTravel.CurrentHex);

            // Stale CurrentHex must heal immediately when authority re-resolves geometry.
            world.PlayerPartyTravel.SetAtWorldPosition(across, midHex);
            Assert.AreEqual(midHex, world.PlayerPartyTravel.CurrentHex);
            Assert.IsTrue(PlayerPartyWorldLocationQuery.TryResolve(world, party, out var resolved));
            Assert.AreEqual(neighbor, world.PlayerPartyTravel.CurrentHex);
            Assert.AreEqual(neighbor, resolved.DerivedHex);
        }

        [Test]
        public void ACCEPT_06_CloseBeforeBoundaryLoadsOldHexLocalMap()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var midHex);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            const int neDir = 1;
            var neighbor = HexMath.Neighbor(midHex, neDir);
            SetHexTerrain(world, midHex, HexTerrainType.Plain);
            SetHexTerrain(world, neighbor, HexTerrainType.Forest);

            var nearEdge = PositionNearEdgeTowardNeighbor(world, midHex, neighbor);
            var hexSize = world.HexWorld.HexSize;
            var derived = HexMath.WorldToHex(nearEdge.X, nearEdge.Y, hexSize);
            Assert.AreEqual(midHex, derived);
            world.PlayerPartyTravel.SetAtWorldPosition(nearEdge, derived);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);

            Assert.IsTrue(PlayerPartyHexTravelService.CloseWorldMapTakeover(world, party).IsSuccess);
            Assert.AreEqual(WildernessLocalMapFallback.PlainsWildernessLocalMapId, world.PartyWorld.LocalMapId);
            Assert.AreNotEqual(WildernessLocalMapFallback.ForbiddenHuangyuanSiteLocalMapId, world.PartyWorld.LocalMapId);
        }

        [Test]
        public void ACCEPT_07_CloseAfterBoundaryLoadsNewHexLocalMap()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var midHex);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            const int neDir = 1;
            var neighbor = HexMath.Neighbor(midHex, neDir);
            SetHexTerrain(world, midHex, HexTerrainType.Plain);
            SetHexTerrain(world, neighbor, HexTerrainType.Forest);

            var across = PositionAcrossEdgeInNeighbor(world, midHex, neighbor);
            var hexSize = world.HexWorld.HexSize;
            var derived = HexMath.WorldToHex(across.X, across.Y, hexSize);
            Assert.AreEqual(neighbor, derived);
            world.PlayerPartyTravel.SetAtWorldPosition(across, derived);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);

            Assert.IsTrue(PlayerPartyHexTravelService.CloseWorldMapTakeover(world, party).IsSuccess);
            Assert.AreEqual(WildernessLocalMapFallback.ForestWildernessLocalMapId, world.PartyWorld.LocalMapId);
            Assert.AreNotEqual(WildernessLocalMapFallback.ForbiddenHuangyuanSiteLocalMapId, world.PartyWorld.LocalMapId);
        }

        [Test]
        public void ACCEPT_08_WorldMapMarkerAndLocalViewResolverUseSameAuthoritativeLocation()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var midHex);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            SetPartyAtSiteOpen(world, siteA, party);

            Assert.IsTrue(PlayerPartyWorldLocationQuery.TryResolve(world, party, out var atSite));
            Assert.IsTrue(PlayerPartyHexTravelService.TryResolvePartyWorldPosition(world, party, out var markerPos));
            Assert.IsTrue(PlayerPartyHexTravelService.TryResolvePartyWorldHex(world, party, out var markerHex));
            Assert.AreEqual(atSite.WorldPosition.X, markerPos.X, FloatTol);
            Assert.AreEqual(atSite.WorldPosition.Y, markerPos.Y, FloatTol);
            Assert.AreEqual(atSite.DerivedHex, markerHex);
            Assert.AreEqual(siteA.LocalMapId, atSite.ResolvedLocalMapId);

            SetPartyOffCenter(world, midHex, 0.2f, -0.15f);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);
            Assert.IsTrue(PlayerPartyWorldLocationQuery.TryResolve(world, party, out var wilderness));
            Assert.IsTrue(PlayerPartyHexTravelService.TryResolvePartyWorldPosition(world, party, out markerPos));
            Assert.IsTrue(PlayerPartyHexTravelService.TryResolvePartyWorldHex(world, party, out markerHex));
            Assert.AreEqual(wilderness.WorldPosition.X, markerPos.X, FloatTol);
            Assert.AreEqual(wilderness.WorldPosition.Y, markerPos.Y, FloatTol);
            Assert.AreEqual(wilderness.DerivedHex, markerHex);
            Assert.IsTrue(WildernessLocalMapFallback.TryResolve(world, wilderness.DerivedHex, out var enterMapId));
            Assert.AreEqual(enterMapId, wilderness.ResolvedLocalMapId);
        }

        [Test]
        public void ACCEPT_09_AutoTravelUsesDistanceBasedConstantSpeed()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var midHex);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            world.PlayerPartyTravel.SetIdleAt(siteA.PresenceHex);
            SetPartyOffCenter(world, siteA.PresenceHex, 0.35f, -0.2f);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);

            var dest = new HexCoord(midHex.Q + 2, midHex.R);
            Assert.IsTrue(world.HexWorld.TryGetTile(dest, out var destTile) && destTile != null && destTile.IsPassable);
            Assert.IsTrue(PlayerPartyHexTravelService.BeginTravel(world, party, dest).IsSuccess);

            var hexSize = world.HexWorld.HexSize;
            var unitsPerTick = PlayerPartyHexTravelService.WorldUnitsPerTick(hexSize);
            var prev = world.PlayerPartyTravel.WorldPosition;
            var tickSpeeds = new System.Collections.Generic.List<float>(8);
            for (var i = 0; i < 8 && world.PlayerPartyTravel.IsMoving; i++)
            {
                PlayerPartyHexTravelService.AdvanceAll(world, 1);
                var next = world.PlayerPartyTravel.WorldPosition;
                tickSpeeds.Add(WorldVec2.Distance(prev, next));
                prev = next;
            }

            Assert.GreaterOrEqual(tickSpeeds.Count, 4, "Need several travel ticks to measure constant speed.");
            for (var i = 0; i < tickSpeeds.Count; i++)
            {
                Assert.AreEqual(unitsPerTick, tickSpeeds[i], 0.08f,
                    "Tick " + i + " should advance at constant WorldUnitsPerTick.");
            }

            var segmentIndexAfterShort = world.PlayerPartyTravel.SegmentIndex;
            var totalDist = 0f;
            var totalTicks = 0;
            var prevPos = world.PlayerPartyTravel.WorldPosition;
            while (world.PlayerPartyTravel.IsMoving && totalTicks < 24)
            {
                PlayerPartyHexTravelService.AdvanceAll(world, 1);
                totalDist += WorldVec2.Distance(prevPos, world.PlayerPartyTravel.WorldPosition);
                prevPos = world.PlayerPartyTravel.WorldPosition;
                totalTicks++;
            }

            if (totalTicks > 0)
            {
                var avgSpeed = totalDist / totalTicks;
                Assert.AreEqual(unitsPerTick, avgSpeed, 0.12f);
            }

            Assert.Greater(segmentIndexAfterShort, 0, "Off-center start should produce a shorter first segment.");
        }

        [Test]
        public void ACCEPT_10_SegmentBoundaryDoesNotConsumeExtraPauseTick()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var midHex);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            world.PlayerPartyTravel.SetIdleAt(siteA.PresenceHex);
            SetPartyOffCenter(world, siteA.PresenceHex, 0.4f, 0.05f);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);

            var waypoint = new HexCoord(midHex.Q + 1, midHex.R);
            Assert.IsTrue(PlayerPartyHexTravelService.BeginTravel(world, party, waypoint).IsSuccess);
            Assert.AreEqual(0, world.PlayerPartyTravel.SegmentIndex);

            var hexSize = world.HexWorld.HexSize;
            Assert.IsTrue(world.PlayerPartyTravel.TryGetActiveSegmentWorld(
                hexSize, out _, out var segmentEnd));
            var remainingOnSegment = WorldVec2.Distance(world.PlayerPartyTravel.WorldPosition, segmentEnd);
            Assert.Greater(remainingOnSegment, 0.05f);

            PlayerPartyHexTravelService.AdvanceDistanceBudget(world, remainingOnSegment + 0.02f);

            Assert.Greater(world.PlayerPartyTravel.SegmentIndex, 0,
                "SegmentIndex should advance within the same AdvanceDistanceBudget call.");
            Assert.IsTrue(world.PlayerPartyTravel.IsMoving);
        }

        [Test]
        public void ACCEPT_11_RoadTerrainUsesDedicatedWildernessFallback()
        {
            var world = BuildTinyTravelWorld(out _, out _, out var midHex);
            SetHexTerrain(world, midHex, HexTerrainType.Road);

            Assert.IsTrue(WildernessLocalMapFallback.TryResolve(HexTerrainType.Road, out var byTerrain));
            Assert.AreEqual(WildernessLocalMapFallback.RoadWildernessLocalMapId, byTerrain);
            Assert.IsTrue(WildernessLocalMapFallback.TryResolve(world, midHex, out var byHex));
            Assert.AreEqual(WildernessLocalMapFallback.RoadWildernessLocalMapId, byHex);
            Assert.AreNotEqual(WildernessLocalMapFallback.ForbiddenHuangyuanSiteLocalMapId, byHex);
        }

        [Test]
        public void ACCEPT_12_OrdinaryHexFallbackDoesNotResolveToHuangyuanSiteLocalMap()
        {
            Assert.IsTrue(WildernessLocalMapFallback.TryResolve(HexTerrainType.Plain, out var plain));
            Assert.IsTrue(WildernessLocalMapFallback.TryResolve(HexTerrainType.Road, out var road));
            Assert.IsTrue(WildernessLocalMapFallback.TryResolve(HexTerrainType.Forest, out var forest));
            Assert.IsTrue(WildernessLocalMapFallback.TryResolve(HexTerrainType.Mountain, out var mountain));

            var forbidden = WildernessLocalMapFallback.ForbiddenHuangyuanSiteLocalMapId;
            Assert.AreNotEqual(forbidden, plain);
            Assert.AreNotEqual(forbidden, road);
            Assert.AreNotEqual(forbidden, forest);
            Assert.AreNotEqual(forbidden, mountain);
            Assert.AreNotEqual(forbidden, WildernessLocalMapFallback.PlainsWildernessLocalMapId);
            Assert.AreNotEqual(forbidden, WildernessLocalMapFallback.RoadWildernessLocalMapId);
            Assert.AreNotEqual(forbidden, WildernessLocalMapFallback.ForestWildernessLocalMapId);
            Assert.AreNotEqual(forbidden, WildernessLocalMapFallback.MountainWildernessLocalMapId);
        }

        [Test]
        public void ACCEPT_13_StartupOpeningSetsAtWorldSite()
        {
            var world = new SimulationWorld();
            Ch01HexPrototypeMapBuilder.Build(world);

            var result = HexStrategicSessionBootstrap.ApplyOpening(world, null, null, null);
            Assert.IsTrue(result.IsSuccess, result.IsFailure ? result.Error.ToString() : string.Empty);

            Assert.AreEqual(PlayerPartyLocationKind.AtWorldSite, world.PlayerPartyTravel.LocationKind);
            Assert.AreNotEqual(PlayerPartyLocationKind.AtWorldPosition, world.PlayerPartyTravel.LocationKind);
            Assert.AreEqual(HexStrategicSessionBootstrap.DefaultStartSiteId, world.PlayerPartyTravel.SiteId);
            Assert.IsTrue(world.Strategic.Sites.TryGet(HexStrategicSessionBootstrap.DefaultStartSiteId, out var site));
            Assert.AreEqual(site.PresenceHex, world.PlayerPartyTravel.CurrentHex);
        }

        [Test]
        public void ACCEPT_14_LegacyPartyWorldSiteIdCannotOverrideAtWorldPosition()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);

            // 模拟：正式已在开世界 mid，但 PartyWorld 仍残留出发 Site（旧 presentation cache）。
            world.PlayerPartyTravel.SnapToHexCenter(mid, world.HexWorld.HexSize);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);
            PlayerPartyHexTravelService.ApplyMembersAtHex(world, party, mid);
            world.PartyWorld.SiteId = siteA.SiteId;
            world.PartyWorld.LocalMapId = siteA.LocalMapId;
            world.PartyWorld.Mode = PartyWorldPresenceMode.AtSite;

            Assert.IsTrue(PlayerPartyWorldLocationQuery.TryResolve(world, party, out var resolved));
            Assert.AreEqual(PlayerPartyLocationKind.AtWorldPosition, world.PlayerPartyTravel.LocationKind);
            Assert.AreEqual(PlayerPartyLocationKind.AtWorldPosition, resolved.LocationKind);
            Assert.AreEqual(mid, resolved.DerivedHex);
            Assert.IsTrue(string.IsNullOrEmpty(world.PlayerPartyTravel.SiteId));

            // healDrift 亦不得用 PartyWorld 覆盖 Domain。
            Assert.IsTrue(PlayerPartyWorldLocationQuery.TryResolve(world, party, out _, healDrift: true));
            Assert.AreEqual(PlayerPartyLocationKind.AtWorldPosition, world.PlayerPartyTravel.LocationKind);
            Assert.AreEqual(mid, world.PlayerPartyTravel.CurrentHex);
        }

        [Test]
        public void ARRIVAL_01_StartAtWorldSite_TravelToOrdinaryHex_CompletionRemainsAtWorldPosition()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            var hexSize = world.HexWorld.HexSize;
            world.PlayerPartyTravel.SetAtWorldSite(siteA.SiteId, siteA.PresenceHex, hexSize);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);

            Assert.IsTrue(PlayerPartyHexTravelService.BeginTravel(world, party, mid).IsSuccess);
            Assert.IsTrue(string.IsNullOrEmpty(world.PartyWorld.SiteId), "BeginTravel must clear PartyWorld.SiteId");
            Assert.IsTrue(string.IsNullOrEmpty(world.PartyWorld.LocalMapId), "BeginTravel must clear PartyWorld.LocalMapId");

            ForceAdvanceToDestination(world);
            Assert.IsFalse(world.PlayerPartyTravel.IsMoving);
            Assert.AreEqual(PlayerPartyLocationKind.AtWorldPosition, world.PlayerPartyTravel.LocationKind);
            Assert.IsTrue(string.IsNullOrEmpty(world.PlayerPartyTravel.SiteId));
            Assert.AreEqual(mid, world.PlayerPartyTravel.CurrentHex);
            Assert.IsTrue(IsNearHexCenter(world, world.PlayerPartyTravel.WorldPosition, mid));
        }

        [Test]
        public void ARRIVAL_02_TravelToOrdinaryHex_CompletionDoesNotRestoreDepartureSiteId()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            world.PlayerPartyTravel.SetAtWorldSite(siteA.SiteId, siteA.PresenceHex, world.HexWorld.HexSize);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);

            Assert.IsTrue(PlayerPartyHexTravelService.BeginTravel(world, party, mid).IsSuccess);
            // 故意污染：模拟 WatchArrivals/SyncPartyFocus 试图写回出发村
            world.PartyWorld.SiteId = siteA.SiteId;
            world.PartyWorld.LocalMapId = siteA.LocalMapId;

            ForceAdvanceToDestination(world);
            WorldTravelService.SyncPartyFocus(world);

            Assert.AreEqual(PlayerPartyLocationKind.AtWorldPosition, world.PlayerPartyTravel.LocationKind);
            Assert.AreNotEqual(siteA.SiteId, world.PlayerPartyTravel.SiteId);
            Assert.IsTrue(string.IsNullOrEmpty(world.PlayerPartyTravel.SiteId));
            Assert.AreEqual(mid, world.PlayerPartyTravel.CurrentHex);
        }

        [Test]
        public void ARRIVAL_03_TravelToOrdinaryHex_CompletionWorldPositionEqualsTargetCanonicalCenter()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            world.PlayerPartyTravel.SetAtWorldSite(siteA.SiteId, siteA.PresenceHex, world.HexWorld.HexSize);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);

            Assert.IsTrue(WorldMapPartyTravelCommand.TryResolve(world, mid, out var cmd));
            Assert.IsTrue(PlayerPartyHexTravelService.BeginTravel(
                world, party, cmd.DestinationHex, cmd.TargetSiteId).IsSuccess);
            ForceAdvanceToDestination(world);

            Assert.AreEqual(cmd.CanonicalDestinationWorld.X, world.PlayerPartyTravel.WorldPosition.X, FloatTol);
            Assert.AreEqual(cmd.CanonicalDestinationWorld.Y, world.PlayerPartyTravel.WorldPosition.Y, FloatTol);
        }

        [Test]
        public void ARRIVAL_04_TravelCompletion_DoesNotMaterializePreviousWorldSite()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            world.PlayerPartyTravel.SetAtWorldSite(siteA.SiteId, siteA.PresenceHex, world.HexWorld.HexSize);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);

            Assert.IsTrue(PlayerPartyHexTravelService.BeginTravel(world, party, mid).IsSuccess);
            ForceAdvanceToDestination(world);

            // WorldMap 仍打开时：不加载出发村 LocalMap
            Assert.AreNotEqual(siteA.LocalMapId, world.PartyWorld.LocalMapId);
            Assert.IsTrue(string.IsNullOrEmpty(world.PartyWorld.SiteId));
            Assert.AreEqual(PlayerPartyLocationKind.AtWorldPosition, world.PlayerPartyTravel.LocationKind);
        }

        [Test]
        public void ARRIVAL_05_CloseWorldMapAfterOrdinaryHexArrival_LoadsDestinationWilderness()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            world.PlayerPartyTravel.SetAtWorldSite(siteA.SiteId, siteA.PresenceHex, world.HexWorld.HexSize);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);

            Assert.IsTrue(PlayerPartyHexTravelService.BeginTravel(world, party, mid).IsSuccess);
            ForceAdvanceToDestination(world);
            Assert.IsTrue(PlayerPartyHexTravelService.CloseWorldMapTakeover(world, party).IsSuccess);

            Assert.AreEqual(PlayerPartyLocationKind.AtWorldPosition, world.PlayerPartyTravel.LocationKind);
            Assert.AreEqual(mid, world.PlayerPartyTravel.CurrentHex);
            Assert.AreNotEqual(siteA.LocalMapId, world.PartyWorld.LocalMapId);
            Assert.IsTrue(WildernessLocalMapFallback.TryResolve(world, mid, out var expectedMap));
            Assert.AreEqual(expectedMap, world.PartyWorld.LocalMapId);
            Assert.IsTrue(world.WorldPresence.TryGet(a, out var wp));
            Assert.IsTrue(wp.UsesHexPresence);
            Assert.AreEqual(mid, wp.ResidualHex);
        }

        [Test]
        public void ARRIVAL_06_TravelToWorldSite_OnlyDestinationSiteBecomesAtWorldSite()
        {
            var world = BuildTinyTravelWorld(out var siteA, out var siteB, out _);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            world.PlayerPartyTravel.SetAtWorldSite(siteA.SiteId, siteA.PresenceHex, world.HexWorld.HexSize);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);

            Assert.IsTrue(WorldMapPartyTravelCommand.TryResolve(world, siteB.PresenceHex, out var cmd));
            Assert.AreEqual(siteB.SiteId, cmd.TargetSiteId);
            Assert.IsTrue(PlayerPartyHexTravelService.BeginTravel(
                world, party, cmd.DestinationHex, cmd.TargetSiteId).IsSuccess);
            ForceAdvanceToDestination(world);

            Assert.AreEqual(PlayerPartyLocationKind.AtWorldSite, world.PlayerPartyTravel.LocationKind);
            Assert.AreEqual(siteB.SiteId, world.PlayerPartyTravel.SiteId);
            Assert.AreNotEqual(siteA.SiteId, world.PlayerPartyTravel.SiteId);
            Assert.AreEqual(siteB.PresenceHex, world.PlayerPartyTravel.CurrentHex);
        }

        [Test]
        public void ARRIVAL_07_MarkerQueryAndIdlePositionStayAtDestinationAfterComplete()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            world.PlayerPartyTravel.SetAtWorldSite(siteA.SiteId, siteA.PresenceHex, world.HexWorld.HexSize);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);

            Assert.IsTrue(PlayerPartyHexTravelService.BeginTravel(world, party, mid).IsSuccess);
            ForceAdvanceToDestination(world);

            // 模拟 WorldMap 仍打开、多帧查询 Marker（含 SyncPartyFocus 污染尝试）
            world.PartyWorld.SiteId = siteA.SiteId;
            world.PartyWorld.LocalMapId = siteA.LocalMapId;
            for (var i = 0; i < 5; i++)
            {
                WorldTravelService.SyncPartyFocus(world);
                Assert.IsTrue(PlayerPartyWorldLocationQuery.TryResolve(world, party, out var r));
                Assert.AreEqual(PlayerPartyLocationKind.AtWorldPosition, r.LocationKind);
                Assert.AreEqual(mid, r.DerivedHex);
                Assert.IsTrue(IsNearHexCenter(world, r.WorldPosition, mid));
            }
        }

        [Test]
        public void ARRIVAL_08_CloseWorldMapMaterializesActiveExactlyOnce()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            var b = Spawn(world, "WangChen");
            var party = BuildParty(world, siteA, a, b);
            world.PlayerPartyTravel.SetAtWorldSite(siteA.SiteId, siteA.PresenceHex, world.HexWorld.HexSize);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);

            Assert.IsTrue(PlayerPartyHexTravelService.BeginTravel(world, party, mid).IsSuccess);
            ForceAdvanceToDestination(world);
            Assert.IsTrue(PlayerPartyHexTravelService.CloseWorldMapTakeover(world, party).IsSuccess);

            var bounds = new WildernessLocalWorldProjection.WildernessLocalMapBounds(0f, 20f, 0f, 20f);
            PlayerPartyLocalMapMaterializationService.MaterializePartyOnResolvedLocalMap(
                world, party.Members, bounds);

            Assert.IsTrue(
                PlayerPartyLocalMapMaterializationService.TryAssertActiveMaterializedOnce(
                    world, a, bounds, out var err),
                err);
            Assert.AreEqual(a, party.ActiveCharacterId);
            Assert.IsTrue(world.LocalMap.ContainsOccupant(a));
            Assert.IsTrue(world.LocalMap.ContainsOccupant(b));
        }

        [Test]
        public void ARRIVAL_09_MaterializedActiveIsInsidePlayableBounds()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            world.PlayerPartyTravel.SetAtWorldSite(siteA.SiteId, siteA.PresenceHex, world.HexWorld.HexSize);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);

            Assert.IsTrue(PlayerPartyHexTravelService.BeginTravel(world, party, mid).IsSuccess);
            ForceAdvanceToDestination(world);
            Assert.IsTrue(PlayerPartyHexTravelService.EnterLocalViewAtCurrentHex(world, party).IsSuccess);

            var bounds = new WildernessLocalWorldProjection.WildernessLocalMapBounds(-5f, 15f, -5f, 15f);
            PlayerPartyLocalMapMaterializationService.MaterializePartyOnResolvedLocalMap(
                world, party.Members, bounds);

            Assert.IsTrue(world.Entities.TryGet(a, out var ent));
            Assert.IsTrue(ent.TryGet<XianXia.Core.Exploration.EntityLocationComponent>(out var loc));
            Assert.IsTrue(loc.HasPresentationOverride);
            Assert.GreaterOrEqual(loc.PresentationOverrideX, bounds.MinX - 0.01f);
            Assert.LessOrEqual(loc.PresentationOverrideX, bounds.MaxX + 0.01f);
            Assert.GreaterOrEqual(loc.PresentationOverrideZ, bounds.MinY - 0.01f);
            Assert.LessOrEqual(loc.PresentationOverrideZ, bounds.MaxY + 0.01f);
        }

        [Test]
        public void ARRIVAL_10_MaterializedActiveBelongsToResolvedLocalMap()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            world.PlayerPartyTravel.SetAtWorldSite(siteA.SiteId, siteA.PresenceHex, world.HexWorld.HexSize);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);

            Assert.IsTrue(PlayerPartyHexTravelService.BeginTravel(world, party, mid).IsSuccess);
            ForceAdvanceToDestination(world);
            Assert.IsTrue(PlayerPartyHexTravelService.CloseWorldMapTakeover(world, party).IsSuccess);

            Assert.IsTrue(WildernessLocalMapFallback.TryResolve(world, mid, out var expectedMap));
            Assert.AreEqual(expectedMap, world.PartyWorld.LocalMapId);
            Assert.AreEqual(expectedMap, world.LocalMap.ActiveMapLayoutId);

            PlayerPartyLocalMapMaterializationService.MaterializePartyOnResolvedLocalMap(
                world, party.Members);
            Assert.IsTrue(world.LocalMap.ContainsOccupant(a));
            Assert.IsTrue(world.WorldPresence.TryGet(a, out var wp));
            Assert.IsTrue(
                PlayerPartyLocalMapMaterializationService.IsWildernessPartyMemberVisibleOnActiveLocalMap(
                    world, a, wp));
            Assert.AreNotEqual(siteA.LocalMapId, world.LocalMap.ActiveMapLayoutId);
        }

        [Test]
        public void ARRIVAL_11_LeaveSite_PartyWorldCacheCannotReassertAtWorldSite()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            world.PlayerPartyTravel.SetAtWorldSite(siteA.SiteId, siteA.PresenceHex, world.HexWorld.HexSize);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);

            Assert.IsTrue(PlayerPartyHexTravelService.BeginTravel(world, party, mid).IsSuccess);
            Assert.AreEqual(PlayerPartyLocationKind.AtWorldPosition, world.PlayerPartyTravel.LocationKind);
            Assert.IsTrue(string.IsNullOrEmpty(world.PartyWorld.SiteId));
            Assert.IsTrue(string.IsNullOrEmpty(world.PartyWorld.LocalMapId));

            // 模拟旧 presentation cache 试图复活出发村
            world.PartyWorld.SiteId = siteA.SiteId;
            world.PartyWorld.LocalMapId = siteA.LocalMapId;
            WorldTravelService.SyncPartyFocus(world);
            Assert.IsTrue(PlayerPartyWorldLocationQuery.TryResolve(world, party, out var midTravel));
            Assert.AreEqual(PlayerPartyLocationKind.AtWorldPosition, midTravel.LocationKind);

            ForceAdvanceToDestination(world);
            world.PartyWorld.SiteId = siteA.SiteId;
            WorldTravelService.SyncPartyFocus(world);
            Assert.IsTrue(PlayerPartyWorldLocationQuery.TryResolve(world, party, out var after));
            Assert.AreEqual(PlayerPartyLocationKind.AtWorldPosition, after.LocationKind);
            Assert.AreEqual(mid, after.DerivedHex);
            Assert.IsTrue(string.IsNullOrEmpty(world.PlayerPartyTravel.SiteId));
        }
    }
}
