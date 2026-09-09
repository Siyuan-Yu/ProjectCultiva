using System;
using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;
using XianXia.Core.World.Surface;
using XianXia.Data.Content;

namespace XianXia.Tests.EditMode
{
    public sealed class ContinuousSurfaceSpatialRepairTests
    {
        [Test]
        public void CentersRoundTrip_PositiveNegativeOddEvenRowsAndScales()
        {
            foreach (var size in new[] { 0.25f, 1f, 3f })
            for (var r = -5; r <= 5; r++)
            for (var q = -4; q <= 4; q++)
            {
                var hex = new HexCoord(q, r);
                HexMath.ToWorldPosition(hex, size, out var x, out var y);
                Assert.AreEqual(hex, HexMath.WorldToHex(x, y, size));
                Assert.AreEqual(hex, HexMetrics.WorldToHexCoord(x, y, size));
            }
        }

        [Test]
        public void AllSixSharedEdges_ResolveCorrectSide_IncludingFourDiagonals()
        {
            for (var row = -2; row <= 1; row++)
            for (var dir = 0; dir < 6; dir++)
            {
                var a = new HexCoord(1, row);
                var b = HexMath.Neighbor(a, dir);
                HexMath.ToWorldPosition(a, 1f, out var ax, out var ay);
                HexMath.ToWorldPosition(b, 1f, out var bx, out var by);
                var x = (ax + bx) / 2f; var y = (ay + by) / 2f;
                Assert.AreEqual(a, HexMath.WorldToHex(x - (bx - ax) * 0.001f, y - (by - ay) * 0.001f, 1f));
                Assert.AreEqual(b, HexMath.WorldToHex(x + (bx - ax) * 0.001f, y + (by - ay) * 0.001f, 1f));
            }
        }

        [Test]
        public void CornerNearbySamples_AgreeWithPolygonHalfPlanes()
        {
            foreach (var origin in new[] { new HexCoord(-1, -1), new HexCoord(2, 2) })
            {
                HexMath.ToWorldPosition(origin, 1f, out var cx, out var cy);
                for (var corner = 0; corner < 6; corner++)
                for (var sample = 0; sample < 12; sample++)
                {
                    var angle = Math.PI / 6 + corner * Math.PI / 3;
                    var jitter = (sample + 0.37) * Math.PI / 6;
                    var x = cx + (float)(Math.Cos(angle) + 0.01 * Math.Cos(jitter));
                    var y = cy + (float)(Math.Sin(angle) + 0.01 * Math.Sin(jitter));
                    HexCoord expected = default;
                    var matches = 0;
                    for (var r = origin.R - 2; r <= origin.R + 2; r++)
                    for (var q = origin.Q - 2; q <= origin.Q + 2; q++)
                    {
                        var candidate = new HexCoord(q, r);
                        if (!InsidePolygon(candidate, x, y)) continue;
                        expected = candidate; matches++;
                    }
                    Assert.AreEqual(1, matches, "Samples deliberately avoid exact ties");
                    Assert.AreEqual(expected, HexMath.WorldToHex(x, y, 1f));
                }
            }
        }

        static bool InsidePolygon(HexCoord hex, float x, float y)
        {
            HexMath.ToWorldPosition(hex, 1f, out var cx, out var cy);
            // Pointy polygon outward normals at 0,60,... ; apothem = sqrt(3)/2.
            for (var i = 0; i < 6; i++)
                if ((x - cx) * Math.Cos(i * Math.PI / 3) + (y - cy) * Math.Sin(i * Math.PI / 3) > Math.Sqrt(3) / 2 + 0.000001)
                    return false;
            return true;
        }

        [Test]
        public void CommitHysteresis_DoesNotHoldWholeNeighborOrPingPong()
        {
            var a = new HexCoord(0, 0); var b = HexMath.Neighbor(a, 0);
            var edge = (float)Math.Sqrt(3) / 2f;
            var h = ContinuousSurfaceHexCommitResolver.HysteresisFraction;
            Assert.AreEqual(a, ContinuousSurfaceHexCommitResolver.Resolve(a, new WorldVec2(edge - 0.01f, 0f), 1f));
            Assert.AreEqual(a, ContinuousSurfaceHexCommitResolver.Resolve(a, new WorldVec2(edge + h / 2, 0f), 1f));
            Assert.AreEqual(b, ContinuousSurfaceHexCommitResolver.Resolve(a, new WorldVec2(edge + h * 1.1f, 0f), 1f));
            Assert.AreEqual(b, ContinuousSurfaceHexCommitResolver.Resolve(b, new WorldVec2(edge - h / 2, 0f), 1f));
            Assert.AreEqual(a, ContinuousSurfaceHexCommitResolver.Resolve(b, new WorldVec2(edge - h * 1.1f, 0f), 1f));
            var far = new HexCoord(4, 3);
            HexMath.ToWorldPosition(far, 1f, out var x, out var y);
            Assert.AreEqual(far, ContinuousSurfaceHexCommitResolver.Resolve(a, new WorldVec2(x, y), 1f));
            Assert.AreEqual(a, WildernessLocalWorldProjection.ResolveAuthoritativeWildernessHex(a, new WorldVec2(1.5f, 0f), 1f));
        }

