using System.Text;

namespace ContentAuthoring.Shared;

/// <summary>
/// MapEditor kind ↔ Host prefab 对照（与 Unity <c>MapKindCatalog</c> 对齐）。
/// 保存 mapLayout 时校验：未知 kind，或对应 prefab 文件不在工程里 → 报错。
/// </summary>
public static class MapKindPrefabCatalog
{
    public enum KindMode
    {
        Placeable = 0,
        ZoneOverlay = 1
    }

    public sealed record Entry(string Kind, string? PrefabAssetsPath, KindMode Mode);

    static readonly Entry[] All =
    {
        new("zoneHerb", "Assets/Prefabs/Environment/Tiles/HerbPatchTile.prefab", KindMode.ZoneOverlay),
        new("zoneGrain", "Assets/Prefabs/Environment/Tiles/FarmlandTile.prefab", KindMode.ZoneOverlay),
        new("zoneHousing", "Assets/Prefabs/Environment/Buildings/CommonHouse.prefab", KindMode.ZoneOverlay),
        new("zoneForest", "Assets/Prefabs/Environment/Tiles/ForestFloorTile.prefab", KindMode.ZoneOverlay),
        new("zoneMine", "Assets/Prefabs/Environment/Tiles/RockTile.prefab", KindMode.ZoneOverlay),
        new("zoneSpring", "Assets/Prefabs/Environment/Tiles/SpiritSiteTile.prefab", KindMode.ZoneOverlay),
        new("spawnZone", "Assets/Prefabs/Environment/Tiles/SpiritSiteTile.prefab", KindMode.ZoneOverlay),
        new("forest", "Assets/Prefabs/Environment/Tiles/ForestFloorTile.prefab", KindMode.ZoneOverlay),
        new("spring", "Assets/Prefabs/Environment/Tiles/SpiritSiteTile.prefab", KindMode.ZoneOverlay),

        new("mine", "Assets/Prefabs/Environment/Props/OrePile.prefab", KindMode.Placeable),
        new("herbField", "Assets/Prefabs/Environment/Tiles/HerbPatchTile.prefab", KindMode.Placeable),
        new("grainField", "Assets/Prefabs/Environment/Tiles/FarmlandTile.prefab", KindMode.Placeable),
        new("road", "Assets/Prefabs/Environment/Tiles/DirtRoadTile.prefab", KindMode.Placeable),
        new("wall", "Assets/Prefabs/Environment/Tiles/WallTile.prefab", KindMode.Placeable),
        new("treeS", "Assets/Prefabs/Environment/Props/TreeSmall.prefab", KindMode.Placeable),
        new("treeM", "Assets/Prefabs/Environment/Props/TreeMid.prefab", KindMode.Placeable),
        new("treeL", "Assets/Prefabs/Environment/Props/TreeLarge.prefab", KindMode.Placeable),
        new("ore", "Assets/Prefabs/Environment/Props/OrePile.prefab", KindMode.Placeable),
        new("cushion", "Assets/Prefabs/Environment/Props/Cushion.prefab", KindMode.Placeable),
        new("rock", "Assets/Prefabs/Environment/Tiles/RockTile.prefab", KindMode.Placeable),
        new("cave", "Assets/Prefabs/Environment/Buildings/Warehouse.prefab", KindMode.Placeable),
        new("loot", "Assets/Prefabs/Environment/Props/Cushion.prefab", KindMode.Placeable),
        new("controlCore", "Assets/Prefabs/Environment/Buildings/SupervisorHouse.prefab", KindMode.Placeable),
        new("rallyPoint", "Assets/Prefabs/Environment/Props/Cushion.prefab", KindMode.Placeable),
    };

    public static string NormalizeKind(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
            return "";
        if (string.Equals(kind, "roadHub", StringComparison.OrdinalIgnoreCase))
            return "controlCore";
        if (string.Equals(kind, "house", StringComparison.OrdinalIgnoreCase))
            return "zoneHousing";
        return kind.Trim();
    }

    public static bool TryGet(string? kind, out Entry entry)
    {
        entry = null!;
        var key = NormalizeKind(kind);
        if (string.IsNullOrEmpty(key))
            return false;
        for (var i = 0; i < All.Length; i++)
        {
            if (!string.Equals(All[i].Kind, key, StringComparison.Ordinal))
                continue;
            entry = All[i];
            return true;
        }

        return false;
    }

    /// <summary>
    /// 校验 placements：未知 kind，或 Placeable 对应 prefab 文件缺失 → 失败。
    /// </summary>
    public static bool TryValidatePlacements(
        IEnumerable<(string Id, string Kind)> placements,
        string? packageRoot,
        out string error)
    {
        error = "";
        var unityRoot = FindUnityProjectRoot(packageRoot);
        var unknown = new List<string>();
        var missingPrefab = new List<string>();
        var seenKind = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (id, rawKind) in placements)
        {
            var kind = NormalizeKind(rawKind);
            if (string.IsNullOrEmpty(kind))
            {
                unknown.Add($"{id}（kind 为空）");
                continue;
            }

            if (!TryGet(kind, out var entry))
            {
                unknown.Add($"{id} → kind「{kind}」");
                continue;
            }

            if (entry.Mode == KindMode.ZoneOverlay)
                continue;
            if (string.IsNullOrEmpty(entry.PrefabAssetsPath))
                continue;
            if (!seenKind.Add(entry.Kind))
                continue;
            if (string.IsNullOrEmpty(unityRoot))
                continue;

            var full = Path.GetFullPath(Path.Combine(unityRoot, entry.PrefabAssetsPath.Replace('/', Path.DirectorySeparatorChar)));
            if (!File.Exists(full))
                missingPrefab.Add($"{entry.Kind} → {entry.PrefabAssetsPath}");
        }

        if (unknown.Count == 0 && missingPrefab.Count == 0)
            return true;

        var sb = new StringBuilder();
        if (unknown.Count > 0)
        {
            sb.AppendLine("地图里有 Host／prefab 目录未登记的 kind（保存已取消）：");
            foreach (var u in unknown.Take(12))
                sb.AppendLine(" · " + u);
            if (unknown.Count > 12)
                sb.AppendLine($" · …共 {unknown.Count} 处");
        }

        if (missingPrefab.Count > 0)
        {
            if (sb.Length > 0)
                sb.AppendLine();
            sb.AppendLine("下列 kind 已登记，但工程里找不到对应 prefab：");
            foreach (var m in missingPrefab)
                sb.AppendLine(" · " + m);
            sb.AppendLine("请在 Unity 菜单 XianXia/Content/Ensure MapLayout Prefabs 生成，或补资源。");
        }

        if (string.IsNullOrEmpty(unityRoot) && unknown.Count == 0)
        {
            error = "无法定位 Unity 工程根目录，已跳过 prefab 文件检查（kind 目录校验通过）。";
            return true;
        }

        error = sb.ToString().TrimEnd();
        return false;
    }

    /// <summary>Content/BaseGame → 上两级为 Unity 工程根（含 Assets/）。</summary>
    public static string? FindUnityProjectRoot(string? packageRoot)
    {
        var root = packageRoot;
        if (string.IsNullOrEmpty(root))
            root = PackagePaths.FindDefaultBaseGame();
        if (string.IsNullOrEmpty(root))
            return null;

        var dir = new DirectoryInfo(root);
        for (var i = 0; i < 6 && dir != null; i++, dir = dir.Parent)
        {
            var assets = Path.Combine(dir.FullName, "Assets");
            if (Directory.Exists(assets))
                return dir.FullName;
        }

        return null;
    }
}
