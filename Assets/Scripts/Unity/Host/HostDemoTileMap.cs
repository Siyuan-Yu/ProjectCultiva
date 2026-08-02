using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Stamps Demo_v0_1 ground layout with Sprite tile prefabs (Editor AssetDatabase).
    /// Default stride=1 so tiles are edge-to-edge (no skybox gaps).
    /// </summary>
    public sealed class HostDemoTileMap : MonoBehaviour
    {
        const int MapMinX = -40;
        const int MapMinY = -25;
        const int MapWidth = 80;
        const int MapHeight = 50;

        [SerializeField] Transform mapRoot;
        [SerializeField] int stride = 1;
        [SerializeField] bool buildOnRebuild = true;

        readonly List<GameObject> _built = new List<GameObject>();

        public int TileCount => _built.Count;

        public void Rebuild()
        {
            Clear();
            if (!buildOnRebuild)
                return;
            EnsureRoot();
            var step = Mathf.Max(1, stride);
            var maxX = MapMinX + MapWidth - 1;
            var maxY = MapMinY + MapHeight - 1;
            for (var y = MapMinY; y <= maxY; y += step)
            for (var x = MapMinX; x <= maxX; x += step)
            {
                var path = ChooseGroundPrefab(x, y);
                PlaceTile(path, x + 0.5f, y + 0.5f, "Tile_" + x + "_" + y);
            }
        }

        public void Clear()
        {
            for (var i = 0; i < _built.Count; i++)
            {
                if (_built[i] == null)
                    continue;
                if (Application.isPlaying)
                    Destroy(_built[i]);
                else
                    DestroyImmediate(_built[i]);
            }

            _built.Clear();
        }

        void EnsureRoot()
        {
            if (mapRoot != null)
                return;
            var go = new GameObject("DemoTileMap");
            go.transform.SetParent(transform, false);
            mapRoot = go.transform;
        }

        void PlaceTile(string prefabPath, float x, float y, string name)
        {
            GameObject go = null;
#if UNITY_EDITOR
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab != null)
            {
                go = Application.isPlaying
                    ? (GameObject)PrefabUtility.InstantiatePrefab(prefab)
                    : (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            }
#endif
            if (go == null)
            {
                go = new GameObject(name);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = HostSpriteFactory.TileSprite();
                sr.color = ColorForPath(prefabPath);
                sr.sortingOrder = -30;
            }

            go.name = name;
            go.transform.SetParent(mapRoot, false);
            go.transform.position = HostPresentationSpace.FromPresentation(x, y, HostPresentationSpace.GroundZ);
            // Strip Demo Runtime behaviours if prefab carried any.
            var behaviours = go.GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
            {
                var mb = behaviours[i];
                if (mb == null)
                    continue;
                var ns = mb.GetType().Namespace ?? string.Empty;
                if (ns.StartsWith("XianXia.Unity.Host", System.StringComparison.Ordinal))
                    continue;
                DestroyComponent(mb);
            }

            _built.Add(go);
        }

        static void DestroyComponent(Object comp)
        {
            if (comp == null)
                return;
            if (Application.isPlaying)
                Destroy(comp);
            else
                DestroyImmediate(comp);
        }

        static string ChooseGroundPrefab(int x, int y)
        {
            if (x >= 24 && y <= -10)
                return "Assets/Prefabs/Environment/Tiles/SpiritSiteTile.prefab";
            if (x <= -28)
                return "Assets/Prefabs/Environment/Tiles/ForestFloorTile.prefab";
            if (x >= -10 && x <= 3 && y >= -20 && y <= -11)
                return "Assets/Prefabs/Environment/Tiles/HerbPatchTile.prefab";
            if (x >= 8 && x <= 32 && y >= -20 && y <= -4)
                return "Assets/Prefabs/Environment/Tiles/FarmlandTile.prefab";
            var horizontalRoad = Mathf.Abs(y) <= 1 || (y >= 8 && y <= 9 && x >= -20 && x <= 22);
            var verticalRoad = Mathf.Abs(x) <= 1
                || (x >= 16 && x <= 17 && y >= -16 && y <= 12)
                || (x >= -14 && x <= -13 && y >= -2 && y <= 14);
            if (horizontalRoad || verticalRoad)
                return "Assets/Prefabs/Environment/Tiles/DirtRoadTile.prefab";
            return "Assets/Prefabs/Environment/Tiles/GrassTile.prefab";
        }

        static Color ColorForPath(string path)
        {
            if (path.Contains("Spirit")) return new Color(0.35f, 0.7f, 0.85f);
            if (path.Contains("Forest")) return new Color(0.25f, 0.48f, 0.28f);
            if (path.Contains("Herb")) return new Color(0.35f, 0.65f, 0.40f);
            if (path.Contains("Farm")) return new Color(0.70f, 0.62f, 0.30f);
            if (path.Contains("Dirt")) return new Color(0.55f, 0.45f, 0.32f);
            return new Color(0.30f, 0.42f, 0.26f);
        }
    }
}
