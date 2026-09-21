using System.Collections.Generic;
using NUnit.Framework;
using XianXia.Core.Navigation;

namespace XianXia.Tests
{
    public sealed class ContinuousSurfaceLocalRouteResolverTests
    {
        [Test]
        public void ImmediateReachableWaypoint_RemainsCurrentTarget()
        {
            var grid = new WalkGrid(0f, 0f, 1f, 8, 5);
            Assert.IsTrue(ContinuousSurfaceLocalRouteResolver.TryResolve(
                grid, .5f, 2.5f, new[] { 1.5f, 2.5f, 4.5f, 2.5f }, 4,
                out var index, out _, out _, out var kind, out _));
            Assert.AreEqual(4, index);
            Assert.AreEqual(ContinuousWalkGridSubgoalKind.ExactLoadedGoal, kind);
        }

        [Test]
        public void BlockedWaypoint_LooksAheadToFurthestConnectedCandidate()
        {
            var grid = new WalkGrid(0f, 0f, 1f, 8, 5);
            grid.SetBlocked(1, 2, true);
            var candidates = new[] { 1.5f, 2.5f, 2.5f, 2.5f, 4.5f, 2.5f };
            Assert.IsTrue(ContinuousSurfaceLocalRouteResolver.TryResolve(
                grid, .5f, 2.5f, candidates, 1,
                out var index, out var x, out var y, out var kind, out var failure), failure);
            Assert.AreEqual(3, index);
            Assert.AreEqual(4.5f, x, .001f);
            Assert.AreEqual(2.5f, y, .001f);
            Assert.AreEqual(ContinuousWalkGridSubgoalKind.ExactLoadedGoal, kind);
            var path = new List<float>();
            Assert.IsTrue(GridPathfinder.TryFindWorldPath(grid, .5f, 2.5f, x, y, path));
        }

        [Test]
        public void DisconnectedCandidates_ReturnExplicitNoPhysicalRoute()
        {
            var grid = new WalkGrid(0f, 0f, 1f, 8, 5);
            for (var y = 0; y < grid.Height; y++) grid.SetBlocked(1, y, true);
            Assert.IsFalse(ContinuousSurfaceLocalRouteResolver.TryResolve(
                grid, .5f, 2.5f, new[] { 1.5f, 2.5f, 4.5f, 2.5f }, 1,
                out _, out _, out _, out _, out var failure));
            Assert.AreEqual("NoPhysicalRoute", failure);
        }

        [Test]
        public void OutsideLoadedRoute_ResolvesReachableFrontier()
        {
            var grid = new WalkGrid(0f, 0f, 1f, 5, 5);
            Assert.IsTrue(ContinuousSurfaceLocalRouteResolver.TryResolve(
                grid, .5f, 2.5f, new[] { 7.5f, 2.5f }, 9,
                out var index, out var x, out _, out var kind, out var failure), failure);
            Assert.AreEqual(-1, index);
            Assert.AreEqual(4.5f, x, .001f);
            Assert.AreEqual(ContinuousWalkGridSubgoalKind.ReachableFrontier, kind);
        }
    }
}