        [Test]
        public void MapperPositivePresentationY_RemainsPositiveWorldY()
        {
            var mapper = new OutdoorSurfaceCoordinateMapper(1.4f, 1.4f, 0.028f, presentationUnitsPerWorldUnit: 1f / 0.028f);
            mapper.PresentationToWorld(0f, 10f, out _, out var y);
            Assert.Greater(y, 0f);
        }

        [Test]
        public void AuthoredCoverageEgress_DistinguishesInsideCrossingAndOutside_AtActualRayEdge()
        {
            var surface = new OutdoorWorldSurfaceDefinition
            {
                OriginWorldX = 10f, OriginWorldY = -5f, ChunkWidth = 2f, ChunkHeight = 2f, CellSize = 1f,
                Chunks = new System.Collections.Generic.List<OutdoorSurfaceChunkDefinition>
                {
                    new OutdoorSurfaceChunkDefinition { Coord = new SurfaceChunkCoord(0, 0) },
                    new OutdoorSurfaceChunkDefinition { Coord = new SurfaceChunkCoord(1, 0) },
                }
            };
            Assert.IsTrue(OutdoorSurfaceBoundaryEgressResolver.TryResolve(surface, 11f, -4f, 12f, -4f, out var inside));
            Assert.AreEqual(OutdoorSurfaceBoundaryEgressResolver.State.Inside, inside.EgressState);
            Assert.IsTrue(OutdoorSurfaceBoundaryEgressResolver.TryResolve(surface, 11f, -4f, 20f, -4f, out var crossing));
            Assert.AreEqual(OutdoorSurfaceBoundaryEgressResolver.State.CrossingOuterBoundary, crossing.EgressState);
            Assert.AreEqual(14f, crossing.BoundaryWorldX, 0.0001f);
            Assert.Greater(crossing.JustOutsideWorldX, crossing.BoundaryWorldX);
            Assert.IsFalse(OutdoorSurfaceCoverageResolver.ContainsWorldPosition(surface,
                crossing.JustOutsideWorldX, crossing.JustOutsideWorldY));
            Assert.IsTrue(OutdoorSurfaceBoundaryEgressResolver.TryResolve(surface, 20f, -4f, 21f, -4f, out var outside));
            Assert.AreEqual(OutdoorSurfaceBoundaryEgressResolver.State.Outside, outside.EgressState);
        }

        [Test]
        public void BoundaryHandoffGroundGate_AllowsPassableAndRejectsWater()
        {
            var world = new SimulationWorld();
            world.HexWorld.HexSize = 1f;
            var current = new HexCoord(0, 0);
            var target = new HexCoord(1, 0);
            world.HexWorld.GetOrCreate(current);
            var targetTile = world.HexWorld.GetOrCreate(target);
            var traveler = world.Entities.CreateCharacter(new DefinitionId("test", "egress_traveler"), "traveler").Value.Id;
            world.PlayerPartyTravel.SetAtWorldPosition(new WorldVec2(0f, 0f), current);
            world.PlayerPartyTravel.CaptureTravelingMembers(new[] { traveler });
            world.WorldPresence.SetAtHex(traveler, current);
            targetTile.Terrain = HexTerrainType.Plain; targetTile.IsPassable = true;
            Assert.IsTrue(PlayerPartyWildernessTransitionService.TryCommitContinuousSurfaceBoundaryEgress(
                world, new WorldVec2(1f, 0f), target).IsSuccess);
            Assert.AreEqual(target, world.PlayerPartyTravel.CurrentHex);
            Assert.IsTrue(world.WorldPresence.TryGet(traveler, out var presence));
            Assert.AreEqual(target, presence.ResidualHex);
            world.PlayerPartyTravel.SetAtWorldPosition(new WorldVec2(0f, 0f), current);
            targetTile.Terrain = HexTerrainType.Water;
            Assert.IsTrue(PlayerPartyWildernessTransitionService.TryCommitContinuousSurfaceBoundaryEgress(
                world, new WorldVec2(1f, 0f), target).IsFailure);
            Assert.AreEqual(current, world.PlayerPartyTravel.CurrentHex);
        }

