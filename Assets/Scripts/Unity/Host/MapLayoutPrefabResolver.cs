using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace XianXia.Unity.Host
{
    /// <summary>
    /// MapEditor kind 与 prefab 严格一一对应；缺失时用 MissingPrefab 占位，不再偷换其它 prefab。
    /// </summary>
    public static class MapLayoutPrefabResolver
    {
        static readonly HashSet<string> WarnedPaths = new HashSet<string>();

        public static int MissingCount { get; private set; }

        public static void BeginBatch()
        {
            WarnedPaths.Clear();
            MissingCount = 0;
        }

        public static bool TryInstantiate(
            string kind,
            string prefabPath,
            out GameObject instance,
            bool warnOnMissing = true)
        {
            instance = null;
            if (TryLoadPrefab(prefabPath, out var prefab))
            {
#if UNITY_EDITOR
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
#else
                instance = Object.Instantiate(prefab);
#endif
                return instance != null;
            }

            if (warnOnMissing)
            {
                MissingCount++;
                WarnMissing(kind, prefabPath);
            }

            return false;
        }

        public static bool PrefabExists(string prefabPath) =>
            TryLoadPrefab(prefabPath, out _);

        public static IReadOnlyList<string> ValidateCatalogPrefabs()
        {
            var missing = new List<string>();
            var seen = new HashSet<string>();
            for (var i = 0; i < MapKindCatalog.All.Length; i++)
            {
                var path = MapKindCatalog.All[i].PrefabPath;
                if (string.IsNullOrEmpty(path) || !seen.Add(path))
                    continue;
                if (MapKindCatalog.All[i].Mode == MapKindCatalog.StampMode.ZoneOverlay)
                    continue;
                if (!PrefabExists(path))
                    missing.Add(MapKindCatalog.All[i].Kind + " → " + path);
            }

            return missing;
        }

        static bool TryLoadPrefab(string prefabPath, out GameObject prefab)
        {
            prefab = null;
            if (string.IsNullOrEmpty(prefabPath))
                return false;

#if UNITY_EDITOR
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null && prefabPath == MapKindCatalog.House)
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MapKindCatalog.HouseFallback);
            return prefab != null;
#else
            return false;
#endif
        }

        static void WarnMissing(string kind, string prefabPath)
        {
            var key = (kind ?? string.Empty) + "|" + (prefabPath ?? string.Empty);
            if (!WarnedPaths.Add(key))
                return;
            Debug.LogWarning(
                "[MapLayout] Missing prefab for kind '" + (kind ?? "?") +
                "' at " + (prefabPath ?? "(empty)") +
                ". Using MissingPrefab placeholder. Run XianXia/Content/Ensure MapLayout Prefabs.");
        }
    }
}
