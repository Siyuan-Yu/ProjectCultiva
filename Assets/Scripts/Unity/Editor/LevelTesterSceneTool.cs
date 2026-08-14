#if UNITY_EDITOR
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using XianXia.Unity.Host;

namespace XianXia.Unity.EditorTools
{
    /// <summary>
    /// 创建／更新 Level Tester 场景：逻辑关卡试玩台（选一份 mapLayout JSON）。
    /// </summary>
    public static class LevelTesterSceneTool
    {
        public const string ScenePath = "Assets/Scenes/LevelTester.unity";
        public const string DefaultMapLayoutPath = "Content/BaseGame/Data/Maps/ch01_reference_map.json";

        [MenuItem("XianXia/Level Tester/Create Or Update Level Tester Scene")]
        public static void CreateOrUpdateScene()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");
            if (!AssetDatabase.IsValidFolder("Assets/LevelTester"))
                AssetDatabase.CreateFolder("Assets", "LevelTester");
            if (!AssetDatabase.IsValidFolder("Assets/LevelTester/Maps"))
                AssetDatabase.CreateFolder("Assets/LevelTester", "Maps");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var hostGo = new GameObject("LevelTester");
            var bootstrap = hostGo.AddComponent<PlayableHostBootstrap>();
            hostGo.AddComponent<EntityViewSpawner>();
            hostGo.AddComponent<PlayableHostCameraRig>();
            hostGo.AddComponent<HostSelectionController>();
            hostGo.AddComponent<HostCommandBridge>();
            hostGo.AddComponent<HostDebugHud>();
            hostGo.AddComponent<HostContentDebugPanel>();
            hostGo.AddComponent<HostEventFeed>();
            hostGo.AddComponent<HostSnapshotPanel>();
            hostGo.AddComponent<HostMapGraybox>();
            hostGo.AddComponent<HostDemoTileMap>();
            hostGo.AddComponent<HostMoveController>();
            hostGo.AddComponent<HostActionMenu>();
            hostGo.AddComponent<HostFormalHud>();
            hostGo.AddComponent<HostActivityPresenter>();
            hostGo.AddComponent<HostCrowdPresenter>();
            hostGo.AddComponent<HostFeedbackOverlay>();
            hostGo.AddComponent<HostWorkTargetMode>();
            hostGo.AddComponent<HostContentInterruptPresenter>();
            hostGo.AddComponent<HostDialoguePresenter>();
            hostGo.AddComponent<HostDialogueUguiView>();
            hostGo.AddComponent<HostQuestJournal>();
            hostGo.AddComponent<HostInteractSpotPresenter>();
            hostGo.AddComponent<HostNpcScheduleMover>();
            hostGo.AddComponent<HostNpcContextMenu>();
            hostGo.AddComponent<LevelTesterHud>();

            var bootstrapSo = new SerializedObject(bootstrap);
            bootstrapSo.FindProperty("openingScenarioId").stringValue = "base:scenario_ch01_reference";
            bootstrapSo.FindProperty("preferredMapLayoutId").stringValue = "base:map_ch01_reference";
            bootstrapSo.FindProperty("mapLayoutFilePath").stringValue = DefaultMapLayoutPath;
            bootstrapSo.FindProperty("secondsPerAutoTickAt1x").floatValue = 1f;
            bootstrapSo.FindProperty("rebuildKey").intValue = (int)KeyCode.F12;
            bootstrapSo.ApplyModifiedPropertiesWithoutUndo();

            var interruptSo = new SerializedObject(hostGo.GetComponent<HostContentInterruptPresenter>());
            if (interruptSo.FindProperty("enableContentEventPopups") != null)
            {
                interruptSo.FindProperty("enableContentEventPopups").boolValue = true;
                interruptSo.ApplyModifiedPropertiesWithoutUndo();
            }

            var bridgeSo = new SerializedObject(hostGo.GetComponent<HostCommandBridge>());
            if (bridgeSo.FindProperty("showDebugButtons") != null)
            {
                bridgeSo.FindProperty("showDebugButtons").boolValue = true;
                bridgeSo.ApplyModifiedPropertiesWithoutUndo();
            }

