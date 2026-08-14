using System.Collections.Generic;
using NUnit.Framework;
using XianXia.Core.Navigation;

namespace XianXia.Tests
{
    public sealed class HostNavigationPathFollowTests
    {
        [Test]
        public void PathAroundHouseBlock_DoesNotStayColinearThroughObstacle()
        {
            var grid = Ch01ReferenceWalkGrid.Create();
            // 枢纽 → 房屋前（已清障的可行走点），应绕开房屋主体
            var xy = new List<float>();
            Assert.IsTrue(GridPathfinder.TryFindWorldPath(grid, 0f, 0f, -8f, 10f, xy));
            Assert.Greater(xy.Count / 2, 3);

            for (var i = 0; i + 1 < xy.Count; i += 2)
            {
                Assert.IsTrue(grid.TryWorldToCell(xy[i], xy[i + 1], out var cx, out var cy));
                Assert.IsTrue(grid.IsWalkable(cx, cy), "waypoint in blocked cell");
            }
        }

        [Test]
        public void UnreachableDeepInsideBlock_SnapsOrFailsGracefully()
        {
            var grid = Ch01ReferenceWalkGrid.Create();
            var xy = new List<float>();
            // 房屋障碍深处：默认 goalSnap=4 可能失败；加大 snap 应落到附近可行走，且不抛异常
            Assert.DoesNotThrow(() =>
            {
                GridPathfinder.TryFindWorldPath(grid, 0f, 0f, -8f, 10.5f, xy, 8, 8);
            });
        }

        [Test]
        public void GoalInsideBlock_WithTightSnap_FailsInsteadOfFarTeleport()
        {
            var grid = new WalkGrid(0f, 0f, 1f, 20, 20);
            grid.SetBlockedRect(5, 5, 15, 15, true);
            var xy = new List<float>();
            // 点在障碍深处且 goalSnap=4：不应跳到很远，直接失败更可控
            Assert.IsFalse(GridPathfinder.TryFindWorldPath(grid, 1f, 1f, 10f, 10f, xy, 8, 4));
        }
    }
}
