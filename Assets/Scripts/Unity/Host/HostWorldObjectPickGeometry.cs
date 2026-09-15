using UnityEngine;

namespace XianXia.Unity.Host
{
    /// <summary>Presentation-only click geometry, sampled after prefab scaling and bounds alignment.</summary>
    public static class HostWorldObjectPickGeometry
    {
        public const float ClickPadding = .08f;
        public const float FallbackRadius = .25f;

        public static bool TryGetInteractionBounds(GameObject go, out Rect bounds)
        {
            bounds = default;
            if (go == null) return false;
            var renderers = go.GetComponentsInChildren<SpriteRenderer>(true);
            var hasBounds = false;
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || !renderer.enabled || renderer.sprite == null) continue;
                Add(ref bounds, ref hasBounds, renderer.bounds);
            }
            if (!hasBounds)
            {
                var colliders = go.GetComponentsInChildren<Collider2D>(true);
                for (var i = 0; i < colliders.Length; i++)
                    if (colliders[i] != null && colliders[i].enabled)
                        Add(ref bounds, ref hasBounds, colliders[i].bounds);
            }
            if (!hasBounds)
            {
                var position = go.transform.position;
                bounds = Rect.MinMaxRect(position.x - FallbackRadius, position.y - FallbackRadius,
                    position.x + FallbackRadius, position.y + FallbackRadius);
            }
            bounds.xMin -= ClickPadding;
            bounds.xMax += ClickPadding;
            bounds.yMin -= ClickPadding;
            bounds.yMax += ClickPadding;
            return true;
        }

        static void Add(ref Rect rect, ref bool hasBounds, Bounds bounds)
        {
            var next = Rect.MinMaxRect(bounds.min.x, bounds.min.y, bounds.max.x, bounds.max.y);
            if (!hasBounds) { rect = next; hasBounds = true; }
            else rect = Rect.MinMaxRect(
                Mathf.Min(rect.xMin, next.xMin), Mathf.Min(rect.yMin, next.yMin),
                Mathf.Max(rect.xMax, next.xMax), Mathf.Max(rect.yMax, next.yMax));
        }
    }
}
