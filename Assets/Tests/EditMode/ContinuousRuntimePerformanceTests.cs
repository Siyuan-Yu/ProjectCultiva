using System.Collections.Generic;
using NUnit.Framework;
using XianXia.Core.Navigation;
using XianXia.Unity.Host;

namespace XianXia.Tests.EditMode
{
    /// <summary>
    /// Continuous runtime 性能回归守卫：
    ///  - A* workspace 必须与旧实现给出**完全相同**的 path（禁止改变 path result semantics）；
    ///  - NPC 日程 repath policy：正常移动中的 NPC 不因 timer 到期而重新 A*；
    ///  - InteractSpot 批量 build 只重建一次 flatten，且 slot 查询结果与旧实现一致。
    /// </summary>
    public sealed class ContinuousRuntimePerformanceTests
    {
        // ---- A 组：A* workspace ----

        static WalkGrid BuildGrid(int w, int h, int seed)
        {
            var grid = new WalkGrid(0f, 0f, 1f, w, h);
            var rng = new System.Random(seed);
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                if (x == 0 || y == 0 || x == w - 1 || y == h - 1)
                {
                    grid.SetBlocked(x, y, true);
                    continue;
                }

                if (rng.NextDouble() < 0.2)
                    grid.SetBlocked(x, y, true);
            }

            return grid;
        }

        static void AssertSamePath(List<GridCoord> expected, List<GridCoord> actual, string context)
        {
            Assert.AreEqual(expected.Count, actual.Count, context + " count");
            for (var i = 0; i < expected.Count; i++)
                Assert.AreEqual(expected[i].X + "," + expected[i].Y, actual[i].X + "," + actual[i].Y,
                    context + " cell[" + i + "]");
        }

        [Test]
        public void A01_WorkspacePathMatchesLegacyImplementationOnManyRandomGrids()
        {
            for (var seed = 0; seed < 40; seed++)
            {
                var grid = BuildGrid(24, 18, seed);
                var rng = new System.Random(seed * 7919 + 13);
                var workspace = new GridPathfinderWorkspace();
                for (var attempt = 0; attempt < 12; attempt++)
                {
                    var sx = rng.Next(1, grid.Width - 1);
                    var sy = rng.Next(1, grid.Height - 1);
                    var gx = rng.Next(1, grid.Width - 1);
                    var gy = rng.Next(1, grid.Height - 1);
                    if (!grid.IsWalkable(sx, sy) || !grid.IsWalkable(gx, gy))
                        continue;

                    var legacy = new List<GridCoord>();
                    var legacyFound = LegacyTryFindPath(grid, sx, sy, gx, gy, legacy);
                    var actual = new List<GridCoord>();
                    var actualFound = workspace.TryFindPath(grid, sx, sy, gx, gy, actual);

                    var context = "seed=" + seed + " " + sx + "," + sy + "→" + gx + "," + gy;
                    Assert.AreEqual(legacyFound, actualFound, context + " found");
                    if (legacyFound)
                        AssertSamePath(legacy, actual, context);
                }
            }
        }

        [Test]
        public void A02_WorkspaceResetBetweenCallsIsExact()
        {
            var grid = BuildGrid(20, 16, 3);
            var workspace = new GridPathfinderWorkspace();
            var first = new List<GridCoord>();
            var second = new List<GridCoord>();
            var fresh = new List<GridCoord>();

            Assert.IsTrue(workspace.TryFindPath(grid, 1, 1, 18, 14, first));
            // 同实例重复调用（buffer 复用 + 只复位 touched cells）：结果必须一致。
            Assert.IsTrue(workspace.TryFindPath(grid, 1, 1, 18, 14, second));
            AssertSamePath(first, second, "reused workspace");

            var other = new GridPathfinderWorkspace();
            Assert.IsTrue(other.TryFindPath(grid, 1, 1, 18, 14, fresh));
            AssertSamePath(first, fresh, "fresh workspace");
        }

