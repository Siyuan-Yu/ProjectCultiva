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
