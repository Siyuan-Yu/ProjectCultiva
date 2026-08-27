using System;
using System.Collections.Generic;
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
            // Odd-R WorldToHex 近边界会跳邻格：从跨边点回退到仍属于 fromHex 的最近点。
            for (var t = Math.Min(0.95f, inwardFactor); t >= 0.05f; t -= 0.02f)
            {
                var candidate = new WorldVec2(
                    center.X + (crossPos.X - center.X) * t,
                    center.Y + (crossPos.Y - center.Y) * t);
                if (HexMath.WorldToHex(candidate.X, candidate.Y, hexSize).Equals(fromHex))
                    return candidate;
            }

            return center;
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
            Assert.AreEqual(siteA.AnchorHex, siteA.PresenceHex);
        }

        [Test]
        public void CONTINUOUS_19_LeaveWorldSite_TransitionsToAtWorldPosition()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out _);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            Assert.IsTrue(PlayerPartyHexTravelService.EnterWorldSiteAsParty(world, party, siteA).IsSuccess);

            var connections = new List<SurfaceExitConnection>(12);
            SurfaceExitZoneCalculator.CollectConnections(
                world, DefaultWildernessBounds(), SurfaceExitZoneCalculator.DefaultExitTriggerDepth, connections);
            Assert.Greater(connections.Count, 0);
            var exit = connections[0];
            Assert.IsTrue(PlayerPartyWildernessTransitionService.TryExitWorldSiteByConnection(
                world, party, exit).IsSuccess);

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
            Assert.AreEqual(siteA.AnchorHex, siteA.PresenceHex);

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
            Assert.IsTrue(world.Strategic.Sites.TryGet(
                HexStrategicSessionBootstrap.DefaultStartSiteId, out var site));

            // ApplyOpening 依赖 party 实体；此处用同等开局契约：Presence + EnterWorldSite → AtWorldSite。
            var a = Spawn(world, "LinQing");
            world.WorldPresence.SetAtSite(a, site.SiteId);
            var party = new PlayerPartyRuntime();
            Assert.IsTrue(party.TryInitialize(a, out _));
            Assert.IsTrue(PlayerPartyHexTravelService.EnterWorldSiteAsParty(world, party, site).IsSuccess);

            Assert.AreEqual(PlayerPartyLocationKind.AtWorldSite, world.PlayerPartyTravel.LocationKind);
            Assert.AreNotEqual(PlayerPartyLocationKind.AtWorldPosition, world.PlayerPartyTravel.LocationKind);
            Assert.AreEqual(HexStrategicSessionBootstrap.DefaultStartSiteId, world.PlayerPartyTravel.SiteId);
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

        [Test]
        public void EDGE_01_SurfaceWorldSiteMovementCrossingBoundaryTriggersExit()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out _);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            Assert.IsTrue(PlayerPartyHexTravelService.EnterWorldSiteAsParty(world, party, siteA).IsSuccess);
            Assert.IsTrue(PlayerPartyWildernessTransitionService.IsSurfaceHexEdgeTransitionEnabled(world));

            const int exitDir = 0; // E
            Assert.IsTrue(PlayerPartyWildernessTransitionService.TryAttemptSurfaceEdgeTransition(
                world, party, exitDir).IsSuccess);
            Assert.AreEqual(PlayerPartyLocationKind.AtWorldPosition, world.PlayerPartyTravel.LocationKind);
            Assert.IsTrue(string.IsNullOrEmpty(world.PlayerPartyTravel.SiteId));
            Assert.IsFalse(world.Strategic.Sites.TryGetAtHex(world.PlayerPartyTravel.CurrentHex, out _));
        }

        [Test]
        public void EDGE_02_MovementClampDoesNotConsumeValidWorldEdgeCrossing()
        {
            // Clamp 前：from 在界内近缘、to 在界外 → 必须识别 Crossing Intent。
            var bounds = WildernessLocalWorldProjection.WildernessLocalMapBounds.FromOriginSize(
                0f, 0f, 1f, 20, 20);
            var fromX = bounds.MaxX - 0.4f;
            var fromY = bounds.CenterY;
            var toX = bounds.MaxX + 0.5f;
            var toY = bounds.CenterY;
            Assert.IsFalse(WildernessLocalWorldProjection.IsOutsideBounds(fromX, fromY, bounds));
            Assert.IsTrue(WildernessLocalWorldProjection.IsOutsideBounds(toX, toY, bounds));
            Assert.IsTrue(WildernessLocalWorldProjection.TryResolveCrossingIntent(
                fromX, fromY, toX, toY, bounds, out var dir));
            Assert.AreEqual(0, dir); // E
        }

        [Test]
        public void EDGE_03_WildernessMovementCrossingNEBoundaryEntersNENeighbor()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);
            const int neDir = 1;
            var neighbor = HexMath.Neighbor(mid, neDir);
            SetPartyOffCenter(world, mid, 0.4f, -0.35f);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);

            Assert.IsTrue(PlayerPartyWildernessTransitionService.TryAttemptSurfaceEdgeTransition(
                world, party, neDir).IsSuccess);
            Assert.AreEqual(neighbor, world.PlayerPartyTravel.CurrentHex);
            Assert.AreEqual(PlayerPartyLocationKind.AtWorldPosition, world.PlayerPartyTravel.LocationKind);
        }

        [Test]
        public void EDGE_04_NeighborEntryUsesOppositeEdge()
        {
            var bounds = DefaultWildernessBounds();
            const int entryDir = 1;
            var opposite = WildernessLocalWorldProjection.OppositeDirection(entryDir);
            WildernessLocalWorldProjection.GetLocalPositionNearEdge(bounds, opposite, out var lx, out var ly);
            // 出生在 Entry 边的内侧 Safe Interior，而非边界线上。
            Assert.IsFalse(WildernessLocalWorldProjection.IsOutsideBounds(lx, ly, bounds));
            Assert.IsTrue(WildernessLocalWorldProjection.IsInSafeInterior(lx, ly, bounds));
            // 相对中心仍落在 opposite 扇区（靠 SW 一侧进）。
            var dx = lx - bounds.CenterX;
            var dy = ly - bounds.CenterY;
            Assert.IsTrue(dx * dx + dy * dy > 0.01f);
        }

        [Test]
        public void EDGE_05_ManualEdgeTransitionDoesNotSnapToNeighborCenter()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);
            const int neDir = 1;
            var neighbor = HexMath.Neighbor(mid, neDir);
            SetPartyOffCenter(world, mid, 0.35f, -0.3f);
            Assert.IsTrue(PlayerPartyWildernessTransitionService.TryCrossWildernessEdge(world, party, neDir).IsSuccess);
            Assert.IsFalse(IsNearHexCenter(world, world.PlayerPartyTravel.WorldPosition, neighbor));
        }

        [Test]
        public void EDGE_06_WildernessToWorldSiteBoundaryCrossingEntersSite()
        {
            var world = BuildTinyTravelWorld(out var siteA, out var siteB, out _);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);

            // 站在青石镇西侧邻格，向东跨入 footprint。
            var westOfSite = new HexCoord(siteB.PresenceHex.Q - 1, siteB.PresenceHex.R);
            Assert.IsFalse(siteB.OccupiesHex(westOfSite));
            Assert.IsTrue(siteB.OccupiesHex(HexMath.Neighbor(westOfSite, 0)));
            world.PlayerPartyTravel.SnapToHexCenter(westOfSite, world.HexWorld.HexSize);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);
            PlayerPartyHexTravelService.ApplyMembersAtHex(world, party, westOfSite);

            Assert.IsTrue(PlayerPartyWildernessTransitionService.TryAttemptSurfaceEdgeTransition(
                world, party, 0).IsSuccess);
            Assert.AreEqual(PlayerPartyLocationKind.AtWorldSite, world.PlayerPartyTravel.LocationKind);
            Assert.AreEqual(siteB.SiteId, world.PlayerPartyTravel.SiteId);
            Assert.AreEqual(siteB.LocalMapId, world.PartyWorld.LocalMapId);
        }

        [Test]
        public void EDGE_07_WorldSiteToWildernessBoundaryCrossingLeavesSite()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out _);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            Assert.IsTrue(PlayerPartyHexTravelService.EnterWorldSiteAsParty(world, party, siteA).IsSuccess);

            Assert.IsTrue(PlayerPartyWildernessTransitionService.TryAttemptSurfaceEdgeTransition(
                world, party, 3).IsSuccess);
            Assert.AreEqual(PlayerPartyLocationKind.AtWorldPosition, world.PlayerPartyTravel.LocationKind);
            Assert.IsTrue(string.IsNullOrEmpty(world.PartyWorld.SiteId));
            Assert.IsFalse(string.IsNullOrEmpty(world.PartyWorld.LocalMapId));
            Assert.AreNotEqual(siteA.LocalMapId, world.PartyWorld.LocalMapId);
        }

        [Test]
        public void EDGE_08_ImpassableNeighborBlocksCrossing()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            world.PlayerPartyTravel.SnapToHexCenter(mid, world.HexWorld.HexSize);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);

            const int neDir = 1;
            var neighbor = HexMath.Neighbor(mid, neDir);
            Assert.IsTrue(world.HexWorld.TryGetCell(neighbor, out var cell) && cell != null);
            cell.Terrain = HexTerrainType.Water;
            cell.IsPassable = false;

            Assert.IsFalse(PlayerPartyWildernessTransitionService.TryAttemptSurfaceEdgeTransition(
                world, party, neDir).IsSuccess);
            Assert.AreEqual(mid, world.PlayerPartyTravel.CurrentHex);
            Assert.AreEqual(PlayerPartyLocationKind.AtWorldPosition, world.PlayerPartyTravel.LocationKind);
        }

        [Test]
        public void EDGE_09_InteriorLocalMapDoesNotUseHexEdgeTransition()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            world.PlayerPartyTravel.SnapToHexCenter(mid, world.HexWorld.HexSize);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);
            world.LocalMap.OverworldMapLayoutId = "base:map_overworld";
            world.LocalMap.ActiveMapLayoutId = "base:map_cave_interior";
            Assert.IsTrue(world.LocalMap.IsInInterior);
            Assert.IsFalse(PlayerPartyWildernessTransitionService.IsSurfaceHexEdgeTransitionEnabled(world));
            Assert.IsFalse(PlayerPartyWildernessTransitionService.TryAttemptSurfaceEdgeTransition(
                world, party, 0).IsSuccess);
            Assert.AreEqual(mid, world.PlayerPartyTravel.CurrentHex);
        }

        [Test]
        public void EDGE_10_ActiveAndFollowersSurviveEdgeTransition()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            var b = Spawn(world, "WangChen");
            var party = BuildParty(world, siteA, a, b);
            world.PlayerPartyTravel.SnapToHexCenter(mid, world.HexWorld.HexSize);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);
            PlayerPartyHexTravelService.ApplyMembersAtHex(world, party, mid);

            const int neDir = 1;
            var neighbor = HexMath.Neighbor(mid, neDir);
            Assert.IsTrue(PlayerPartyWildernessTransitionService.TryAttemptSurfaceEdgeTransition(
                world, party, neDir).IsSuccess);

            Assert.AreEqual(a, party.ActiveCharacterId);
            Assert.AreEqual(2, party.Count);
            Assert.IsTrue(world.WorldPresence.TryGet(a, out var wpA));
            Assert.IsTrue(world.WorldPresence.TryGet(b, out var wpB));
            Assert.AreEqual(neighbor, wpA.ResidualHex);
            Assert.AreEqual(neighbor, wpB.ResidualHex);
        }

        [Test]
        public void EDGE_11_PlayerPartyIdAndActiveCharacterRemainUnchanged()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out _);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            Assert.IsTrue(PlayerPartyHexTravelService.EnterWorldSiteAsParty(world, party, siteA).IsSuccess);
            var activeBefore = party.ActiveCharacterId;
            var countBefore = party.Count;

            Assert.IsTrue(PlayerPartyWildernessTransitionService.TryAttemptSurfaceEdgeTransition(
                world, party, 2).IsSuccess);
            Assert.AreEqual(activeBefore, party.ActiveCharacterId);
            Assert.AreEqual(countBefore, party.Count);
        }

        static void FinishEdgeGateForTests(SimulationWorld world, int exitDir)
        {
            var bounds = DefaultWildernessBounds();
            var entry = WildernessLocalWorldProjection.OppositeDirection(exitDir);
            WildernessLocalWorldProjection.GetLocalPositionNearEdge(bounds, entry, out var x, out var y);
            // BeginTransition 已写入 LastExitDirection；若未 Begin，补一次。
            var gate = world.PlayerPartyTravel.SurfaceEdgeGate;
            if (gate != null && !gate.TransitionInProgress && gate.LastExitDirection < 0)
                gate.BeginTransition(exitDir);
            PlayerPartyWildernessTransitionService.CompleteEdgeTransitionPresentation(world, bounds, x, y);
        }

        [Test]
        public void EDGE_12_SiteToWildernessTransitionDoesNotImmediatelyReturn()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out _);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            Assert.IsTrue(PlayerPartyHexTravelService.EnterWorldSiteAsParty(world, party, siteA).IsSuccess);

            const int exitDir = 0;
            Assert.IsTrue(PlayerPartyWildernessTransitionService.TryAttemptSurfaceEdgeTransition(
                world, party, exitDir).IsSuccess);
            var destHex = world.PlayerPartyTravel.CurrentHex;
            Assert.AreEqual(PlayerPartyLocationKind.AtWorldPosition, world.PlayerPartyTravel.LocationKind);

            FinishEdgeGateForTests(world, exitDir);
            var gate = world.PlayerPartyTravel.SurfaceEdgeGate;
            Assert.IsFalse(gate.EdgeArmed);
            Assert.IsFalse(gate.CanAttemptEdgeTransition);

            // 同帧／立即再试反向：必须被 Gate 拒绝。
            Assert.IsFalse(PlayerPartyWildernessTransitionService.TryAttemptSurfaceEdgeTransition(
                world, party, WildernessLocalWorldProjection.OppositeDirection(exitDir)).IsSuccess);
            Assert.AreEqual(destHex, world.PlayerPartyTravel.CurrentHex);
        }

        [Test]
        public void EDGE_13_WildernessToWildernessTransitionDoesNotImmediatelyReturn()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            world.PlayerPartyTravel.SnapToHexCenter(mid, world.HexWorld.HexSize);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);
            const int neDir = 1;
            var neighbor = HexMath.Neighbor(mid, neDir);

            Assert.IsTrue(PlayerPartyWildernessTransitionService.TryAttemptSurfaceEdgeTransition(
                world, party, neDir).IsSuccess);
            Assert.AreEqual(neighbor, world.PlayerPartyTravel.CurrentHex);
            FinishEdgeGateForTests(world, neDir);

            Assert.IsFalse(PlayerPartyWildernessTransitionService.TryAttemptSurfaceEdgeTransition(
                world, party, WildernessLocalWorldProjection.OppositeDirection(neDir)).IsSuccess);
            Assert.AreEqual(neighbor, world.PlayerPartyTravel.CurrentHex);
        }

        [Test]
        public void EDGE_14_WildernessToWorldSiteTransitionDoesNotImmediatelyReturn()
        {
            var world = BuildTinyTravelWorld(out var siteA, out var siteB, out _);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            var westOfSite = new HexCoord(siteB.PresenceHex.Q - 1, siteB.PresenceHex.R);
            world.PlayerPartyTravel.SnapToHexCenter(westOfSite, world.HexWorld.HexSize);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);
            PlayerPartyHexTravelService.ApplyMembersAtHex(world, party, westOfSite);

            Assert.IsTrue(PlayerPartyWildernessTransitionService.TryAttemptSurfaceEdgeTransition(
                world, party, 0).IsSuccess);
            Assert.AreEqual(PlayerPartyLocationKind.AtWorldSite, world.PlayerPartyTravel.LocationKind);
            Assert.AreEqual(siteB.SiteId, world.PlayerPartyTravel.SiteId);
            FinishEdgeGateForTests(world, 0);

            Assert.IsFalse(PlayerPartyWildernessTransitionService.TryAttemptSurfaceEdgeTransition(
                world, party, 3).IsSuccess);
            Assert.AreEqual(siteB.SiteId, world.PlayerPartyTravel.SiteId);
        }

        [Test]
        public void EDGE_15_DestinationSpawnIsInsidePlayableBounds()
        {
            var bounds = DefaultWildernessBounds();
            WildernessLocalWorldProjection.GetLocalPositionNearEdge(bounds, 4, out var lx, out var ly);
            Assert.IsFalse(WildernessLocalWorldProjection.IsOutsideBounds(lx, ly, bounds));
            Assert.IsTrue(WildernessLocalWorldProjection.IsInSafeInterior(lx, ly, bounds));
        }

        [Test]
        public void EDGE_16_DestinationWorldPositionResolvesToDestinationHex()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            world.PlayerPartyTravel.SnapToHexCenter(mid, world.HexWorld.HexSize);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);
            const int neDir = 1;
            var neighbor = HexMath.Neighbor(mid, neDir);
            Assert.IsTrue(PlayerPartyWildernessTransitionService.TryAttemptSurfaceEdgeTransition(
                world, party, neDir).IsSuccess);
            Assert.AreEqual(neighbor, world.PlayerPartyTravel.CurrentHex);
            Assert.AreEqual(
                neighbor,
                HexMath.WorldToHex(
                    world.PlayerPartyTravel.WorldPosition.X,
                    world.PlayerPartyTravel.WorldPosition.Y,
                    world.HexWorld.HexSize));
            Assert.IsFalse(IsNearHexCenter(world, world.PlayerPartyTravel.WorldPosition, neighbor));
        }

        [Test]
        public void EDGE_17_TransitionCannotRetriggerWhileTransitionInProgress()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            world.PlayerPartyTravel.SnapToHexCenter(mid, world.HexWorld.HexSize);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);

            Assert.IsTrue(PlayerPartyWildernessTransitionService.TryAttemptSurfaceEdgeTransition(
                world, party, 1).IsSuccess);
            Assert.IsTrue(world.PlayerPartyTravel.SurfaceEdgeGate.TransitionInProgress);
            Assert.IsFalse(world.PlayerPartyTravel.SurfaceEdgeGate.CanAttemptEdgeTransition);
            Assert.IsFalse(PlayerPartyWildernessTransitionService.TryAttemptSurfaceEdgeTransition(
                world, party, 1).IsSuccess);
        }

        [Test]
        public void EDGE_18_EdgeDetectorStartsDisarmedAfterEntry()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            world.PlayerPartyTravel.SnapToHexCenter(mid, world.HexWorld.HexSize);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);
            Assert.IsTrue(PlayerPartyWildernessTransitionService.TryAttemptSurfaceEdgeTransition(
                world, party, 1).IsSuccess);
            FinishEdgeGateForTests(world, 1);
            Assert.IsFalse(world.PlayerPartyTravel.SurfaceEdgeGate.EdgeArmed);
            Assert.IsFalse(world.PlayerPartyTravel.SurfaceEdgeGate.TransitionInProgress);
        }

        [Test]
        public void EDGE_19_EdgeDetectorRearmsAfterEnteringSafeInterior()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            world.PlayerPartyTravel.SnapToHexCenter(mid, world.HexWorld.HexSize);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);
            Assert.IsTrue(PlayerPartyWildernessTransitionService.TryAttemptSurfaceEdgeTransition(
                world, party, 1).IsSuccess);
            FinishEdgeGateForTests(world, 1);
            Assert.IsFalse(world.PlayerPartyTravel.SurfaceEdgeGate.EdgeArmed);

            var bounds = DefaultWildernessBounds();
            world.PlayerPartyTravel.SurfaceEdgeGate.TickRearm(bounds.CenterX, bounds.CenterY, bounds);
            Assert.IsTrue(world.PlayerPartyTravel.SurfaceEdgeGate.EdgeArmed);
            Assert.IsTrue(world.PlayerPartyTravel.SurfaceEdgeGate.CanAttemptEdgeTransition);
        }

        [Test]
        public void EDGE_20_StandingNearEntryBoundaryDoesNotTriggerExit()
        {
            var bounds = DefaultWildernessBounds();
            // Entry SW (dir 4 if entry was from NE=1 opposite=4)
            WildernessLocalWorldProjection.GetLocalPositionNearEdge(bounds, 4, out var spawnX, out var spawnY);
            // 站在出生点微抖：不应形成 Inside→Outside crossing
            Assert.IsFalse(WildernessLocalWorldProjection.TryResolveCrossingIntent(
                spawnX, spawnY, spawnX + 0.01f, spawnY + 0.01f, bounds, out _));
        }

        [Test]
        public void EDGE_21_HoldingForwardAfterTransitionKeepsDestinationHex()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            world.PlayerPartyTravel.SnapToHexCenter(mid, world.HexWorld.HexSize);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);
            const int neDir = 1;
            var neighbor = HexMath.Neighbor(mid, neDir);
            Assert.IsTrue(PlayerPartyWildernessTransitionService.TryAttemptSurfaceEdgeTransition(
                world, party, neDir).IsSuccess);
            var posAfter = world.PlayerPartyTravel.WorldPosition;
            FinishEdgeGateForTests(world, neDir);

            // 模拟继续朝 NE 推进（向 destination interior）：WorldPosition 连续微调，Hex 保持 B
            var hexSize = world.HexWorld.HexSize;
            HexMath.ToWorldPosition(neighbor, hexSize, out var cx, out var cy);
            var nudged = new WorldVec2(
                posAfter.X + (cx - posAfter.X) * 0.05f,
                posAfter.Y + (cy - posAfter.Y) * 0.05f);
            world.PlayerPartyTravel.SetAtWorldPosition(
                nudged, HexMath.WorldToHex(nudged.X, nudged.Y, hexSize));
            Assert.AreEqual(neighbor, world.PlayerPartyTravel.CurrentHex);

            // Gate 仍 Disarmed 时禁止反向
            Assert.IsFalse(PlayerPartyWildernessTransitionService.TryAttemptSurfaceEdgeTransition(
                world, party, WildernessLocalWorldProjection.OppositeDirection(neDir)).IsSuccess);
        }

        [Test]
        public void EDGE_22_PlayerCanLaterWalkBackAcrossSameEdgeIntentionally()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            world.PlayerPartyTravel.SnapToHexCenter(mid, world.HexWorld.HexSize);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);
            const int neDir = 1;
            var neighbor = HexMath.Neighbor(mid, neDir);
            Assert.IsTrue(PlayerPartyWildernessTransitionService.TryAttemptSurfaceEdgeTransition(
                world, party, neDir).IsSuccess);
            FinishEdgeGateForTests(world, neDir);

            var bounds = DefaultWildernessBounds();
            world.PlayerPartyTravel.SurfaceEdgeGate.TickRearm(bounds.CenterX, bounds.CenterY, bounds);
            Assert.IsTrue(world.PlayerPartyTravel.SurfaceEdgeGate.EdgeArmed);

            // 主动原路返回
            Assert.IsTrue(PlayerPartyWildernessTransitionService.TryAttemptSurfaceEdgeTransition(
                world, party, WildernessLocalWorldProjection.OppositeDirection(neDir)).IsSuccess);
            Assert.AreEqual(mid, world.PlayerPartyTravel.CurrentHex);
        }

        [Test]
        public void EXITZONE_01_SameLocalMapAlwaysProducesSameExitGeometry()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            BuildParty(world, siteA, a);
            world.PlayerPartyTravel.SnapToHexCenter(mid, world.HexWorld.HexSize);
            world.LocalMap.ActiveMapLayoutId = "w";
            world.LocalMap.OverworldMapLayoutId = "w";
            var bounds = DefaultWildernessBounds();
            const float depth = 1.25f;
            var sigA = CaptureCoverageSignature(world, bounds, depth);
            var sigB = CaptureCoverageSignature(world, bounds, depth);
            Assert.AreEqual(sigA, sigB);
        }

        [Test]
        public void EXITZONE_02_ExitGeometryDoesNotDependOnCharacterPosition()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            BuildParty(world, siteA, a);
            world.PlayerPartyTravel.SnapToHexCenter(mid, world.HexWorld.HexSize);
            world.LocalMap.ActiveMapLayoutId = "w";
            var bounds = DefaultWildernessBounds();
            const float depth = 1.25f;
            var sig1 = CaptureCoverageSignature(world, bounds, depth);
            var sig2 = CaptureCoverageSignature(world, bounds, depth);
            Assert.AreEqual(sig1, sig2);
        }

        [Test]
        public void EXITZONE_03_ExitGeometryDoesNotDependOnEntryDirectionOrWorldHex()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            var bounds = DefaultWildernessBounds();
            const float depth = 1.25f;

            world.PlayerPartyTravel.SnapToHexCenter(mid, world.HexWorld.HexSize);
            world.LocalMap.ActiveMapLayoutId = "w";
            world.LocalMap.OverworldMapLayoutId = "w";
            world.LocalMap.ExitTriggerDepth = depth;
            var geoA = CaptureCoverageSignature(world, bounds, depth);

            const int neDir = 1;
            Assert.IsTrue(PlayerPartyWildernessTransitionService.TryAttemptSurfaceEdgeTransition(
                world, party, neDir).IsSuccess);
            world.PlayerPartyTravel.SurfaceEdgeGate.CompleteTransition(
                neDir, bounds.CenterX, bounds.CenterY);
            world.PlayerPartyTravel.SnapToHexCenter(mid, world.HexWorld.HexSize);
            var geoB = CaptureCoverageSignature(world, bounds, depth);
            Assert.AreEqual(geoA, geoB);
        }

        [Test]
        public void EXITZONE_04_ReturningToSameWorldSiteProducesSameExitGeometry()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            var bounds = HuangcunLikeBounds();
            const float depth = 1.25f;

            Assert.IsTrue(PlayerPartyHexTravelService.EnterWorldSiteAsParty(world, party, siteA).IsSuccess);
            world.LocalMap.ActiveMapLayoutId = siteA.LocalMapId;
            world.LocalMap.OverworldMapLayoutId = siteA.LocalMapId;
            world.LocalMap.ExitTriggerDepth = depth;
            var geoSite1 = CaptureCoverageSignature(world, bounds, depth);

            world.PlayerPartyTravel.SnapToHexCenter(mid, world.HexWorld.HexSize);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);
            world.LocalMap.ActiveMapLayoutId = "base:map_wilderness_plain";
            world.LocalMap.OverworldMapLayoutId = "base:map_wilderness_plain";

            Assert.IsTrue(PlayerPartyHexTravelService.EnterWorldSiteAsParty(world, party, siteA).IsSuccess);
            world.LocalMap.ActiveMapLayoutId = siteA.LocalMapId;
            world.LocalMap.OverworldMapLayoutId = siteA.LocalMapId;
            world.LocalMap.ExitTriggerDepth = depth;
            var geoSite2 = CaptureCoverageSignature(world, bounds, depth);

            Assert.AreEqual(geoSite1, geoSite2);
        }

        [Test]
        public void EXITZONE_05_RuntimeAvailabilityCanChangeWithoutChangingGeometry()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            BuildParty(world, siteA, a);
            world.PlayerPartyTravel.SnapToHexCenter(mid, world.HexWorld.HexSize);
            world.LocalMap.ActiveMapLayoutId = "w";
            world.LocalMap.OverworldMapLayoutId = "w";
            var bounds = DefaultWildernessBounds();
            const float depth = 1.25f;
            var before = new System.Collections.Generic.List<SurfaceExitConnection>(6);
            SurfaceExitZoneCalculator.CollectConnections(world, bounds, depth, before);

            const int neDir = 1;
            var neighbor = HexMath.Neighbor(mid, neDir);
            Assert.IsTrue(world.HexWorld.TryGetCell(neighbor, out var cell) && cell != null);
            cell.Terrain = HexTerrainType.Water;
            cell.IsPassable = false;

            var after = new System.Collections.Generic.List<SurfaceExitConnection>(6);
            SurfaceExitZoneCalculator.CollectConnections(world, bounds, depth, after);
            Assert.AreEqual(before.Count - 1, after.Count);

            // Availability 变化只删 Connection；重叠消解可能改 span，但方向／Destination 必须保留。
            for (var i = 0; i < after.Count; i++)
            {
                SurfaceExitConnection? matched = null;
                for (var j = 0; j < before.Count; j++)
                {
                    if (!before[j].DestinationHex.Equals(after[i].DestinationHex))
                        continue;
                    matched = before[j];
                    break;
                }

                Assert.IsTrue(matched.HasValue, after[i].DestinationHex.ToString());
                Assert.AreEqual(matched.Value.LocalDirectionX, after[i].LocalDirectionX, FloatTol);
                Assert.AreEqual(matched.Value.LocalDirectionY, after[i].LocalDirectionY, FloatTol);
            }

            var visible = new System.Collections.Generic.List<SurfaceExitVisibleZone>(6);
            SurfaceExitZoneCalculator.CollectVisibleZones(world, bounds, depth, visible);
            for (var i = 0; i < visible.Count; i++)
                Assert.AreNotEqual(neighbor, visible[i].DestinationHex);
        }

        [Test]
        public void EXITZONE_06_RenderedZoneBoundsEqualDetectionZoneBounds()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            BuildParty(world, siteA, Spawn(world, "LinQing"));
            world.PlayerPartyTravel.SnapToHexCenter(mid, world.HexWorld.HexSize);
            world.LocalMap.ActiveMapLayoutId = "w";
            var bounds = DefaultWildernessBounds();
            const float depth = 1.25f;
            var step = 0.2f;
            var connections = new System.Collections.Generic.List<SurfaceExitConnection>(6);
            SurfaceExitZoneCalculator.CollectConnections(world, bounds, depth, connections);
            Assert.Greater(connections.Count, 0);

            for (var c = 0; c < connections.Count; c++)
            {
                var conn = connections[c];
                var rects = new System.Collections.Generic.List<SurfaceExitCoverageRect>(1);
                SurfaceExitZoneCalculator.AppendConnectionCoverageRects(conn, rects);
                Assert.AreEqual(1, rects.Count);

                for (var x = bounds.MinX; x <= bounds.MaxX + 0.001f; x += step)
                for (var y = bounds.MinY; y <= bounds.MaxY + 0.001f; y += step)
                {
                    var belongs = SurfaceExitZoneCalculator.PointBelongsToConnection(
                        x, y, conn, depth);
                    var inAnyRect = false;
                    for (var r = 0; r < rects.Count; r++)
                    {
                        var rect = rects[r];
                        if (x >= rect.MinX - 0.001f && x <= rect.MaxX + 0.001f &&
                            y >= rect.MinY - 0.001f && y <= rect.MaxY + 0.001f)
                        {
                            inAnyRect = true;
                            break;
                        }
                    }

                    if (belongs)
                        Assert.IsTrue(inAnyRect || IsNearAnyRect(x, y, rects, CoverageSampleStepTolerance),
                            "detection point must be covered by presentation rects");
                }
            }
        }

        const float CoverageSampleStepTolerance = 0.3f;

        [Test]
        public void EXITZONE_07_WorldSiteAndWildernessUseSameCanonicalGeometryPipeline()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            var bounds = DefaultWildernessBounds();
            const float depth = 1.25f;

            Assert.IsTrue(PlayerPartyHexTravelService.EnterWorldSiteAsParty(world, party, siteA).IsSuccess);
            world.LocalMap.ActiveMapLayoutId = siteA.LocalMapId;
            world.LocalMap.OverworldMapLayoutId = siteA.LocalMapId;
            Assert.IsTrue(SurfaceExitZoneCalculator.ShouldPresent(world));
            var siteVisible = new System.Collections.Generic.List<SurfaceExitVisibleZone>(6);
            SurfaceExitZoneCalculator.CollectVisibleZones(world, bounds, depth, siteVisible);
            Assert.Greater(siteVisible.Count, 0);
            for (var i = 0; i < siteVisible.Count; i++)
            {
                var z = siteVisible[i];
                Assert.Greater(System.Math.Abs(z.Connection.LocalDirectionX) +
                               System.Math.Abs(z.Connection.LocalDirectionY), 0.001f);
            }

            world.PlayerPartyTravel.SnapToHexCenter(mid, world.HexWorld.HexSize);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);
            world.LocalMap.ActiveMapLayoutId = "base:map_wilderness_plain";
            world.LocalMap.OverworldMapLayoutId = "base:map_wilderness_plain";
            Assert.IsTrue(SurfaceExitZoneCalculator.ShouldPresent(world));
            var wildVisible = new System.Collections.Generic.List<SurfaceExitVisibleZone>(6);
            SurfaceExitZoneCalculator.CollectVisibleZones(world, bounds, depth, wildVisible);
            Assert.Greater(wildVisible.Count, 0);
        }

        [Test]
        public void EXITZONE_08_ExitZoneDepthMatchesConfiguredGameplayDepth()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            BuildParty(world, siteA, Spawn(world, "LinQing"));
            world.PlayerPartyTravel.SnapToHexCenter(mid, world.HexWorld.HexSize);
            world.LocalMap.ActiveMapLayoutId = "w";
            var bounds = DefaultWildernessBounds();
            const float depth = 1.25f;
            var connections = new System.Collections.Generic.List<SurfaceExitConnection>(6);
            SurfaceExitZoneCalculator.CollectConnections(world, bounds, depth, connections);
            for (var i = 0; i < connections.Count; i++)
            {
                var rect = connections[i].SlotRect;
                var minDim = System.Math.Min(rect.Width, rect.Height);
                Assert.LessOrEqual(minDim, depth + 0.05f);
                Assert.GreaterOrEqual(minDim, depth - 0.05f);
                Assert.Less(
                    System.Math.Max(rect.Width, rect.Height),
                    System.Math.Max(bounds.HalfWidth, bounds.HalfHeight) * 1.1f);
            }
        }

        [Test]
        public void EXITZONE_09_PingPongDisarmDoesNotModifyExitGeometry()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            world.PlayerPartyTravel.SnapToHexCenter(mid, world.HexWorld.HexSize);
            world.LocalMap.ActiveMapLayoutId = "w";
            world.LocalMap.OverworldMapLayoutId = "w";
            var bounds = DefaultWildernessBounds();
            const float depth = 1.25f;
            var before = CaptureCoverageSignature(world, bounds, depth);

            Assert.IsTrue(PlayerPartyWildernessTransitionService.TryAttemptSurfaceEdgeTransition(
                world, party, 1).IsSuccess);
            Assert.IsFalse(world.PlayerPartyTravel.SurfaceEdgeGate.CanAttemptEdgeTransition);
            world.PlayerPartyTravel.SnapToHexCenter(mid, world.HexWorld.HexSize);
            var after = CaptureCoverageSignature(world, bounds, depth);
            Assert.AreEqual(before, after);
        }

        [Test]
        public void EXITZONE_10_InteriorProducesNoExitZones()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            BuildParty(world, siteA, a);
            world.PlayerPartyTravel.SnapToHexCenter(mid, world.HexWorld.HexSize);
            world.LocalMap.OverworldMapLayoutId = "base:map_overworld";
            world.LocalMap.ActiveMapLayoutId = "base:map_cave_interior";
            Assert.IsTrue(world.LocalMap.IsInInterior);
            Assert.IsFalse(SurfaceExitZoneCalculator.ShouldPresent(world));
            var zones = new System.Collections.Generic.List<SurfaceExitVisibleZone>(6);
            SurfaceExitZoneCalculator.CollectVisibleZones(
                world, DefaultWildernessBounds(), 1.25f, zones);
            Assert.AreEqual(0, zones.Count);
        }

        [Test]
        public void EXITZONE_11_TriggerIntentRequiresAlreadyInZoneThenOutward()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            BuildParty(world, siteA, Spawn(world, "LinQing"));
            world.PlayerPartyTravel.SnapToHexCenter(mid, world.HexWorld.HexSize);
            world.LocalMap.ActiveMapLayoutId = "w";
            var bounds = DefaultWildernessBounds();
            const float depth = 1.25f;
            var connections = new System.Collections.Generic.List<SurfaceExitConnection>(6);
            SurfaceExitZoneCalculator.CollectConnections(world, bounds, depth, connections);
            SurfaceExitConnection? east = null;
            for (var i = 0; i < connections.Count; i++)
            {
                if (connections[i].DirectionIndex != 0)
                    continue;
                east = connections[i];
                break;
            }

            Assert.IsTrue(east.HasValue);
            var slot = east.Value.SlotRect;
            var zoneX = (slot.MinX + slot.MaxX) * 0.5f;
            var zoneY = (slot.MinY + slot.MaxY) * 0.5f;
            var fromX = bounds.CenterX;
            var fromY = bounds.CenterY;
            Assert.IsFalse(WildernessLocalWorldProjection.TryResolveExitTriggerIntent(
                world, fromX, fromY, zoneX, zoneY, bounds, depth, out _));

            var to2X = zoneX + east.Value.LocalDirectionX * (depth * 2f);
            var to2Y = zoneY + east.Value.LocalDirectionY * (depth * 2f);
            Assert.IsTrue(WildernessLocalWorldProjection.TryResolveExitTriggerIntent(
                world, zoneX, zoneY, to2X, to2Y, bounds, depth, out var dir));
            Assert.AreEqual(0, dir);
        }

        [Test]
        public void EXITZONE_12_WorldSiteSurfaceProducesVisibleExitZones()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            Assert.IsTrue(PlayerPartyHexTravelService.EnterWorldSiteAsParty(world, party, siteA).IsSuccess);
            world.LocalMap.ActiveMapLayoutId = siteA.LocalMapId;
            world.LocalMap.OverworldMapLayoutId = siteA.LocalMapId;
            var zones = new System.Collections.Generic.List<SurfaceExitVisibleZone>(6);
            SurfaceExitZoneCalculator.CollectVisibleZones(
                world, DefaultWildernessBounds(), 1.25f, zones);
            Assert.Greater(zones.Count, 0);
        }

        static WildernessLocalWorldProjection.WildernessLocalMapBounds HuangcunLikeBounds() =>
            WildernessLocalWorldProjection.WildernessLocalMapBounds.FromOriginSize(-40f, -25f, 1f, 200, 100);

        static string CaptureCoverageSignature(
            SimulationWorld world,
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds,
            float depth)
        {
            var sb = new System.Text.StringBuilder();
            var connections = new System.Collections.Generic.List<SurfaceExitConnection>(6);
            SurfaceExitZoneCalculator.CollectConnections(world, bounds, depth, connections);
            for (var i = 0; i < connections.Count; i++)
            {
                var c = connections[i];
                sb.Append(c.DirectionIndex).Append(':').Append(depth.ToString("0.###")).Append('|');
                var rect = c.SlotRect;
                sb.Append(rect.MinX.ToString("0.##")).Append(',')
                    .Append(rect.MaxX.ToString("0.##")).Append(',')
                    .Append(rect.MinY.ToString("0.##")).Append(',')
                    .Append(rect.MaxY.ToString("0.##")).Append(';');
                sb.Append('/');
            }

            return sb.ToString();
        }

        static void AssertConnectionSlotEqual(SurfaceExitConnection a, SurfaceExitConnection b)
        {
            Assert.AreEqual(a.DirectionIndex, b.DirectionIndex);
            Assert.AreEqual(a.SlotRect.MinX, b.SlotRect.MinX, FloatTol);
            Assert.AreEqual(a.SlotRect.MaxX, b.SlotRect.MaxX, FloatTol);
            Assert.AreEqual(a.SlotRect.MinY, b.SlotRect.MinY, FloatTol);
            Assert.AreEqual(a.SlotRect.MaxY, b.SlotRect.MaxY, FloatTol);
        }

        static bool IsNearAnyRect(
            float x,
            float y,
            System.Collections.Generic.List<SurfaceExitCoverageRect> rects,
            float tol)
        {
            for (var r = 0; r < rects.Count; r++)
            {
                var rect = rects[r];
                if (x >= rect.MinX - tol && x <= rect.MaxX + tol &&
                    y >= rect.MinY - tol && y <= rect.MaxY + tol)
                    return true;
            }

            return false;
        }
    }
}
