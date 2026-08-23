using NUnit.Framework;
using UnityEngine;
using XianXia.Core.World.Hex;
using XianXia.Unity.Host;

namespace XianXia.Tests.EditMode
{
    public sealed class HexMapViewportProjectionTests
    {
        const float HexSize = HexWorldScale.DefaultHexOuterRadius;

        static HexMapViewportProjection SampleProjection(float viewHalf = 40f) =>
            new HexMapViewportProjection(
                new Rect(16f, 104f, 1200f, 700f),
                viewCenterX: 120f,
                viewCenterY: 45f,
                viewHalf: viewHalf,
                hexSize: HexSize);

        [Test]
        public void HexMath_RoundTrip_AllSampleCoords()
        {
            var coords = new[]
            {
                new HexCoord(0, 0),
                new HexCoord(20, 25),
                new HexCoord(38, 22),
                new HexCoord(99, 49),
                new HexCoord(5, 17),
            };

            foreach (var coord in coords)
            {
                HexMath.ToWorldPosition(coord, HexSize, out var wx, out var wy);
                var back = HexMath.WorldToHex(wx, wy, HexSize);
                Assert.AreEqual(coord, back, "Hex round-trip failed at " + coord);
            }
        }

        [Test]
        public void ViewportProjection_RoundTrip_SampleHexes()
        {
            var projection = SampleProjection();
            var coords = new[] { new HexCoord(0, 0), new HexCoord(20, 25), new HexCoord(45, 14) };

            foreach (var coord in coords)
            {
                Assert.IsTrue(
                    projection.ValidateHexRoundTrip(coord, out var hexBack),
                    "Hex round-trip failed at " + coord);
                Assert.AreEqual(coord, hexBack);

                Assert.IsTrue(
                    projection.ValidateProjectionRoundTrip(coord, out var projBack),
                    "Projection round-trip failed at " + coord);
                Assert.AreEqual(coord, projBack);
            }
        }

        [Test]
        public void ViewportProjection_ScreenToWorld_InverseOfProjectWorld()
        {
            var projection = SampleProjection();
            var worldPoints = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(120f, 45f),
                new Vector2(133.5f, 52.2f),
            };

            foreach (var world in worldPoints)
            {
                var screen = projection.ProjectWorld(world.x, world.y);
                var back = projection.ScreenToWorld(screen);
                Assert.AreEqual(world.x, back.x, 0.001f);
                Assert.AreEqual(world.y, back.y, 0.001f);
            }
        }

        [Test]
        public void ViewportProjection_PanAndZoom_StillRoundTrips()
        {
            var projection = new HexMapViewportProjection(
                new Rect(0f, 100f, 800f, 600f),
                viewCenterX: 200f,
                viewCenterY: 80f,
                viewHalf: 12f,
                hexSize: HexSize);
            var hex = new HexCoord(45, 14);
            Assert.IsTrue(projection.ValidateProjectionRoundTrip(hex, out var back));
            Assert.AreEqual(hex, back);
        }
    }
}
