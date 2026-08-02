using System.Collections.Generic;
using NUnit.Framework;
using XianXia.Core.Navigation;

namespace XianXia.Tests
{
    public sealed class NavigationFoundationTests
    {
        [Test]
        public void Ch01Grid_HasBlockedCells_AndKeySitesWalkable()
        {
            var grid = Ch01ReferenceWalkGrid.Create();
            Assert.Greater(grid.BlockedCount, 10);
            Assert.IsTrue(grid.TryWorldToCell(20f, -12f, out var fx, out var fy));
            Assert.IsTrue(grid.IsWalkable(fx, fy));
            Assert.IsTrue(grid.TryWorldToCell(0f, 0f, out var hx, out var hy));
            Assert.IsTrue(grid.IsWalkable(hx, hy));
        }

        [Test]
        public void AStar_FindsPathAroundBlockedRect()
        {
            var grid = new WalkGrid(0f, 0f, 1f, 10, 10);
            grid.SetBlockedRect(4, 0, 4, 8, true);

            var path = new List<GridCoord>();
            Assert.IsTrue(GridPathfinder.TryFindPath(grid, 0, 5, 9, 5, path));
            Assert.Greater(path.Count, 5);
            Assert.AreEqual(0, path[0].X);
            Assert.AreEqual(9, path[path.Count - 1].X);
            for (var i = 0; i < path.Count; i++)
                Assert.IsTrue(grid.IsWalkable(path[i].X, path[i].Y));
        }

        [Test]
        public void AStar_FailsWhenGoalBlockedAndNoSnap()
        {
            var grid = new WalkGrid(0f, 0f, 1f, 5, 5);
            grid.SetBlocked(4, 4, true);
            var path = new List<GridCoord>();
            Assert.IsFalse(GridPathfinder.TryFindPath(grid, 0, 0, 4, 4, path));
        }

        [Test]
        public void WorldPath_FarmToForest_ExistsOnCh01()
        {
            var grid = Ch01ReferenceWalkGrid.Create();
            var xy = new List<float>();
            Assert.IsTrue(GridPathfinder.TryFindWorldPath(grid, 20f, -12f, -34f, 0f, xy));
            Assert.GreaterOrEqual(xy.Count, 4);
            Assert.AreEqual(0, xy.Count % 2);
        }

        [Test]
        public void NearestWalkable_FindsAdjacentWhenStandingOnBlock()
        {
            var grid = new WalkGrid(0f, 0f, 1f, 5, 5);
            grid.SetBlocked(2, 2, true);
            Assert.IsTrue(grid.TryFindNearestWalkable(2, 2, 3, out var x, out var y));
            Assert.IsTrue(grid.IsWalkable(x, y));
        }
    }
}
