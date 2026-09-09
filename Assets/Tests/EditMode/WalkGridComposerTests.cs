using System;
using System.Collections.Generic;
using NUnit.Framework;
using XianXia.Core.Navigation;

namespace XianXia.Tests.EditMode
{
    public sealed class WalkGridComposerTests
    {
        [Test]
        public void Compose_AdjacentWalkableGrids_ProducesContinuousPath()
        {
            var left = new WalkGrid(0f, 0f, 1f, 2, 1);
            var right = new WalkGrid(0f, 0f, 1f, 2, 1);
            var composite = WalkGridComposer.Compose(new[]
            {
                new WalkGridComposer.Input(left, 0f, 0f),
                new WalkGridComposer.Input(right, 2f, 0f),
            });

            var path = new List<GridCoord>();
            Assert.IsTrue(GridPathfinder.TryFindPath(composite, 0, 0, 3, 0, path));
            Assert.AreEqual(4, composite.Width);
        }

        [Test]
        public void Compose_GapIsBlocked()
        {
            var a = new WalkGrid(0f, 0f, 1f, 1, 1);
            var b = new WalkGrid(0f, 0f, 1f, 1, 1);
            var composite = WalkGridComposer.Compose(new[]
            {
                new WalkGridComposer.Input(a, 0f, 0f),
                new WalkGridComposer.Input(b, 2f, 0f),
            });

            Assert.IsFalse(composite.IsWalkable(1, 0));
            Assert.IsFalse(GridPathfinder.TryFindPath(composite, 0, 0, 2, 0, new List<GridCoord>()));
        }

        [Test]
        public void Compose_OverlapBlockerWins()
        {
            var open = new WalkGrid(0f, 0f, 1f, 1, 1);
            var blocked = new WalkGrid(0f, 0f, 1f, 1, 1);
            blocked.SetBlocked(0, 0, true);

            var composite = WalkGridComposer.Compose(new[]
            {
                new WalkGridComposer.Input(open, 4f, 3f),
                new WalkGridComposer.Input(blocked, 4f, 3f),
            });

            Assert.IsFalse(composite.IsWalkable(0, 0));
            Assert.AreEqual(4f, composite.OriginX);
            Assert.AreEqual(3f, composite.OriginY);
        }

        [Test]
        public void Compose_DifferentCellSizeFailsExplicitly()
        {
            Assert.Throws<InvalidOperationException>(() => WalkGridComposer.Compose(new[]
            {
                new WalkGridComposer.Input(new WalkGrid(0f, 0f, 1f, 1, 1), 0f, 0f),
                new WalkGridComposer.Input(new WalkGrid(0f, 0f, 2f, 1, 1), 0f, 0f),
            }));
        }
    }
}