        [Test]
        public void A03_WorkspaceSurvivesUnreachableThenReachableAndSizeChanges()
        {
            var workspace = new GridPathfinderWorkspace();

            // 目标被完全封死 → false（写入大量 stale 状态）。
            var sealedGrid = BuildGrid(16, 16, 11);
            for (var x = 0; x < sealedGrid.Width; x++)
                sealedGrid.SetBlocked(x, 8, true);
            var path = new List<GridCoord>();
            Assert.IsFalse(workspace.TryFindPath(sealedGrid, 2, 2, 2, 13, path));

            // 之后在另一尺寸 grid 上成功寻路：stale 状态不得污染结果。
            // 明确打通中间走廊，保证 start→goal 可达（避免随机阻挡影响用例确定性）。
            var open = BuildGrid(28, 12, 12);
            for (var x = 0; x < open.Width; x++)
                open.SetBlocked(x, 6, false);
            var reference = new List<GridCoord>();
            var afterFailure = new List<GridCoord>();
            Assert.IsTrue(new GridPathfinderWorkspace().TryFindPath(open, 1, 6, 26, 6, reference));
            Assert.IsTrue(workspace.TryFindPath(open, 1, 6, 26, 6, afterFailure));
            AssertSamePath(reference, afterFailure, "after failure + resize");
        }

        [Test]
        public void A04_StaticApiMatchesWorkspaceAndWorldPathStaysStable()
        {
            var grid = BuildGrid(22, 16, 21);
            // 明确打通一条走廊，保证 start→goal 可达。
            for (var x = 0; x < grid.Width; x++)
                grid.SetBlocked(x, 8, false);

            var direct = new List<GridCoord>();
            Assert.IsTrue(GridPathfinder.TryFindPath(grid, 1, 8, 20, 8, direct));

            grid.CellToWorldCenter(1, 8, out var sx, out var sy);
            grid.CellToWorldCenter(20, 8, out var gx, out var gy);
            var xy = new List<float>();
            Assert.IsTrue(GridPathfinder.TryFindWorldPath(grid, sx, sy, gx, gy, xy));
            Assert.GreaterOrEqual(xy.Count, 2);

            // 连续两次必须一致（string-pull 之后不得因 buffer 复用而漂移）。
            var xy2 = new List<float>();
            Assert.IsTrue(GridPathfinder.TryFindWorldPath(grid, sx, sy, gx, gy, xy2));
            Assert.AreEqual(xy.Count, xy2.Count);
            for (var i = 0; i < xy.Count; i++)
                Assert.AreEqual(xy[i], xy2[i], 1e-5f);
        }

        // ---- B 组：NPC repath policy（§7） ----

        [Test]
        public void B01_MovingNpcIsNeverRepathedByTimerAlone()
        {
            Assert.IsFalse(NpcSchedulePathRequestPolicy.ShouldRequestPath(
                targetChanged: false, stuck: false, isMoving: true,
                repathDue: true, gridRevisionChanged: false),
                "正常沿路径移动中：timer 到期也不得重新 A*");
            Assert.IsFalse(NpcSchedulePathRequestPolicy.ShouldRequestPath(
                targetChanged: false, stuck: false, isMoving: true,
                repathDue: false, gridRevisionChanged: false));
        }

        [Test]
        public void B02_TargetChangeRequestsImmediately()
        {
            Assert.IsTrue(NpcSchedulePathRequestPolicy.ShouldRequestPath(
                targetChanged: true, stuck: false, isMoving: true,
                repathDue: false, gridRevisionChanged: false));
        }

        [Test]
        public void B03_NotMovingRequestsAreCooledDown()
        {
            Assert.IsTrue(NpcSchedulePathRequestPolicy.ShouldRequestPath(
                targetChanged: false, stuck: false, isMoving: false,
                repathDue: true, gridRevisionChanged: false));
            Assert.IsFalse(NpcSchedulePathRequestPolicy.ShouldRequestPath(
                targetChanged: false, stuck: false, isMoving: false,
                repathDue: false, gridRevisionChanged: false));
        }

