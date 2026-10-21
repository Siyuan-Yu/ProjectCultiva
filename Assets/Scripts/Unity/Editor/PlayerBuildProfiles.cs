using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using XianXia.Unity.Content;
using XianXia.Unity.Host;

namespace XianXia.Unity.EditorTools
{
    public static class PlayerBuildProfiles
    {
        public const string FormalScene = "Assets/Scenes/LevelTester.unity";
        public static readonly string[] DemoScenes =
            { "Assets/Scenes/Demo_v0_1.unity", "Assets/Scenes/DemoParityHost.unity" };
        public static string ActiveFlavor { get; private set; } = "Formal";
        static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        [MenuItem("XianXia/Build/Formal Windows x64")]
        public static void BuildFormalWindows() => Build("Formal");

        [MenuItem("XianXia/Build/Historical Demo Windows x64")]
        public static void BuildDemoWindows() => Build("Demo");

        static void Build(string flavor)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new BuildFailedException("请先退出 Play Mode 再构建。");
            for (var index = 0; index < SceneManager.sceneCount; index++)
                if (SceneManager.GetSceneAt(index).isDirty)
                    throw new BuildFailedException("请先保存已修改的场景，再构建；不会自动保存当前编辑。");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                throw new BuildFailedException("当前 Unity 未安装 Windows x64 构建支持。");
            var previousProduct = PlayerSettings.productName;
            var previousFlavor = ActiveFlavor;
            ActiveFlavor = flavor;
            try
            {
                ValidatePackage();
                PlayerSettings.productName = flavor == "Formal" ? "PJCultiva" : "PJCultiva Demo";
                var folder = Path.Combine(ProjectRoot, "Builds", flavor,
                    DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6));
                Directory.CreateDirectory(folder);
                var executable = flavor == "Formal" ? "PJCultiva.exe" : "PJCultivaDemo.exe";
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = flavor == "Formal" ? new[] { FormalScene } : DemoScenes,
                    locationPathName = Path.Combine(folder, executable),
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None,
                    extraScriptingDefines = flavor == "Demo" ? new[] { "PJCULTIVA_DEMO_BUILD" } : Array.Empty<string>()
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new BuildFailedException("构建失败：" + report.summary.result);
                Debug.Log("[PlayerBuild] " + flavor + " complete: " + folder);
                if (!Application.isBatchMode) EditorUtility.RevealInFinder(folder);
            }
            finally
            {
                PlayerSettings.productName = previousProduct;
                ActiveFlavor = previousFlavor;
            }
        }

        [Serializable]
        sealed class PackageManifest
        {
            public string modId;
            public string version;
            public string[] contentFolders;
        }

        [Serializable]
        internal sealed class FormalOpeningSelection
        {
            public string openingScenarioId;
            public string characterRosterId;
            public string preferredMapLayoutId;
        }

        internal static FormalOpeningSelection ReadFormalOpening(string packageRoot)
        {
            var path = Path.Combine(packageRoot, "formal-opening.json");
            if (!File.Exists(path)) throw new BuildFailedException("缺少正式开局绑定：" + path);
            var selection = JsonUtility.FromJson<FormalOpeningSelection>(File.ReadAllText(path));
            if (selection == null) throw new BuildFailedException("正式开局绑定无效：空配置");
            if (!ContentPackageValidationFacade.TryValidateOpeningSelection(packageRoot,
                    selection.openingScenarioId, selection.characterRosterId, selection.preferredMapLayoutId, out var message))
                throw new BuildFailedException("正式开局绑定无效：" + message);
            return selection;
        }

        internal static void ValidatePackage()
        {
            var root = Path.Combine(ProjectRoot, "Content", "BaseGame");
            var path = Path.Combine(root, "manifest.json");
            if (!File.Exists(path) || !Directory.Exists(Path.Combine(root, "Data")))
                throw new BuildFailedException("正式 BaseGame 缺少 manifest.json 或 Data。");
            var manifest = JsonUtility.FromJson<PackageManifest>(File.ReadAllText(path));
            if (manifest == null || manifest.modId != "base" || string.IsNullOrEmpty(manifest.version) ||
                manifest.contentFolders == null || manifest.contentFolders.Length != 1 || manifest.contentFolders[0] != "Data")
                throw new BuildFailedException("当前 BaseGame 构建仅接收 manifest 指定的 Data；不打包作者源或备份。");
            if (ActiveFlavor == "Formal")
            {
                if (!ContentPackageValidationFacade.TryValidateDirectory(root, out var validationMessage))
                    throw new BuildFailedException("BaseGame 内容加载或引用校验失败：" + validationMessage);
                ReadFormalOpening(root);
            }
            var appearanceErrors = HostStepAArt.ValidateAppearanceMappings();
            if (appearanceErrors.Count > 0)
                throw new BuildFailedException("外观映射无效：" + string.Join("; ", appearanceErrors));
            if (AssetDatabase.LoadAssetAtPath<MapLayoutPrefabCatalog>("Assets/Resources/MapLayoutPrefabCatalog.asset") == null)
                throw new BuildFailedException("缺少正式 Resources Prefab 引用表。");
            var missing = MapLayoutPrefabResolver.ValidateCatalogPrefabs();
            if (missing.Count > 0) throw new BuildFailedException("地图 Prefab 缺失：" + string.Join("; ", missing));
            foreach (var kind in MapKindCatalog.All)
                if (!MapLayoutPrefabResolver.TryLoadRuntimePrefab(kind.PrefabPath, out _))
                    throw new BuildFailedException("Player Prefab 引用表缺少：" + kind.PrefabPath);
        }

        [Serializable]
        sealed class PayloadRecord { public string path; public string sha256; }
        [Serializable]
        sealed class BuildIdentity
        {
            public string flavor;
            public string unityVersion;
            public string contentVersion;
            public string builtUtc;
            public string[] scenes;
            public PayloadRecord[] contentFiles;
        }

        internal static void CopyContent(BuildReport report)
        {
            var source = Path.Combine(ProjectRoot, "Content", "BaseGame");
            var playerRoot = Path.GetDirectoryName(report.summary.outputPath);
            var dataName = Path.GetFileNameWithoutExtension(report.summary.outputPath) + "_Data";
            var destination = Path.Combine(playerRoot, dataName, "StreamingAssets", "PJCultiva", ActiveFlavor, "BaseGame");
            if (Directory.Exists(destination))
                throw new BuildFailedException("内容目标已经存在，请使用新的构建目录，旧包不会被覆盖：" + destination);
            Directory.CreateDirectory(destination);
            var records = new List<PayloadRecord>();
            var files = new[] { Path.Combine(source, "manifest.json") }.Concat(
                Directory.GetFiles(Path.Combine(source, "Data"), "*.json", SearchOption.AllDirectories)).OrderBy(p => p, StringComparer.Ordinal);
            foreach (var file in files)
            {
                var relative = file.Substring(source.Length + 1);
                var output = Path.Combine(destination, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                File.Copy(file, output, false);
                using (var sha = SHA256.Create())
                using (var stream = File.OpenRead(output))
                    records.Add(new PayloadRecord { path = relative.Replace('\\', '/'),
                        sha256 = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() });
            }
            var manifest = JsonUtility.FromJson<PackageManifest>(File.ReadAllText(Path.Combine(destination, "manifest.json")));
            var identity = new BuildIdentity
            {
                flavor = ActiveFlavor, unityVersion = Application.unityVersion, contentVersion = manifest.version,
                builtUtc = DateTime.UtcNow.ToString("O"), scenes = ActiveFlavor == "Formal" ? new[] { FormalScene } : DemoScenes,
                contentFiles = records.ToArray()
            };
            File.WriteAllText(Path.Combine(playerRoot, "build-identity.json"), JsonUtility.ToJson(identity, true));
        }

        internal static void ApplyFormalOpening(PlayableHostBootstrap host)
        {
            var selection = ReadFormalOpening(Path.Combine(ProjectRoot, "Content", "BaseGame"));
            var serialized = new SerializedObject(host);
            serialized.FindProperty("openingScenarioId").stringValue = selection.openingScenarioId;
            serialized.FindProperty("characterRosterId").stringValue = selection.characterRosterId ?? string.Empty;
            serialized.FindProperty("preferredMapLayoutId").stringValue = selection.preferredMapLayoutId ?? string.Empty;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    public sealed class PlayerBuildPackageHook : IPreprocessBuildWithReport, IPostprocessBuildWithReport, IProcessSceneWithReport
    {
        public int callbackOrder => 1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.StandaloneWindows64)
                throw new BuildFailedException("本轮内容随包路径仅验收 Windows x64；其他平台需独立适配。");
            PlayerBuildProfiles.ValidatePackage();
        }

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report == null) return;
            var formal = PlayerBuildProfiles.ActiveFlavor == "Formal";
            if (formal ? scene.path != PlayerBuildProfiles.FormalScene : !PlayerBuildProfiles.DemoScenes.Contains(scene.path))
                throw new BuildFailedException("构建场景与 " + PlayerBuildProfiles.ActiveFlavor + " 配置不一致：" + scene.path);
            if (!formal) return;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var host in root.GetComponentsInChildren<PlayableHostBootstrap>(true))
                {
                    var serialized = new SerializedObject(host);
                    serialized.FindProperty("contentPackageDirectoryOverride").stringValue = string.Empty;
                    serialized.FindProperty("mapLayoutFilePath").stringValue = string.Empty;
                    serialized.FindProperty("mapLayoutJsonOverride").objectReferenceValue = null;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    PlayerBuildProfiles.ApplyFormalOpening(host);
                }
                foreach (var component in root.GetComponentsInChildren<HostLevelTesterCheatPanel>(true))
                    UnityEngine.Object.DestroyImmediate(component);
                foreach (var component in root.GetComponentsInChildren<LevelTesterHud>(true))
                    UnityEngine.Object.DestroyImmediate(component);
            }
        }

        public void OnPostprocessBuild(BuildReport report) => PlayerBuildProfiles.CopyContent(report);
    }
}
