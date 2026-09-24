using UnityEngine;

namespace XianXia.Unity.Host
{
    /// <summary>Shared MapKindCatalog prefab/fallback visual construction.</summary>
    public static class HostMapObjectVisualFactory
    {
        public static GameObject Create(string kind, string prefabPath, string name, Transform parent,
            Vector3 intendedCenter, float worldWidth, float worldHeight, int sortingOrder,
            bool disableColliders)
        {
            return Create(kind, prefabPath, name, parent, intendedCenter, worldWidth, worldHeight,
                sortingOrder, disableColliders, out _);
        }

        public static GameObject Create(string kind, string prefabPath, string name, Transform parent,
            Vector3 intendedCenter, float worldWidth, float worldHeight, int sortingOrder,
            bool disableColliders, out bool usedMissingVisual)
        {
            GameObject go;
            var missing = false;
            if (!MapLayoutPrefabResolver.TryInstantiate(kind, prefabPath, out go) &&
                !MapLayoutPrefabResolver.TryInstantiate(kind, MapKindCatalog.MissingPrefab, out go, false))
            {
                go = new GameObject(name);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = HostSpriteFactory.MissingPrefabSprite();
                renderer.color = Color.white;
                missing = true;
            }
            else if (!MapLayoutPrefabResolver.PrefabExists(prefabPath)) missing = true;
            go.name = missing ? name + "_MissingPrefab" : name;
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one;
            go.transform.position = intendedCenter;
            Fit(go, Mathf.Max(.01f, worldWidth), Mathf.Max(.01f, worldHeight));
            Align(go, intendedCenter);
            foreach (var renderer in go.GetComponentsInChildren<SpriteRenderer>(true))
                if (renderer != null) renderer.sortingOrder = missing ? sortingOrder + 50 : sortingOrder;
            StripNonHostBehaviours(go);
            if (disableColliders)
            {
                foreach (var collider in go.GetComponentsInChildren<Collider2D>(true))
                    if (collider != null) collider.enabled = false;
                foreach (var component in go.GetComponentsInChildren<Component>(true))
                    if (component != null && component.GetType().Name.EndsWith("Collider", System.StringComparison.Ordinal))
                    {
                        if (Application.isPlaying) Object.Destroy(component); else Object.DestroyImmediate(component);
                    }
            }
            usedMissingVisual = missing;
            return go;
        }

        public static bool TryGetRendererBounds(GameObject go, out Bounds bounds)
        {
            bounds = default; var any = false;
            foreach (var renderer in go.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer == null || !renderer.enabled || renderer.sprite == null) continue;
                if (!any) { bounds = renderer.bounds; any = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return any;
        }

        static void Fit(GameObject go, float width, float height)
        {
            if (!TryGetRendererBounds(go, out var bounds) || bounds.size.x < .0001f || bounds.size.y < .0001f) return;
            go.transform.localScale = new Vector3(width / bounds.size.x, height / bounds.size.y, 1f);
        }

        static void Align(GameObject go, Vector3 center)
        {
            if (TryGetRendererBounds(go, out var bounds)) go.transform.position += center - bounds.center;
        }

        static void StripNonHostBehaviours(GameObject go)
        {
            foreach (var behaviour in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null || (behaviour.GetType().Namespace ?? string.Empty)
                    .StartsWith("XianXia.Unity.Host", System.StringComparison.Ordinal)) continue;
                if (Application.isPlaying) Object.Destroy(behaviour); else Object.DestroyImmediate(behaviour);
            }
        }
    }
}
