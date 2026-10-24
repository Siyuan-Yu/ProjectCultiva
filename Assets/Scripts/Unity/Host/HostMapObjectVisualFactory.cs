using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

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
            bool disableColliders, out bool usedMissingVisual, int visualQuarterTurns = 0)
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
            // Cell wood walls use one renderer, not a placeholder plus disabled overlay.
            var directWoodWall = !missing && (prefabPath == MapKindCatalog.WoodWall || prefabPath == MapKindCatalog.Wall);
            if (directWoodWall)
            {
                var renderer = go.GetComponentInChildren<SpriteRenderer>();
                var sprite = prefabPath == MapKindCatalog.WoodWall
                    ? HostStepAArt.MapSprite("wall") : HostStepAArt.StoneFloorTile();
                if (renderer != null)
                {
                    if (sprite != null) renderer.sprite = sprite;
                    renderer.transform.localRotation = Quaternion.Euler(0, 0, -90f * (visualQuarterTurns % 4));
                }
            }
            using (ListPool<SpriteRenderer>.Get(out var renderers))
            {
                go.GetComponentsInChildren(true, renderers);
                Fit(go, renderers, Mathf.Max(.01f, worldWidth), Mathf.Max(.01f, worldHeight));
                Align(go, renderers, intendedCenter);
                var geometry = go.GetComponent<HostMapVisualGeometry>() ?? go.AddComponent<HostMapVisualGeometry>();
                geometry.Configure(intendedCenter, worldWidth, worldHeight);
                if (!missing && !directWoodWall &&
                    HostStepAArt.ApplyObjectVisual(go, HostStepAArt.MapSprite(kind), visualQuarterTurns,
                        kind == "wall" || kind == "road" || kind == "controlCore",
                        kind == "controlCore"))
                    go.GetComponentsInChildren(true, renderers); // The art overlay may add renderers.
                var order = HostArtDepth.ObjectOrder(kind, intendedCenter.y, worldHeight, sortingOrder);
                for (var i = 0; i < renderers.Count; i++)
                    if (renderers[i] != null) renderers[i].sortingOrder = order;
            }
            StripNonHostBehaviours(go);
            if (disableColliders)
            {
                using (ListPool<Component>.Get(out var components))
                {
                    go.GetComponentsInChildren(true, components);
                    for (var i = 0; i < components.Count; i++)
                    {
                        var component = components[i];
                        if (component is Collider2D collider) collider.enabled = false;
                        if (component != null && component.GetType().Name.EndsWith("Collider", System.StringComparison.Ordinal))
                        {
                            if (Application.isPlaying) Object.Destroy(component); else Object.DestroyImmediate(component);
                        }
                    }
                }
            }
            usedMissingVisual = missing;
            return go;
        }

        public static bool TryGetRendererBounds(GameObject go, out Bounds bounds)
        {
            using (ListPool<SpriteRenderer>.Get(out var renderers))
            {
                go.GetComponentsInChildren(true, renderers);
                return TryGetRendererBounds(renderers, out bounds);
            }
        }

        static bool TryGetRendererBounds(List<SpriteRenderer> renderers, out Bounds bounds)
        {
            bounds = default; var any = false;
            for (var i = 0; i < renderers.Count; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || !renderer.enabled || renderer.sprite == null) continue;
                if (!any) { bounds = renderer.bounds; any = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return any;
        }

        static void Fit(GameObject go, List<SpriteRenderer> renderers, float width, float height)
        {
            if (!TryGetRendererBounds(renderers, out var bounds) || bounds.size.x < .0001f || bounds.size.y < .0001f) return;
            go.transform.localScale = new Vector3(width / bounds.size.x, height / bounds.size.y, 1f);
        }

        static void Align(GameObject go, List<SpriteRenderer> renderers, Vector3 center)
        {
            if (TryGetRendererBounds(renderers, out var bounds)) go.transform.position += center - bounds.center;
        }

        static void StripNonHostBehaviours(GameObject go)
        {
            using (ListPool<MonoBehaviour>.Get(out var behaviours))
            {
                go.GetComponentsInChildren(true, behaviours);
                for (var i = 0; i < behaviours.Count; i++)
                {
                    var behaviour = behaviours[i];
                    if (behaviour == null || (behaviour.GetType().Namespace ?? string.Empty)
                        .StartsWith("XianXia.Unity.Host", System.StringComparison.Ordinal)) continue;
                    if (Application.isPlaying) Object.Destroy(behaviour); else Object.DestroyImmediate(behaviour);
                }
            }
        }
    }
}
