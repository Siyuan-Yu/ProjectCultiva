using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using XianXia.Core.Navigation;
using XianXia.Data.Content;

namespace XianXia.Tests
{
    public sealed class MapLayoutWalkGridTests
    {
        [Test]
        public void Ch01ReferenceMap_BuildsBlockedGrid_AndPathsAroundHouse()
        {
            var path = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", "Content", "BaseGame", "Data", "Maps", "ch01_reference_map.json"));
            Assert.IsTrue(File.Exists(path), path);

            var loaded = MapLayoutJsonLoader.LoadFromFile(path);
            Assert.IsTrue(loaded.IsSuccess, loaded.IsFailure ? loaded.Error.ToString() : "");
            var grid = MapLayoutWalkGridBuilder.Create(loaded.Value);
            Assert.Greater(grid.BlockedCount, 20, "house/walls should block");

            // House footprint (25,40)-(33,47) must be blocked.
            Assert.IsFalse(grid.IsWalkable(28, 44));

            var xy = new List<float>();
            // Left of house → right of house (same row in world).
            Assert.IsFalse(
                GridPathfinder.IsWorldSegmentWalkable(grid, -20f, 18f, -2f, 18f),
                "straight line must cut the house");
            Assert.IsTrue(GridPathfinder.TryFindWorldPath(grid, -20f, 18f, -2f, 18f, xy));
            Assert.GreaterOrEqual(xy.Count / 2, 2);

            for (var i = 0; i + 1 < xy.Count; i += 2)
            {
                Assert.IsTrue(grid.TryWorldToCell(xy[i], xy[i + 1], out var cx, out var cy));
                Assert.IsTrue(grid.IsWalkable(cx, cy), "waypoint in blocked cell " + cx + "," + cy);
            }

            for (var i = 0; i + 3 < xy.Count; i += 2)
            {
                Assert.IsTrue(
                    GridPathfinder.IsWorldSegmentWalkable(grid, xy[i], xy[i + 1], xy[i + 2], xy[i + 3]),
                    "pulled segment must stay walkable");
            }
        }

        [Test]
        public void WorldSegment_RejectsCutThroughBlockedRect()
        {
            var grid = new WalkGrid(0f, 0f, 1f, 10, 10);
            grid.SetBlockedRect(4, 0, 4, 9, true);
            Assert.IsFalse(GridPathfinder.IsWorldSegmentWalkable(grid, 0.5f, 5.5f, 9.5f, 5.5f));
            Assert.IsTrue(GridPathfinder.IsWorldSegmentWalkable(grid, 0.5f, 5.5f, 2.5f, 5.5f));
        }
    }
}