            var snapSo = new SerializedObject(hostGo.GetComponent<HostSnapshotPanel>());
            if (snapSo.FindProperty("showButtons") != null)
            {
                snapSo.FindProperty("showButtons").boolValue = true;
                snapSo.ApplyModifiedPropertiesWithoutUndo();
            }

            var debugSo = new SerializedObject(hostGo.GetComponent<HostDebugHud>());
            if (debugSo.FindProperty("visible") != null)
            {
                debugSo.FindProperty("visible").boolValue = true;
                debugSo.ApplyModifiedPropertiesWithoutUndo();
            }

            var feedSo = new SerializedObject(hostGo.GetComponent<HostEventFeed>());
            if (feedSo.FindProperty("visible") != null)
            {
                feedSo.FindProperty("visible").boolValue = true;
                feedSo.ApplyModifiedPropertiesWithoutUndo();
            }

            var cam = Object.FindObjectOfType<Camera>();
            if (cam != null)
            {
                cam.orthographic = true;
                cam.orthographicSize = 18f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.14f, 0.18f, 0.14f, 1f);
                cam.transform.position = new Vector3(0f, 0f, -10f);
                cam.transform.rotation = Quaternion.identity;
                var rigSo = new SerializedObject(hostGo.GetComponent<PlayableHostCameraRig>());
                rigSo.FindProperty("targetCamera").objectReferenceValue = cam;
                if (rigSo.FindProperty("maxSize") != null)
                    rigSo.FindProperty("maxSize").floatValue = 48f;
                if (rigSo.FindProperty("orthographicSize") != null)
                    rigSo.FindProperty("orthographicSize").floatValue = 18f;
                rigSo.ApplyModifiedPropertiesWithoutUndo();
            }

