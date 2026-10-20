using System.IO;
using UnityEngine;

namespace XianXia.Unity.Host
{
    public static class HostPlayerPaths
    {
#if PJCULTIVA_DEMO_BUILD
        public const string Flavor = "Demo";
#else
        public const string Flavor = "Formal";
#endif
        public static bool IsFormalPlayer => !Application.isEditor && Flavor == "Formal";
        public static bool DevelopmentToolsAllowed => Application.isEditor || Flavor == "Demo";
        public static string PackageDirectory => Path.Combine(Application.streamingAssetsPath,
            "PJCultiva", Flavor, "BaseGame");
        public static string SavePath => ResolveSavePath(Application.persistentDataPath, Application.isEditor);

        public static string ResolveSavePath(string persistentRoot, bool editor)
        {
            return editor ? Path.Combine(persistentRoot, HostLevelTesterSnapshotOps.DefaultFileName) :
                Path.Combine(persistentRoot, "Saves", Flavor, "slot0.json");
        }

        public static bool TryResolvePackageDirectory(out string path, out string error)
        {
            path = PackageDirectory;
            if (Directory.Exists(path) && File.Exists(Path.Combine(path, "manifest.json")) &&
                Directory.Exists(Path.Combine(path, "Data")))
            {
                error = string.Empty;
                return true;
            }
            error = "随包游戏内容缺失或不完整，请保留程序与完整数据文件夹后重试。Expected: " + path;
            return false;
        }
    }
}
