using System.Collections.Generic;
using NUnit.Framework;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Surface;

namespace XianXia.Tests.EditMode
{
    public sealed class OutdoorSurfaceW1CTests
    {
        [Test]
        public void WorldChunkLocal_RoundTripsAcrossNegativeCoordinates()
        {
            var mapper = new OutdoorSurfaceCoordinateMapper(50f, 50f, 1f, 7f, -3f);
            mapper.WorldToChunkLocal(-0.25f, 101.5f, out var chunk, out var localX, out var localY);
            Assert.AreEqual(new SurfaceChunkCoord(-1, 2), chunk);
            mapper.ChunkLocalToWorld(chunk, localX, localY, out var x, out var y);
            Assert.AreEqual(-0.25f, x, 0.0001f); Assert.AreEqual(101.5f, y, 0.0001f);
            mapper.WorldToPresentation(x, y, out var px, out var py);
            Assert.AreEqual(chunk, mapper.PresentationToChunk(px, py));
        }

        [Test]
        public void WorldPresentation_UsesOneUniformPrototypeMetric()
        {
            var mapper = new OutdoorSurfaceCoordinateMapper(1f, 1f, 0.02f, presentationUnitsPerWorldUnit: 50f);
            mapper.WorldToPresentation(1.5f, -0.5f, out var px, out var py);
            Assert.AreEqual(75f, px, 0.0001f);
            Assert.AreEqual(-25f, py, 0.0001f);
            mapper.PresentationToWorld(px, py, out var wx, out var wy);
            Assert.AreEqual(1.5f, wx, 0.0001f);
            Assert.AreEqual(-0.5f, wy, 0.0001f);
        }

        [Test]
        public void ChunkGrid_IsIndependentOfStrategicHexNamingAndDimensions()
        {
            var mapper = new OutdoorSurfaceCoordinateMapper(50f, 50f, 1f);
            // Chunk (0,0) spans a continuous rectangle; the assertion deliberately has no HexCoord input.
            Assert.AreEqual(new SurfaceChunkCoord(0, 0), mapper.WorldToChunk(49.9f, 49.9f));
            Assert.AreEqual(new SurfaceChunkCoord(1, 0), mapper.WorldToChunk(50f, 49.9f));
        }

        [Test]
        public void ChunkAndHexBoundaries_CrossIndependently()
        {
            var mapper = new OutdoorSurfaceCoordinateMapper(1f, 1f, 0.02f);
            // The shared Hex edge is near x=0.866 for hexSize=1, while the chunk edge is x=1.
            // Thus a Hex can change while the player remains in chunk (0,0).
            var beforeHex = HexMath.WorldToHex(0.75f, 0f, 1f);
            var afterHex = HexMath.WorldToHex(0.95f, 0f, 1f);
            Assert.AreNotEqual(beforeHex, afterHex);
            Assert.AreEqual(mapper.WorldToChunk(0.75f, 0f), mapper.WorldToChunk(0.95f, 0f));

            // Crossing x=1 moves to the next chunk but stays inside that same strategic Hex.
            Assert.AreEqual(afterHex, HexMath.WorldToHex(1.05f, 0f, 1f));
            Assert.AreNotEqual(mapper.WorldToChunk(0.95f, 0f), mapper.WorldToChunk(1.05f, 0f));
        }

        [Test]
        public void RadiusOneMoveEast_OnlyDiffsOuterColumns()
        {
            var oldSet = new HashSet<SurfaceChunkCoord>(); var desired = new HashSet<SurfaceChunkCoord>();
            var added = new HashSet<SurfaceChunkCoord>(); var removed = new HashSet<SurfaceChunkCoord>();
            SurfaceChunkNeighborhood.CollectSquare(new SurfaceChunkCoord(0, 0), 1, oldSet);
            SurfaceChunkNeighborhood.CollectSquare(new SurfaceChunkCoord(1, 0), 1, desired);
            SurfaceChunkNeighborhood.Diff(oldSet, desired, added, removed);
            Assert.AreEqual(3, added.Count); Assert.AreEqual(3, removed.Count);
            for (var y = -1; y <= 1; y++) { Assert.IsTrue(added.Contains(new SurfaceChunkCoord(2, y))); Assert.IsTrue(removed.Contains(new SurfaceChunkCoord(-1, y))); }
        }

        [Test]
        public void ChunkRectangles_DetectOnlyActualOverlap()
        {
            Assert.IsFalse(SurfaceChunkNeighborhood.PhysicalRectsOverlap(new SurfaceChunkCoord(0, 0), new SurfaceChunkCoord(1, 0), 80f, 50f));
            Assert.IsTrue(SurfaceChunkNeighborhood.PhysicalRectsOverlap(new SurfaceChunkCoord(0, 0), new SurfaceChunkCoord(0, 0), 80f, 50f));
        }

        [Test]
        public void OwnerKey_IsDeterministicAcrossReloads()
        {
            Assert.AreEqual("surface:base:main_continent_surface:chunk:-1:2", SurfaceChunkNeighborhood.OwnerKey("base:main_continent_surface", new SurfaceChunkCoord(-1, 2)));
        }
    }
}
