using System;
using NUnit.Framework;
using UnityEngine;
using XianXia.Core.World.Hex;
using XianXia.Unity.Host;

namespace XianXia.Tests.EditMode
{
    public sealed class HexMapMousePickTests
    {
        const float HexSize = HexWorldScale.DefaultHexOuterRadius;

        static HexMapViewportProjection ProjectionAt(float viewHalf) =>
            new HexMapViewportProjection(
                new Rect(16f, 104f, 1200f, 700f),
                viewCenterX: 80f,
                viewCenterY: 40f,
                viewHalf: viewHalf,
                hexSize: HexSize);

        [Test]
        public void HEX_PICK_01_HexCenterRoundTrip_100Cells()
        {
            var count = 0;
            for (var q = 0; q < 10; q++)
            {
                for (var r = 0; r < 10; r++)
                {
                    var coord = new HexCoord(q, r);
                    Assert.IsTrue(
                        HexMetrics.ValidateCenterRoundTrip(coord, HexSize, out var back),
                        "center round-trip failed at " + coord);
                    Assert.AreEqual(coord, back);
                    count++;
                }
            }

            Assert.AreEqual(100, count);
        }

        [Test]
        public void HEX_PICK_02_ScreenCenterOfHex_ReturnsSameHex()
        {
            var projection = ProjectionAt(40f);
            var grid = BuildGrid(100, 50);
            var samples = new[] { new HexCoord(0, 0), new HexCoord(20, 25), new HexCoord(45, 14), new HexCoord(99, 49) };

            foreach (var coord in samples)
            {
                var screen = projection.ProjectHexCenter(coord);
                Assert.IsTrue(
                    HexMapMousePick.TryResolveMouseHex(projection, grid, screen, out var picked),
                    "pick failed at " + coord);
                Assert.AreEqual(coord, picked);
            }
        }

        [Test]
        public void HEX_PICK_03_SectorSamples_StayInSameHex()
        {
            var projection = ProjectionAt(25f);
            var grid = BuildGrid(100, 50);
            var coord = new HexCoord(45, 14);
            HexMetrics.HexCoordToWorldCenter(coord, HexSize, out var cx, out var cy);

            for (var i = 0; i < 6; i++)
            {
                var angle = (Math.PI / 3.0) * i;
                var sampleX = cx + HexSize * 0.35f * (float)Math.Cos(angle);
                var sampleY = cy + HexSize * 0.35f * (float)Math.Sin(angle);
                var screen = projection.ProjectWorld(sampleX, sampleY);
                Assert.IsTrue(HexMapMousePick.TryResolveMouseHex(projection, grid, screen, out var picked));
                Assert.AreEqual(coord, picked);
            }
        }

        [Test]
        public void HEX_PICK_04_AdjacentCenters_ReturnDistinctCoords()
        {
            var a = new HexCoord(10, 10);
            var b = HexMath.Neighbor(a, 0);
            HexMetrics.HexCoordToWorldCenter(a, HexSize, out var ax, out var ay);
            HexMetrics.HexCoordToWorldCenter(b, HexSize, out var bx, out var by);
            Assert.AreNotEqual(HexMetrics.WorldToHexCoord(bx, by, HexSize), a);
            Assert.AreEqual(b, HexMetrics.WorldToHexCoord(bx, by, HexSize));
        }

        [Test]
        public void HEX_PICK_05_Pan_DoesNotChangePickedHexForSameScreenPoint()
        {
            var grid = BuildGrid(100, 50);
            var coord = new HexCoord(30, 20);
            var p1 = ProjectionAt(40f);
            var screen = p1.ProjectHexCenter(coord);
            Assert.IsTrue(HexMapMousePick.TryResolveMouseHex(p1, grid, screen, out var pick1));
            Assert.AreEqual(coord, pick1);

            var p2 = new HexMapViewportProjection(
                p1.Viewport,
                viewCenterX: p1.ViewCenterX + 12f,
                viewCenterY: p1.ViewCenterY - 8f,
                viewHalf: p1.ViewHalf,
                hexSize: HexSize);
            Assert.IsTrue(HexMapMousePick.TryResolveMouseHex(p2, grid, screen, out var pick2));
            Assert.AreEqual(coord, pick2);
        }

        [Test]
        public void HEX_PICK_06_Zoom_DoesNotChangePickedHexForSameScreenPoint()
        {
            var grid = BuildGrid(100, 50);
            var coord = new HexCoord(55, 22);
            var p1 = ProjectionAt(50f);
            var screen = p1.ProjectHexCenter(coord);
            Assert.IsTrue(HexMapMousePick.TryResolveMouseHex(p1, grid, screen, out var pick1));
            Assert.AreEqual(coord, pick1);

            var p2 = ProjectionAt(18f);
            Assert.IsTrue(HexMapMousePick.TryResolveMouseHex(p2, grid, screen, out var pick2));
            Assert.AreEqual(coord, pick2);
        }

        static HexWorld BuildGrid(int width, int height)
        {
            var grid = new HexWorld { HexSize = HexSize, MapId = "test" };
            grid.FillRectangle(width, height);
            return grid;
        }
    }
}
