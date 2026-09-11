using System.IO;
using UnityEditor;
using UnityEngine;
using XianXia.Unity.Host;

namespace XianXia.Unity.EditorTools
{
    public static class W2ASurfaceGeographyBakeMenu
    {
        public const string MenuPath = "XianXia/Content/Bake W2A Surface Geography";

        [MenuItem(MenuPath)]
        public static void Bake()
        {
            var root = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(root)) throw new DirectoryNotFoundException("Unity project root unavailable.");
            var source = Path.Combine(root, "ContentAuthoring", "Worlds", "w2a_surface_geography_source_v1.json");
            var surface = Path.Combine(root, "Content", "BaseGame", "Data", "Worlds", "main_wilderness_surface_v1.json");
            var output = Path.Combine(root, "Content", "BaseGame", "Data", "Worlds", "w2a_surface_geography_baked_v1.json");
            W2ASurfaceGeographyBakeBridge.Bake(source, surface, output);
            AssetDatabase.Refresh();
            Debug.Log("[W2A Bake] source=" + source + " output=" + output);
        }
    }
}
