#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using XianXia.Unity.Host;

namespace XianXia.Unity.EditorTools
{
    /// <summary>
    /// Dedicated Demo-parity sample level scene (play HUD only).
    /// Keep <see cref="PlayableHostSceneTool"/> for VS／framework testing.
    /// </summary>
    public static class DemoParityHostSceneTool
    {
        public const string ScenePath = "Assets/Scenes/DemoParityHost.unity";

        [MenuItem("XianXia/Demo Parity/Create Or Update Sample Level Scene")]
        public static void CreateOrUpdateScene()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var hostGo = new GameObject("DemoParityHost");
            var bootstrap = hostGo.AddComponent<PlayableHostBootstrap>();
            hostGo.AddComponent<EntityViewSpawner>();
            hostGo.AddComponent<PlayableHostCameraRig>();
            hostGo.AddComponent<HostSelectionController>();
            hostGo.AddComponent<HostCommandBridge>();
            hostGo.AddComponent<HostDebugHud>();
            hostGo.AddComponent<HostEventFeed>();
            hostGo.AddComponent<HostSnapshotPanel>();
            hostGo.AddComponent<HostMapGraybox>();
            hostGo.AddComponent<HostDemoTileMap>();
            hostGo.AddComponent<HostMoveController>();
            hostGo.AddComponent<HostFormalHud>();
            hostGo.AddComponent<HostActivityPresenter>();
            hostGo.AddComponent<HostCrowdPresenter>();
            hostGo.AddComponent<HostFeedbackOverlay>();
            hostGo.AddComponent<HostWorkTargetMode>();
            hostGo.AddComponent<HostContentInterruptPresenter>();
            hostGo.AddComponent<HostDialoguePresenter>();
            hostGo.AddComponent<HostDialogueUguiView>();

            var bootstrapSo = new SerializedObject(bootstrap);
            bootstrapSo.FindProperty("openingScenarioId").stringValue = "base:scenario_ch01_reference";
            bootstrapSo.FindProperty("secondsPerAutoTickAt1x").floatValue = 1f;
            bootstrapSo.ApplyModifiedPropertiesWithoutUndo();

            var bridgeSo = new SerializedObject(hostGo.GetComponent<HostCommandBridge>());
            bridgeSo.FindProperty("showDebugButtons").boolValue = false;
            bridgeSo.ApplyModifiedPropertiesWithoutUndo();

            var snapSo = new SerializedObject(hostGo.GetComponent<HostSnapshotPanel>());
            snapSo.FindProperty("showButtons").boolValue = false;
            snapSo.ApplyModifiedPropertiesWithoutUndo();

            var debugSo = new SerializedObject(hostGo.GetComponent<HostDebugHud>());
            debugSo.FindProperty("visible").boolValue = false;
            debugSo.ApplyModifiedPropertiesWithoutUndo();

            var feedSo = new SerializedObject(hostGo.GetComponent<HostEventFeed>());
            feedSo.FindProperty("visible").boolValue = false;
            feedSo.ApplyModifiedPropertiesWithoutUndo();

            var cam = Object.FindObjectOfType<Camera>();
            if (cam != null)
            {
                cam.orthographic = true;
                cam.orthographicSize = 12f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.18f, 0.22f, 0.16f, 1f);
                cam.transform.position = new Vector3(0f, 0f, -10f);
                cam.transform.rotation = Quaternion.identity;
                var rigSo = new SerializedObject(hostGo.GetComponent<PlayableHostCameraRig>());
                rigSo.FindProperty("targetCamera").objectReferenceValue = cam;
                rigSo.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == ScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }

            AssetDatabase.Refresh();
            Debug.Log("[DemoParityHost] Sample level scene saved: " + ScenePath);
        }

        [MenuItem("XianXia/Demo Parity/Create Or Update Sample Level Scene (Batch)")]
        public static void CreateOrUpdateSceneBatch()
        {
            CreateOrUpdateScene();
            EditorApplication.Exit(0);
        }
    }
}
#endif
