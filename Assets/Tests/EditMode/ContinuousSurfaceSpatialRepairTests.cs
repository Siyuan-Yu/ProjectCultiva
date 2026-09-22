using System.Collections.Generic;
using NUnit.Framework;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;
using XianXia.Core.World.Surface;
using XianXia.Data.Content;

namespace XianXia.Tests.EditMode
{
    public sealed class ContinuousSurfaceSpatialRepairTests
    {
        [Test]
        public void MovementScale_UsesCurrentWorldValueAndFallsBackForNonPositiveValues()
        {
            var world = new SimulationWorld();
            foreach (var row in new[]
            {
                new { Raw = -1f, Expected = 1f },
                new { Raw = 0f, Expected = 1f },
                new { Raw = 0.00005f, Expected = 0.00005f },
                new { Raw = 2.5f, Expected = 2.5f }
            })
            {
                world.ContinuousWorldMovementScale = row.Raw;
                Assert.AreEqual(row.Expected, ContinuousWorldMovementScale.Resolve(world));
            }
            Assert.AreEqual(1f, ContinuousWorldMovementScale.Resolve(null));
            var unit = (float)System.Math.Sqrt(3.0) / 8f;
            Assert.AreEqual(unit,
                PlayerPartyTravelRuntimeService.WorldUnitsPerTick(0.00005f));
            Assert.AreEqual(unit * 2.5f,
                PlayerPartyTravelRuntimeService.WorldUnitsPerTick(2.5f));
            Assert.AreEqual(unit * 2.5f * 7f,
                BackgroundSimulationScheduler.DistanceBudgetFromElapsedSimulationTicks(2.5f, 7),
                0.000001f);
        }

        [Test]
        public void MapperPositivePresentationY_RemainsPositiveWorldY()
        {
            var mapper = new OutdoorSurfaceCoordinateMapper(
                1.4f, 1.4f, 0.028f, presentationUnitsPerWorldUnit: 1f / 0.028f);
            mapper.PresentationToWorld(0f, 10f, out _, out var y);
            Assert.Greater(y, 0f);
        }

        [Test]
        public void AuthoredCoverageEgress_DistinguishesInsideCrossingAndOutsideAtActualRayEdge()
        {
            var surface = new OutdoorWorldSurfaceDefinition
            {
                OriginWorldX = 10f,
                OriginWorldY = -5f,
                ChunkWidth = 2f,
                ChunkHeight = 2f,
                CellSize = 1f,
                Chunks = new List<OutdoorSurfaceChunkDefinition>
                {
                    new OutdoorSurfaceChunkDefinition { Coord = new SurfaceChunkCoord(0, 0) },
                    new OutdoorSurfaceChunkDefinition { Coord = new SurfaceChunkCoord(1, 0) }
                }
            };

            Assert.IsTrue(OutdoorSurfaceBoundaryEgressResolver.TryResolve(
                surface, 11f, -4f, 12f, -4f, out var inside));
            Assert.AreEqual(OutdoorSurfaceBoundaryEgressResolver.State.Inside, inside.EgressState);

            Assert.IsTrue(OutdoorSurfaceBoundaryEgressResolver.TryResolve(
                surface, 11f, -4f, 20f, -4f, out var crossing));
            Assert.AreEqual(
                OutdoorSurfaceBoundaryEgressResolver.State.CrossingOuterBoundary,
                crossing.EgressState);
            Assert.AreEqual(14f, crossing.BoundaryWorldX, 0.0001f);
            Assert.Greater(crossing.JustOutsideWorldX, crossing.BoundaryWorldX);
            Assert.IsFalse(OutdoorSurfaceCoverageResolver.ContainsWorldPosition(
                surface, crossing.JustOutsideWorldX, crossing.JustOutsideWorldY));

            Assert.IsTrue(OutdoorSurfaceBoundaryEgressResolver.TryResolve(
                surface, 20f, -4f, 21f, -4f, out var outside));
            Assert.AreEqual(OutdoorSurfaceBoundaryEgressResolver.State.Outside, outside.EgressState);
        }

        [Test]
        public void AutoTravelClassification_OutsideLoadedNeighborhoodCanRemainInsideAuthoredSurface()
        {
            var surface = new OutdoorWorldSurfaceDefinition
            {
                ChunkWidth = 1f,
                ChunkHeight = 1f,
                CellSize = 1f,
                Chunks = new List<OutdoorSurfaceChunkDefinition>()
            };
            for (var x = -2; x <= 2; x++)
            for (var y = -2; y <= 2; y++)
                surface.Chunks.Add(new OutdoorSurfaceChunkDefinition
                    { Coord = new SurfaceChunkCoord(x, y) });

            Assert.IsTrue(OutdoorSurfaceBoundaryEgressResolver.TryResolve(
                surface, 0.5f, 0.5f, 2.5f, 0.5f, out var stillInside));
            Assert.AreEqual(OutdoorSurfaceBoundaryEgressResolver.State.Inside, stillInside.EgressState);
            Assert.IsTrue(OutdoorSurfaceBoundaryEgressResolver.TryResolve(
                surface, 2.5f, 0.5f, 4f, 0.5f, out var outsideSurface));
            Assert.AreEqual(
                OutdoorSurfaceBoundaryEgressResolver.State.CrossingOuterBoundary,
                outsideSurface.EgressState);
        }
    }
}
