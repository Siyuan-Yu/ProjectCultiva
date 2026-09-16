using UnityEngine;

namespace XianXia.Unity.Host
{
    /// <summary>World-space-only WorldMap camera. It deliberately has no Hex dependency.</summary>
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
        public Vector2 ProjectWorld(float worldX, float worldY) => new(
            _mapRect.center.x + (worldX - _centerX) * _scale,
            _mapRect.center.y - (worldY - _centerY) * _scale);
        public Vector2 ScreenToWorld(Vector2 screen) => new(
            _centerX + (screen.x - _mapRect.center.x) / _scale,
            _centerY - (screen.y - _mapRect.center.y) / _scale);
        public Rect ProjectWorldRect(Rect worldRect)
        {
            var a = ProjectWorld(worldRect.xMin, worldRect.yMin); var b = ProjectWorld(worldRect.xMax, worldRect.yMax);
            return Rect.MinMaxRect(Mathf.Min(a.x,b.x), Mathf.Min(a.y,b.y), Mathf.Max(a.x,b.x), Mathf.Max(a.y,b.y));
        }
        public Rect VisibleWorldBounds => Rect.MinMaxRect(
            _centerX - _mapRect.width / (2f * _scale), _centerY - _mapRect.height / (2f * _scale),
            _centerX + _mapRect.width / (2f * _scale), _centerY + _mapRect.height / (2f * _scale));
    }
}
