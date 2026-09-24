using UnityEngine;

namespace XianXia.Unity.Host
{
    /// <summary>World-space-only Continuous Surface WorldMap camera.</summary>
    public readonly struct SurfaceWorldMapViewportProjection
    {
        readonly Rect _mapRect;
        readonly float _centerX, _centerY, _half, _scale;
        public SurfaceWorldMapViewportProjection(Rect mapRect, float centerX, float centerY, float viewHalf)
        {
            _mapRect = mapRect; _centerX = centerX; _centerY = centerY; _half = Mathf.Max(.001f, viewHalf);
            _scale = Mathf.Min(mapRect.width, mapRect.height) / (2f * _half);
        }
        public float Scale => _scale;
        public Rect MapRect => _mapRect;
        public Vector2 ProjectWorld(float worldX, float worldY) => new(
            _mapRect.center.x + (worldX - _centerX) * _scale,
            _mapRect.center.y - (worldY - _centerY) * _scale);
        public Vector2 ScreenToWorld(Vector2 screen) => new(
            _centerX + (screen.x - _mapRect.center.x) / _scale,
            _centerY - (screen.y - _mapRect.center.y) / _scale);
        public bool TryScreenToWorld(Vector2 screen, out Vector2 world)
        {
            world = default;
            if (!_mapRect.Contains(screen)) return false;
            world = ScreenToWorld(screen);
            return true;
        }
        public Rect ProjectWorldRect(Rect worldRect)
        {
            var a = ProjectWorld(worldRect.xMin, worldRect.yMin); var b = ProjectWorld(worldRect.xMax, worldRect.yMax);
            return Rect.MinMaxRect(Mathf.Min(a.x,b.x), Mathf.Min(a.y,b.y), Mathf.Max(a.x,b.x), Mathf.Max(a.y,b.y));
        }
        public Rect VisibleWorldBounds => Rect.MinMaxRect(
            _centerX - _mapRect.width / (2f * _scale), _centerY - _mapRect.height / (2f * _scale),
            _centerX + _mapRect.width / (2f * _scale), _centerY + _mapRect.height / (2f * _scale));

        public static float ChooseMajorInterval(float visibleWorldSpan, int desiredIntervals = 7)
        {
            if (float.IsNaN(visibleWorldSpan) || float.IsInfinity(visibleWorldSpan) || visibleWorldSpan <= 0f)
                return 1f;
            desiredIntervals = Mathf.Clamp(desiredIntervals, 1, 20);
            var raw = visibleWorldSpan / desiredIntervals;
            var magnitude = Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(raw)));
            var normalized = raw / magnitude;
            var nice = normalized <= 1f ? 1f : normalized <= 2f ? 2f : normalized <= 5f ? 5f : 10f;
            return nice * magnitude;
        }
    }
}
