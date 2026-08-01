#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using XianXia.Unity.Host;

namespace XianXia.Unity.EditorTools
{
    /// <summary>Creates／updates the VS0.4 Playable Host scene (Editor-only).</summary>
    public static class PlayableHostSceneTool
    {
        public const string ScenePath = "Assets/Scenes/PlayableHost.unity";

        [MenuItem("XianXia/VS0.4/Create Or Update Playable Host Scene")]
        public static void CreateOrUpdateScene()
        {
            EnsureSceneFolder();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var hostGo = new GameObject("PlayableHost");
            hostGo.AddComponent<PlayableHostBootstrap>();
            hostGo.AddComponent<EntityViewSpawner>();
            hostGo.AddComponent<PlayableHostCameraRig>();
            hostGo.AddComponent<HostSelectionController>();
            hostGo.AddComponent<HostCommandBridge>();
            hostGo.AddComponent<HostDebugHud>();
            hostGo.AddComponent<HostEventFeed>();
            hostGo.AddComponent<HostSnapshotPanel>();

            var cam = Object.FindObjectOfType<Camera>();
            if (cam != null)
            {
                cam.transform.position = new Vector3(0f, 8f, -12f);
                cam.transform.rotation = Quaternion.Euler(30f, 0f, 0f);
                var rig = hostGo.GetComponent<PlayableHostCameraRig>();
                var so = new SerializedObject(rig);
                so.FindProperty("targetCamera").objectReferenceValue = cam;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // Simple ground plane (presentation only).
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(2f, 1f, 2f);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            Debug.Log("[PlayableHost] Scene saved: " + ScenePath);
        }

        /// <summary>Batchmode entry: Unity -executeMethod XianXia.Unity.EditorTools.PlayableHostSceneTool.CreateOrUpdateSceneBatch</summary>
        public static void CreateOrUpdateSceneBatch()
        {
            CreateOrUpdateScene();
            EditorApplication.Exit(0);
        }

        static void EnsureSceneFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");
        }
    }
}
#endif
