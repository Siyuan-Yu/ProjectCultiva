using NUnit.Framework;
using XianXia.Core.World.Hex;

namespace XianXia.Tests
{
    public sealed class HexGridPhaseH1Tests
    {
        [Test]
        public void HEX01_HexHasExactlySixNeighbors()
        {
            var center = new HexCoord(3, 4);
            var neighbors = new System.Collections.Generic.List<HexCoord>(6);
            HexMath.CollectNeighbors(center, neighbors);
            Assert.AreEqual(6, neighbors.Count);

            var expected = new[]
            {
                new HexCoord(4, 4),
                new HexCoord(4, 3),
                new HexCoord(3, 3),
                new HexCoord(2, 4),
                new HexCoord(2, 5),
                new HexCoord(3, 5),
            };

            for (var i = 0; i < expected.Length; i++)
                Assert.Contains(expected[i], neighbors, "Neighbor " + i);
        }

        [Test]
        public void HEX02_HexDistanceIsCorrect()
        {
            var a = new HexCoord(0, 0);
            var b = new HexCoord(3, 0);
            var c = new HexCoord(0, 3);
            Assert.AreEqual(3, HexMath.Distance(a, b));
            Assert.AreEqual(3, HexMath.Distance(a, c));
            Assert.AreEqual(5, HexMath.Distance(new HexCoord(2, 3), new HexCoord(5, 1)));
        }

        [Test]
        public void HEX03_AStarFindsValidPath()
        {
            var grid = new HexWorld();
            grid.FillRectangle(8, 8);
            var path = new System.Collections.Generic.List<HexCoord>();
            Assert.IsTrue(HexPathfinder.TryFindPath(grid, new HexCoord(0, 0), new HexCoord(5, 3), path));
            Assert.Greater(path.Count, 1);
            Assert.AreEqual(new HexCoord(0, 0), path[0]);
            Assert.AreEqual(new HexCoord(5, 3), path[path.Count - 1]);
        }

        [Test]
        public void HEX04_ImpassableTileAvoided()
        {
            var grid = new HexWorld();
            grid.FillRectangle(5, 5);
            var blocker = new HexCoord(2, 2);
            var tile = grid.GetOrCreate(blocker);
            tile.Terrain = HexTerrainType.Water;
            tile.IsPassable = false;

            var path = new System.Collections.Generic.List<HexCoord>();
            Assert.IsTrue(HexPathfinder.TryFindPath(grid, new HexCoord(0, 0), new HexCoord(4, 0), path));
            CollectionAssert.DoesNotContain(path, blocker);
        }

        [Test]
        public void HEX05_RoadLowerMovementCostInfluencesPath()
        {
            var grid = new HexWorld();
            grid.FillRectangle(5, 3);
            for (var q = 0; q < 5; q++)
            {
                for (var r = 0; r < 3; r++)
                {
                    var tile = grid.GetOrCreate(new HexCoord(q, r));
                    tile.Terrain = HexTerrainType.Forest;
                }
            }

            for (var q = 0; q < 5; q++)
            {
                var road = grid.GetOrCreate(new HexCoord(q, 1));
                road.IsRoad = true;
            }

            var direct = new System.Collections.Generic.List<HexCoord>();
            var start = new HexCoord(0, 0);
            var goal = new HexCoord(4, 2);
            Assert.IsTrue(HexPathfinder.TryFindPath(grid, start, goal, direct));

            var usesRoad = false;
            for (var i = 0; i < direct.Count; i++)
            {
                if (direct[i].R == 1)
                {
                    usesRoad = true;
                    break;
                }
            }

            Assert.IsTrue(usesRoad, "Path should prefer road hexes with lower movement cost.");
        }

        [Test]
        public void HEX06_WorldToHex_RoundTripsCenter()
        {
            const float size = 1f;
            var coord = new HexCoord(4, 6);
            HexMath.ToWorldPosition(coord, size, out var wx, out var wy);
            var picked = HexMath.WorldToHex(wx, wy, size);
            Assert.AreEqual(coord, picked);
        }
    }
}
