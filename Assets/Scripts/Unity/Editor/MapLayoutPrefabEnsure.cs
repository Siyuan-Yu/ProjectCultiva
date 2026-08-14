#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using XianXia.Unity.Host;

namespace XianXia.Unity.Editor
{
    /// <summary>
    /// 确保 MapLayout 用到的 Wall／Rock／CommonHouse／树／矿石 prefab 存在（没有就从现有资源复制）。
    /// </summary>
    public static class MapLayoutPrefabEnsure
    {
        const string Tiles = "Assets/Prefabs/Environment/Tiles";
        const string Buildings = "Assets/Prefabs/Environment/Buildings";
        const string Props = "Assets/Prefabs/Environment/Props";

        [MenuItem("XianXia/Content/Ensure MapLayout Prefabs")]
        public static void EnsureAll()
        {
            EnsureDir(Props);

            EnsureClone(
                Tiles + "/DirtRoadTile.prefab",
                Tiles + "/WallTile.prefab",
                "WallTile",
                new Color(0.38f, 0.38f, 0.42f, 1f));
            EnsureClone(
                Tiles + "/DirtRoadTile.prefab",
                Tiles + "/RockTile.prefab",
                "RockTile",
                new Color(0.50f, 0.48f, 0.45f, 1f));

            EnsureClone(
                Tiles + "/ForestFloorTile.prefab",
                Props + "/TreeSmall.prefab",
                "TreeSmall",
                new Color(0.20f, 0.55f, 0.25f, 1f));
            EnsureClone(
                Tiles + "/ForestFloorTile.prefab",
                Props + "/TreeMid.prefab",
                "TreeMid",
                new Color(0.16f, 0.48f, 0.22f, 1f));
            EnsureClone(
                Tiles + "/ForestFloorTile.prefab",
                Props + "/TreeLarge.prefab",
                "TreeLarge",
                new Color(0.12f, 0.40f, 0.18f, 1f));
            EnsureClone(
                Tiles + "/RockTile.prefab",
                Props + "/OrePile.prefab",
                "OrePile",
                new Color(0.62f, 0.52f, 0.32f, 1f));
            EnsureClone(
                Tiles + "/SpiritSiteTile.prefab",
                Props + "/Cushion.prefab",
                "Cushion",
                new Color(0.55f, 0.42f, 0.72f, 1f));
            EnsureMissingPlaceholder();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MapLayoutPrefabEnsure] Wall/Rock/House/TreeS-M-L/OrePile/Cushion/MissingPrefab ready.");
        }

        [MenuItem("XianXia/Content/Validate MapLayout Prefabs")]
        public static void ValidateAll()
        {
            var missing = MapLayoutPrefabResolver.ValidateCatalogPrefabs();
            if (missing.Count == 0)
            {
                Debug.Log("[MapLayoutPrefabEnsure] All MapEditor kinds have prefabs.");
                return;
            }

            for (var i = 0; i < missing.Count; i++)
                Debug.LogWarning("[MapLayoutPrefabEnsure] Missing: " + missing[i]);
            Debug.LogWarning(
                "[MapLayoutPrefabEnsure] " + missing.Count +
                " prefab(s) missing. Run XianXia/Content/Ensure MapLayout Prefabs.");
        }

        static void EnsureMissingPlaceholder()
        {
            var prefabPath = Tiles + "/MissingPrefabPlaceholder.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
                return;

            EnsureDir(Tiles);
            var texPath = Tiles + "/MissingPrefabPlaceholder.png";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(texPath) == null)
            {
                var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                var fullTexPath = Path.Combine(projectRoot, texPath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(fullTexPath) ?? projectRoot);
                var tex = BuildMissingTexture(32, 32);
                File.WriteAllBytes(fullTexPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(texPath);
                var ti = AssetImporter.GetAtPath(texPath) as TextureImporter;
                if (ti != null)
                {
                    ti.textureType = TextureImporterType.Sprite;
                    ti.spriteImportMode = SpriteImportMode.Single;
                    ti.filterMode = FilterMode.Point;
                    ti.SaveAndReimport();
                }
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(texPath);
            var root = new GameObject("MissingPrefabPlaceholder");
            var sr = root.AddComponent<SpriteRenderer>();
            sr.sprite = sprite ?? HostSpriteFactory.MissingPrefabSprite();
            sr.sortingOrder = 100;
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
        }

        static Texture2D BuildMissingTexture(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var a = new Color(0.92f, 0.08f, 0.72f, 1f);
            var b = new Color(0.08f, 0.08f, 0.08f, 1f);
            var cell = Mathf.Max(4, w / 4);
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var checker = ((x / cell) + (y / cell)) % 2 == 0;
                tex.SetPixel(x, y, checker ? a : b);
            }

            tex.Apply(false, false);
            return tex;
        }

        [InitializeOnLoadMethod]
        static void AutoEnsure()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    return;
                if (AssetDatabase.LoadAssetAtPath<GameObject>(Tiles + "/WallTile.prefab") != null &&
                    AssetDatabase.LoadAssetAtPath<GameObject>(Buildings + "/CommonHouse.prefab") != null &&
                    AssetDatabase.LoadAssetAtPath<GameObject>(Props + "/TreeSmall.prefab") != null &&
                    AssetDatabase.LoadAssetAtPath<GameObject>(Props + "/OrePile.prefab") != null &&
                    AssetDatabase.LoadAssetAtPath<GameObject>(Props + "/Cushion.prefab") != null &&
                    AssetDatabase.LoadAssetAtPath<GameObject>(Tiles + "/MissingPrefabPlaceholder.prefab") != null)
                    return;
                EnsureAll();
            };
        }

        static void EnsureDir(string assetDir)
        {
            if (AssetDatabase.IsValidFolder(assetDir))
                return;
            var parent = Path.GetDirectoryName(assetDir)?.Replace('\\', '/');
            var name = Path.GetFileName(assetDir);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
                return;
            if (!AssetDatabase.IsValidFolder(parent))
                EnsureDir(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        static void EnsureClone(
            string sourcePath,
            string destPath,
            string newName,
            Color tint,
            float rootScale = 1f)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(destPath) != null)
                return;

            if (!AssetDatabase.CopyAsset(sourcePath, destPath))
            {
                Debug.LogWarning("[MapLayoutPrefabEnsure] Copy failed: " + sourcePath + " → " + destPath);
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(destPath);
            try
            {
                root.name = newName;
                if (rootScale > 0.01f)
                    root.transform.localScale = new Vector3(rootScale, rootScale, 1f);

                if (tint.a > 0.01f)
                {
                    var srs = root.GetComponentsInChildren<SpriteRenderer>(true);
                    for (var i = 0; i < srs.Length; i++)
                        srs[i].color = tint;
                }

                PrefabUtility.SaveAsPrefabAsset(root, destPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
#endif
