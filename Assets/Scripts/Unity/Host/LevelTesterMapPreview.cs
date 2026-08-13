using System.IO;
using UnityEngine;
using XianXia.Data.Content;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Level Tester Inspector「Import」：编辑模式下把 mapLayout JSON 刷成 prefab 预览。
    /// </summary>
    public static class LevelTesterMapPreview
    {
        public static bool TryImport(PlayableHostBootstrap bootstrap, out string message)
        {
            message = string.Empty;
            if (bootstrap == null)
            {
                message = "PlayableHostBootstrap missing.";
                return false;
            }

            if (!TryLoadSelectedLayout(bootstrap, out var layout, out message))
                return false;

#if UNITY_EDITOR
            Undo.RegisterFullObjectHierarchyUndo(bootstrap.gameObject, "Import Map Preview");
#endif

            var tileMap = bootstrap.GetComponent<HostDemoTileMap>() ?? bootstrap.gameObject.AddComponent<HostDemoTileMap>();
            tileMap.RebuildFromLayout(layout);

            var graybox = bootstrap.GetComponent<HostMapGraybox>();
            if (graybox != null)
                graybox.Clear();

            FramePreview(bootstrap, layout);

            message =
                "已 Import：" + layout.Id + " " + layout.Width + "×" + layout.Height +
                "，物件 " + (layout.Placements?.Count ?? 0) + "，prefab " + tileMap.TileCount;
            if (tileMap.MissingPrefabCount > 0)
                message += "；缺失 prefab " + tileMap.MissingPrefabCount + " 处（洋红棋盘格占位）";
            Debug.Log("[LevelTester] " + message, bootstrap);
#if UNITY_EDITOR
            EditorUtility.SetDirty(bootstrap.gameObject);
            SceneView.RepaintAll();
#endif
            return true;
        }

        public static void ClearPreview(PlayableHostBootstrap bootstrap)
        {
            if (bootstrap == null)
                return;

#if UNITY_EDITOR
            Undo.RegisterFullObjectHierarchyUndo(bootstrap.gameObject, "Clear Map Preview");
#endif

            var tileMap = bootstrap.GetComponent<HostDemoTileMap>();
            if (tileMap != null)
                tileMap.Clear();

            var graybox = bootstrap.GetComponent<HostMapGraybox>();
            if (graybox != null)
                graybox.Clear();

#if UNITY_EDITOR
            EditorUtility.SetDirty(bootstrap.gameObject);
            SceneView.RepaintAll();
#endif
        }

        static bool TryLoadSelectedLayout(
            PlayableHostBootstrap bootstrap,
            out MapLayoutDefinition layout,
            out string message)
        {
            layout = null;
            message = string.Empty;

            var preferId = bootstrap.PreferredMapLayoutId;
            if (!string.IsNullOrWhiteSpace(bootstrap.MapLayoutFilePath))
            {
                var path = ResolveMapLayoutPath(bootstrap.MapLayoutFilePath.Trim());
                var loaded = MapLayoutJsonLoader.LoadFromFile(path, preferId);
                if (loaded.IsFailure)
                {
                    message = loaded.Error.ToString();
                    return false;
                }

                layout = loaded.Value;
                return true;
            }

            var textAsset = bootstrap.MapLayoutJsonOverride;
            if (textAsset != null && !string.IsNullOrWhiteSpace(textAsset.text))
            {
                var loaded = MapLayoutJsonLoader.LoadFromText(textAsset.text, preferId, textAsset.name);
                if (loaded.IsFailure)
                {
                    message = loaded.Error.ToString();
                    return false;
                }

                layout = loaded.Value;
                return true;
            }

            message = "请先点「选择文件…」选一份关卡 mapLayout JSON。";
            return false;
        }

        static void FramePreview(PlayableHostBootstrap bootstrap, MapLayoutDefinition layout)
        {
            if (layout == null || layout.Width <= 0 || layout.Height <= 0)
                return;

            var cs = layout.CellSize > 0f ? layout.CellSize : 1f;
            var cx = layout.OriginX + layout.Width * cs * 0.5f;
            var cy = layout.OriginY + layout.Height * cs * 0.5f;
            var center = HostPresentationSpace.FromPresentation(cx, cy, HostPresentationSpace.GroundZ);

            var camRig = bootstrap.GetComponent<PlayableHostCameraRig>();
            if (camRig != null)
                camRig.FrameSlots(center);

#if UNITY_EDITOR
            var size = new Vector3(Mathf.Max(4f, layout.Width * cs), Mathf.Max(4f, layout.Height * cs), 8f);
            var bounds = new Bounds(center, size);
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.Frame(bounds, false);
#endif
        }

        static string ResolveMapLayoutPath(string raw)
        {
            if (Path.IsPathRooted(raw))
                return Path.GetFullPath(raw);
#if UNITY_EDITOR
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectRoot, raw.Replace('/', Path.DirectorySeparatorChar)));
#else
            return Path.GetFullPath(Path.Combine(Application.dataPath, raw));
#endif
        }
    }
}
