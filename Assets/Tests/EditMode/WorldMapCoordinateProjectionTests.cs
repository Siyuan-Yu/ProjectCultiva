using NUnit.Framework;
using UnityEngine;
using XianXia.Unity.Host;

namespace XianXia.Tests
{
    public sealed class WorldMapCoordinateProjectionTests
    {
        [Test]
        public void WorldToMapToWorldRoundTripPreservesExactSurfaceCoordinates()
        {
            var projection = new SurfaceWorldMapViewportProjection(
                new Rect(13, 27, 960, 540), 0f, 0f, 30f);
            var source = new Vector2(4.7f, 5.6f);
            var map = projection.ProjectWorld(source.x, source.y);
            Assert.IsTrue(projection.TryScreenToWorld(map, out var restored));
            Assert.AreEqual(source.x, restored.x, .0001f);
            Assert.AreEqual(source.y, restored.y, .0001f);
        }

        [Test]
        public void ZoomAndPanKeepMarkerInverseMappingStable()
        {
            var mapRect = new Rect(0, 0, 1200, 700);
            var source = new Vector2(-18.3f, 72.4f);
            var centers = new[] { new Vector2(0, 0), new Vector2(-20, 70), new Vector2(35, -12) };
            var viewHalves = new[] { 3f, 20f, 100f };
            foreach (var center in centers)
            foreach (var viewHalf in viewHalves)
            {
                var projection = new SurfaceWorldMapViewportProjection(
                    mapRect, center.x, center.y, viewHalf);
                var map = projection.ProjectWorld(source.x, source.y);
                var restored = projection.ScreenToWorld(map);
                Assert.AreEqual(source.x, restored.x, .0001f);
                Assert.AreEqual(source.y, restored.y, .0001f);
            }
        }

        [Test]
        public void PointerOutsideMapDoesNotKeepAStaleCoordinate()
        {
            var projection = new SurfaceWorldMapViewportProjection(new Rect(10, 20, 300, 200), 0, 0, 10);
            Assert.IsTrue(projection.TryScreenToWorld(new Vector2(150, 100), out _));
            Assert.IsFalse(projection.TryScreenToWorld(new Vector2(9, 100), out _));
            Assert.IsFalse(projection.TryScreenToWorld(new Vector2(150, 221), out _));
        }

        [Test]
        public void MajorIntervalsUseReadableOneTwoFiveStepsAtDifferentZooms()
        {
            var spans = new[] { 3f, 14f, 70f, 350f, 1200f };
            foreach (var span in spans)
            {
                var interval = SurfaceWorldMapViewportProjection.ChooseMajorInterval(span);
                var normalized = interval / Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(interval)));
                Assert.IsTrue(Mathf.Approximately(normalized, 1f) ||
                              Mathf.Approximately(normalized, 2f) ||
                              Mathf.Approximately(normalized, 5f));
                Assert.GreaterOrEqual(span / interval, 3f);
                Assert.LessOrEqual(span / interval, 10f);
            }
        }
    }
}
