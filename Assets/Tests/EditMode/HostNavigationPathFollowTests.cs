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
            // 从枢纽左侧绕开房屋障碍区到房屋北侧
            var xy = new List<float>();
            Assert.IsTrue(GridPathfinder.TryFindWorldPath(grid, 0f, 0f, -8f, 12f, xy));
            Assert.Greater(xy.Count / 2, 3);

            var crossedBlocked = false;
            for (var i = 0; i + 1 < xy.Count; i += 2)
            {
                Assert.IsTrue(grid.TryWorldToCell(xy[i], xy[i + 1], out var cx, out var cy));
                // 路径点应落在可行走格（终点可能被 snap）
                if (!grid.IsWalkable(cx, cy))
                    crossedBlocked = true;
            }

            Assert.IsFalse(crossedBlocked);
        }

        [Test]
        public void UnreachableDeepInsideBlock_SnapsOrFailsGracefully()
        {
            var grid = Ch01ReferenceWalkGrid.Create();
            var xy = new List<float>();
            // 房屋障碍深处：应 snap 到附近可行走或失败，不能抛异常
            Assert.DoesNotThrow(() =>
            {
                GridPathfinder.TryFindWorldPath(grid, 0f, 0f, -8f, 10.5f, xy);
            });
        }
    }
}