        [Test]
        public void AutoTravelClassification_OutsideLoadedNeighborhoodCanStillBeInsideAuthoredSurface()
        {
            var surface = new OutdoorWorldSurfaceDefinition
            {
                ChunkWidth = 1f, ChunkHeight = 1f, CellSize = 1f,
                Chunks = new System.Collections.Generic.List<OutdoorSurfaceChunkDefinition>()
            };
            for (var x = -2; x <= 2; x++)
            for (var y = -2; y <= 2; y++)
                surface.Chunks.Add(new OutdoorSurfaceChunkDefinition { Coord = new SurfaceChunkCoord(x, y) });
            // From chunk (0,0), chunk (2,0) is outside radius-1 but remains authored coverage.
            Assert.IsTrue(OutdoorSurfaceBoundaryEgressResolver.TryResolve(surface, 0.5f, 0.5f, 2.5f, 0.5f, out var stillInside));
            Assert.AreEqual(OutdoorSurfaceBoundaryEgressResolver.State.Inside, stillInside.EgressState);
            Assert.IsTrue(OutdoorSurfaceBoundaryEgressResolver.TryResolve(surface, 2.5f, 0.5f, 4f, 0.5f, out var outsideSurface));
            Assert.AreEqual(OutdoorSurfaceBoundaryEgressResolver.State.CrossingOuterBoundary, outsideSurface.EgressState);
        }

        [Test]
        public void PrototypeGroundLegality_RoadIsNotRequired_WaterAndMissingReject()
        {
            var grid = new HexWorld();
            var a = new HexCoord(0, 0); var b = new HexCoord(1, 0);
            var tile = grid.GetOrCreate(b);
            foreach (var terrain in new[] { HexTerrainType.Plain, HexTerrainType.Forest, HexTerrainType.Mountain })
            {
                tile.Terrain = terrain; tile.IsRoad = false; tile.IsPassable = true;
                Assert.IsTrue(ContinuousSurfacePrototypeGroundLegality.CanCross(grid, a, b));
            }
            tile.Terrain = HexTerrainType.Water; tile.IsRoad = true;
            Assert.IsFalse(ContinuousSurfacePrototypeGroundLegality.CanCross(grid, a, b));
            tile.Terrain = HexTerrainType.Plain; tile.IsPassable = false;
            Assert.IsFalse(ContinuousSurfacePrototypeGroundLegality.CanCross(grid, a, b));
            Assert.IsFalse(ContinuousSurfacePrototypeGroundLegality.CanCross(grid, a, new HexCoord(3, 0)));
            Assert.IsTrue(ContinuousSurfacePrototypeGroundLegality.CanCross(grid, a, a));
        }

        [Test]
        public void SyncRejectsBeforeMutation_ManualAndAutoTravelKeepSafePositionAndPresence()
        {
            foreach (var autoTravel in new[] { false, true })
            {
                var world = new SimulationWorld();
                world.HexWorld.HexSize = 1f;
                var a = new HexCoord(0, 0); var b = new HexCoord(1, 0);
                world.HexWorld.GetOrCreate(a);
                var tile = world.HexWorld.GetOrCreate(b);
                tile.Terrain = HexTerrainType.Water; tile.IsPassable = true;
                var member = world.Entities.CreateCharacter(new DefinitionId("test", "member"), "member").Value.Id;
                var motion = world.PlayerPartyTravel;
                var safe = new WorldVec2(0.85f, 0f);
                motion.SetAtWorldPosition(safe, a);
                if (autoTravel)
                {
                    motion.BeginAutoTravel(new[] { a, b }, b, string.Empty, HexTravelMode.Ground, 1f);
                    motion.SetExecutionMode(PlayerPartyTravelExecutionMode.LocalVisible);
                }
                motion.CaptureTravelingMembers(new[] { member });
                world.WorldPresence.SetAtHex(member, a);
                // Includes the derived-but-not-committed band; canonical must not leak into water.
                foreach (var x in new[] { 0.88f, 1.0f })
                {
                    Assert.IsTrue(PlayerPartyWildernessTransitionService.TrySyncContinuousSurfaceWorldPosition(world, x, 0f).IsFailure);
                    Assert.AreEqual(safe, motion.WorldPosition);
                    Assert.AreEqual(a, motion.CurrentHex);
                    Assert.IsTrue(world.WorldPresence.TryGet(member, out var presence));
                    Assert.AreEqual(a, presence.ResidualHex);
                }
                if (autoTravel)
                {
                    Assert.IsTrue(motion.IsMoving);
                    Assert.AreEqual(2, motion.HexPathCount);
                    Assert.AreEqual(0, motion.SegmentIndex);
                    Assert.AreEqual(b, motion.DestinationHex);
                }
                tile.Terrain = HexTerrainType.Plain; tile.IsRoad = false;
                Assert.IsTrue(PlayerPartyWildernessTransitionService.TrySyncContinuousSurfaceWorldPosition(world, 1.0f, 0f).IsSuccess);
                Assert.AreEqual(b, motion.CurrentHex);
                Assert.IsTrue(world.WorldPresence.TryGet(member, out var accepted));
                Assert.AreEqual(b, accepted.ResidualHex);
            }
        }
    }
}
