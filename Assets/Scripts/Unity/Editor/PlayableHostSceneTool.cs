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
            hostGo.AddComponent<HostContentDebugPanel>();
            hostGo.AddComponent<HostEventFeed>();
            hostGo.AddComponent<HostSnapshotPanel>();
            hostGo.AddComponent<HostMapGraybox>();
            hostGo.AddComponent<HostMoveController>();
            hostGo.AddComponent<HostActionMenu>();
            hostGo.AddComponent<HostFormalHud>();
            hostGo.AddComponent<HostActivityPresenter>();
            hostGo.AddComponent<HostCrowdPresenter>();
            hostGo.AddComponent<HostFeedbackOverlay>();

            var cam = Object.FindObjectOfType<Camera>();
            if (cam != null)
            {
                cam.orthographic = true;
                cam.orthographicSize = 12f;
                cam.transform.position = new Vector3(-4f, 2f, -10f);
                cam.transform.rotation = Quaternion.identity;
                var rig = hostGo.GetComponent<PlayableHostCameraRig>();
                var so = new SerializedObject(rig);
                so.FindProperty("targetCamera").objectReferenceValue = cam;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

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