            EnsureMapsReadme();
            EditorSceneManager.SaveScene(scene, ScenePath);

            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == ScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }

            AssetDatabase.Refresh();
            Debug.Log("[LevelTester] Scene saved: " + ScenePath);
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("XianXia/Level Tester/Create Or Update Level Tester Scene (Batch)")]
        public static void CreateOrUpdateSceneBatch()
        {
            CreateOrUpdateScene();
            EditorApplication.Exit(0);
        }

        static void EnsureMapsReadme()
        {
            var path = Path.Combine(Application.dataPath, "LevelTester", "Maps", "README.txt");
            var text =
                "Level Tester 日常用法：\n" +
                "1. 地图 JSON 真源：Content/BaseGame/Data/Maps/\n" +
                "2. 选中 LevelTester → Inspector 点「选择文件…」选该 JSON\n" +
                "详见 docs/40-process/114-level-tester.md\n";
            File.WriteAllText(path, text);
        }
    }

    [CustomEditor(typeof(PlayableHostBootstrap))]
    public sealed class PlayableHostBootstrapEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var bootstrap = (PlayableHostBootstrap)target;

            EditorGUILayout.LabelField("Level Tester · 关卡（只选一个地图 JSON）", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "关卡 JSON 真源：Content/BaseGame/Data/Maps/\n" +
                "点「选择文件…」选其中一份 mapLayout JSON。地图 Id 自动读取。\n" +
                "「开局剧本 Id」是任务／出生配置，不是地图。",
                MessageType.Info);

            var pathProp = serializedObject.FindProperty("mapLayoutFilePath");
            var idProp = serializedObject.FindProperty("preferredMapLayoutId");
            var scenarioProp = serializedObject.FindProperty("openingScenarioId");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("关卡地图 JSON");
            if (GUILayout.Button("选择文件…", GUILayout.Width(100)))
                BrowseMapJson(pathProp, idProp);
            EditorGUILayout.EndHorizontal();

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField(
                    "当前文件",
                    string.IsNullOrEmpty(pathProp.stringValue) ? "(未选)" : pathProp.stringValue);
                EditorGUILayout.TextField(
                    "地图 Id（自动）",
                    string.IsNullOrEmpty(idProp.stringValue) ? "(选文件后自动填)" : idProp.stringValue);
            }

            EditorGUILayout.PropertyField(
                scenarioProp,
                new GUIContent("开局剧本 Id", "例如 base:scenario_ch01_reference"));

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Map Preview", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Import", GUILayout.Height(24)))
                {
                    serializedObject.ApplyModifiedProperties();
                    if (LevelTesterMapPreview.TryImport(bootstrap, out var okMsg))
                        EditorUtility.DisplayDialog("Import 完成", okMsg, "确定");
                    else
                        EditorUtility.DisplayDialog("Import 失败", okMsg, "确定");
                }

                if (GUILayout.Button("Clear Preview", GUILayout.Height(24), GUILayout.Width(110)))
                {
                    LevelTesterMapPreview.ClearPreview(bootstrap);
                }

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.HelpBox(
                    "选完关卡 JSON 后点 Import，在 Scene 里用游戏 prefab 预览大致布局（无需 Play）。",
                    MessageType.None);
            }

            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Play 模式下请用 F12 重载；Import 仅用于编辑模式预览。", MessageType.Info);
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("用默认第一章地图"))
            {
                pathProp.stringValue = LevelTesterSceneTool.DefaultMapLayoutPath;
                idProp.stringValue = "base:map_ch01_reference";
                if (string.IsNullOrWhiteSpace(scenarioProp.stringValue))
                    scenarioProp.stringValue = "base:scenario_ch01_reference";
            }

            if (Application.isPlaying && GUILayout.Button("立即重载（F12）"))
            {
                serializedObject.ApplyModifiedProperties();
                bootstrap.TryInitialize();
                return;
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);
            DrawPropertiesExcluding(
                serializedObject,
                "m_Script",
                "mapLayoutFilePath",
                "preferredMapLayoutId",
                "mapLayoutJsonOverride",
                "openingScenarioId");

            serializedObject.ApplyModifiedProperties();
        }

        static void BrowseMapJson(SerializedProperty pathProp, SerializedProperty idProp)
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var start = Path.Combine(projectRoot, "Content", "BaseGame", "Data", "Maps");
            if (!Directory.Exists(start))
                Directory.CreateDirectory(start);
            if (!string.IsNullOrEmpty(pathProp.stringValue))
            {
                var cur = pathProp.stringValue;
                var fullCur = Path.IsPathRooted(cur)
                    ? cur
                    : Path.GetFullPath(Path.Combine(projectRoot, cur.Replace('/', Path.DirectorySeparatorChar)));
                var dir = Path.GetDirectoryName(fullCur);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    start = dir;
            }

            var picked = EditorUtility.OpenFilePanel("选择关卡地图 JSON（mapLayout）", start, "json");
            if (string.IsNullOrEmpty(picked))
                return;

            var full = Path.GetFullPath(picked);
            string rel;
            if (full.StartsWith(projectRoot, System.StringComparison.OrdinalIgnoreCase))
                rel = full.Substring(projectRoot.Length).TrimStart('\\', '/').Replace('\\', '/');
            else
                rel = full.Replace('\\', '/');

            pathProp.stringValue = rel;

            try
            {
                var text = File.ReadAllText(full);
                var id = TryReadMapLayoutId(text);
                if (!string.IsNullOrEmpty(id))
                    idProp.stringValue = id;
                else
                    Debug.LogWarning("[LevelTester] 已记下路径，但未能从 JSON 解析地图 id。");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[LevelTester] 已记下路径，读取 id 失败：" + ex.Message);
            }
        }

        /// <summary>Editor 程序集不引用 Data，仅用正则抽 mapLayout.id。</summary>
        static string TryReadMapLayoutId(string json)
        {
            if (string.IsNullOrEmpty(json))
                return null;
            var m = Regex.Match(
                json,
                "\"type\"\\s*:\\s*\"mapLayout\"[\\s\\S]{0,400}?\"id\"\\s*:\\s*\"([^\"]+)\"",
                RegexOptions.CultureInvariant);
            if (m.Success)
                return m.Groups[1].Value;
            m = Regex.Match(
                json,
                "\"id\"\\s*:\\s*\"([^\"]+)\"[\\s\\S]{0,200}?\"type\"\\s*:\\s*\"mapLayout\"",
                RegexOptions.CultureInvariant);
            return m.Success ? m.Groups[1].Value : null;
        }
    }
}
#endif
