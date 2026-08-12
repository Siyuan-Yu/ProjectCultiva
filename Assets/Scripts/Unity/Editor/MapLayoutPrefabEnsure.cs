#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace XianXia.Unity.Editor
{
    /// <summary>
    /// 确保 MapLayout 用到的 Wall／Rock／SmallHouse prefab 存在（没有就从现有资源复制）。
    /// </summary>
    public static class MapLayoutPrefabEnsure
    {
        const string Tiles = "Assets/Prefabs/Environment/Tiles";
        const string Buildings = "Assets/Prefabs/Environment/Buildings";

        [MenuItem("XianXia/Content/Ensure MapLayout Prefabs")]
        public static void EnsureAll()
        {
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
                Buildings + "/CommonHouse.prefab",
                Buildings + "/SmallHouse.prefab",
                "SmallHouse",
                default,
                rootScale: 1f); // 运行时按 20 格缩放

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MapLayoutPrefabEnsure] WallTile / RockTile / SmallHouse ready.");
        }

        [InitializeOnLoadMethod]
        static void AutoEnsure()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    return;
                if (AssetDatabase.LoadAssetAtPath<GameObject>(Tiles + "/WallTile.prefab") != null &&
                    AssetDatabase.LoadAssetAtPath<GameObject>(Buildings + "/SmallHouse.prefab") != null)
                    return;
                EnsureAll();
            };
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

            if (!File.Exists(sourcePath) && !File.Exists(Path.GetFullPath(sourcePath)))
            {
                // Unity 路径
            }

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