        [Test]
        public void B04_StuckAndGridRevisionAreControlledRepaths()
        {
            Assert.IsTrue(NpcSchedulePathRequestPolicy.ShouldRequestPath(
                targetChanged: false, stuck: true, isMoving: true,
                repathDue: true, gridRevisionChanged: false));
            Assert.IsFalse(NpcSchedulePathRequestPolicy.ShouldRequestPath(
                targetChanged: false, stuck: true, isMoving: true,
                repathDue: false, gridRevisionChanged: false));

            Assert.IsTrue(NpcSchedulePathRequestPolicy.ShouldRequestPath(
                targetChanged: false, stuck: false, isMoving: true,
                repathDue: true, gridRevisionChanged: true));
            Assert.IsFalse(NpcSchedulePathRequestPolicy.ShouldRequestPath(
                targetChanged: false, stuck: false, isMoving: true,
                repathDue: false, gridRevisionChanged: true));
        }

        // ---- C 组：InteractSpot batch build / slot 查询（§11 §12） ----

        [Test]
        public void C01_SlotSpotMatchesLegacySelectionOrder()
        {
            HostInteractSpots.ClearAll();
            try
            {
                HostInteractSpots.BeginOwnerBuild("test:owner");
                var expected = new List<HostInteractSpot>();
                for (var i = 0; i < 4; i++)
                {
                    var spot = new HostInteractSpot("base:loc_farm", HostInteractSpotKind.Work, i, 0f, "田" + i);
                    HostInteractSpots.RegisterPlot("test:owner", spot);
                    expected.Add(spot);
                }

                HostInteractSpots.RegisterPlot("test:owner",
                    new HostInteractSpot("base:loc_spring", HostInteractSpotKind.Cultivate, 0f, 5f, "泉"));
                HostInteractSpots.EndOwnerBuild();

                for (var slot = -3; slot < 7; slot++)
                {
                    var legacyIndex = slot % expected.Count;
                    if (legacyIndex < 0)
                        legacyIndex += expected.Count;

                    Assert.IsTrue(HostInteractSpots.TryGetSlotSpot(
                        "base:loc_farm", HostInteractSpotKind.Work, slot, out var spot, null),
                        "slot=" + slot);
                    Assert.AreEqual(expected[legacyIndex].PresentationX, spot.PresentationX, 1e-4f, "slot=" + slot);
                    Assert.AreEqual(expected[legacyIndex].Label, spot.Label, "slot=" + slot);
                }

                Assert.IsFalse(HostInteractSpots.TryGetSlotSpot(
                    "base:loc_missing", HostInteractSpotKind.Work, 0, out _, null));
            }
            finally
            {
                HostInteractSpots.ClearAll();
            }
        }

        [Test]
        public void C02_OwnerBatchBuildFlattensOnceNotPerCell()
        {
            HostInteractSpots.ClearAll();
            try
            {
                HostInteractSpots.BeginOwnerBuild("test:bulk");
                var generationAfterBegin = HostInteractSpots.LayoutGeneration;
                for (var i = 0; i < 40; i++)
                    HostInteractSpots.RegisterPlot("test:bulk",
                        new HostInteractSpot("base:loc_farm", HostInteractSpotKind.Work, i, 0f, "格" + i));

                // 逐格注册不得触发 flatten（旧实现每格 O(N) rebuild = O(N²)）。
                Assert.AreEqual(generationAfterBegin, HostInteractSpots.LayoutGeneration,
                    "register 阶段不应 flatten");

                HostInteractSpots.EndOwnerBuild();
                Assert.AreEqual(generationAfterBegin, HostInteractSpots.LayoutGeneration,
                    "EndOwnerBuild 只标脏，不立即 flatten");

                var spots = HostInteractSpots.GetSpots(null);
                Assert.Greater(HostInteractSpots.LayoutGeneration, generationAfterBegin,
                    "首次 query 才 flatten 一次");
                Assert.AreEqual(40, spots.Count);
                Assert.AreEqual(40, HostInteractSpots.LoadedSpotCount);
            }
            finally
            {
                HostInteractSpots.ClearAll();
            }
        }

        [Test]
        public void C03_ClearAllDropsSpotsAndBumpsGeneration()
        {
            HostInteractSpots.ClearAll();
            HostInteractSpots.BeginOwnerBuild("test:one");
            HostInteractSpots.RegisterPlot("test:one",
                new HostInteractSpot("base:loc_forest", HostInteractSpotKind.Work, 0f, 0f, "树"));
            HostInteractSpots.EndOwnerBuild();
            Assert.Greater(HostInteractSpots.GetSpots(null).Count, 0);

            var before = HostInteractSpots.LayoutGeneration;
            HostInteractSpots.ClearAll();
            Assert.Greater(HostInteractSpots.LayoutGeneration, before);
            Assert.AreEqual(0, HostInteractSpots.GetSpots(null).Count);
            Assert.IsFalse(HostInteractSpots.TryGetSlotSpot(
                "base:loc_forest", HostInteractSpotKind.Work, 0, out _, null));
        }

        // ---- Legacy reference（旧实现副本，仅用于等价性断言） ----

        static readonly int[] LegacyDx = { 0, 0, 1, -1, 1, -1, 1, -1 };
        static readonly int[] LegacyDy = { 1, -1, 0, 0, 1, 1, -1, -1 };

        /// <summary>旧 GridPathfinder.TryFindPath 的逐行副本（linear-scan open list）。</summary>
        static bool LegacyTryFindPath(
            WalkGrid grid, int startX, int startY, int goalX, int goalY, List<GridCoord> pathOut)
        {
            pathOut.Clear();
            if (!grid.IsWalkable(startX, startY) || !grid.IsWalkable(goalX, goalY))
                return false;
            if (startX == goalX && startY == goalY)
            {
                pathOut.Add(new GridCoord(startX, startY));
                return true;
            }

            var w = grid.Width;
            var h = grid.Height;
            var len = w * h;
            var gScore = new int[len];
            var fScore = new int[len];
            var cameFrom = new int[len];
            var closed = new bool[len];
            for (var i = 0; i < len; i++)
            {
                gScore[i] = int.MaxValue;
                fScore[i] = int.MaxValue;
                cameFrom[i] = -1;
            }

            var start = startY * w + startX;
            var goal = goalY * w + goalX;
            gScore[start] = 0;
            fScore[start] = LegacyHeuristic(startX, startY, goalX, goalY);

            var open = new List<int>(64) { start };
            while (open.Count > 0)
            {
                var bestI = 0;
                var bestF = fScore[open[0]];
                for (var i = 1; i < open.Count; i++)
                {
                    var f = fScore[open[i]];
                    if (f >= bestF)
                        continue;
                    bestF = f;
                    bestI = i;
                }

                var current = open[bestI];
                open.RemoveAt(bestI);
                if (current == goal)
                {
                    LegacyReconstruct(cameFrom, goal, w, pathOut);
                    return true;
                }

                if (closed[current])
                    continue;
                closed[current] = true;

                var cx = current % w;
                var cy = current / w;
                for (var n = 0; n < 8; n++)
                {
                    var nx = cx + LegacyDx[n];
                    var ny = cy + LegacyDy[n];
                    if (!grid.IsWalkable(nx, ny))
                        continue;
                    var diagonal = n >= 4;
                    if (diagonal && !(grid.IsWalkable(nx, cy) && grid.IsWalkable(cx, ny)))
                        continue;

                    var ni = ny * w + nx;
                    if (closed[ni])
                        continue;

                    var step = diagonal ? 14 : 10;
                    var tentative = gScore[current] + step;
                    if (tentative >= gScore[ni])
                        continue;

                    cameFrom[ni] = current;
                    gScore[ni] = tentative;
                    fScore[ni] = tentative + LegacyHeuristic(nx, ny, goalX, goalY);
                    if (!open.Contains(ni))
                        open.Add(ni);
                }
            }

            return false;
        }

        static int LegacyHeuristic(int ax, int ay, int bx, int by)
        {
            var dx = System.Math.Abs(ax - bx);
            var dy = System.Math.Abs(ay - by);
            return 10 * (dx + dy) + (14 - 2 * 10) * System.Math.Min(dx, dy);
        }

        static void LegacyReconstruct(int[] cameFrom, int goal, int w, List<GridCoord> pathOut)
        {
            var stack = new List<int>(32);
            for (var cur = goal; cur >= 0; cur = cameFrom[cur])
            {
                stack.Add(cur);
                if (cameFrom[cur] < 0)
                    break;
            }

            for (var i = stack.Count - 1; i >= 0; i--)
            {
                var idx = stack[i];
                pathOut.Add(new GridCoord(idx % w, idx / w));
            }
        }
    }
}
