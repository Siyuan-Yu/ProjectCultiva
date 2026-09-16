using System.Text.Json.Nodes;

namespace SurfaceAuthoring.Core;

public sealed record CompatibilityPublishSummary(
    string CompositionId,
    int WidthCells,
    int HeightCells,
    int ChunkCount,
    int WaterCells,
    int RiverCount,
    int RoadCount,
    int BridgeCount,
    int WorldObjectCount,
    int HuangcunObjectCount,
    string MainRuntimePath,
    string GeographyRuntimePath,
    string WorldMapRuntimePath,
    string BackupRoot);

/// <summary>Stages the same bytes created by candidate export, validates them, then replaces both legacy runtime files together.</summary>
public static class CompatibilityPublisher
{
    public static CompatibilityPublishSummary Prepare(WorldCompositionDocument composition, LinkedSourceSet linked, string contentRoot)
    {
        EnsureReady(composition, linked);
        var binding = composition.LegacyMigration!;
        var main = RuntimePath(contentRoot, binding.MainSurfaceRelativePath);
        var geography = RuntimePath(contentRoot, binding.GeographyRelativePath);
        var worldMap = Path.Combine(contentRoot, "BaseGame", "Data", "Worlds", "main_world_surface_map_v1.json");
        var engine = new CompositionEngine(composition, linked);
        var water = 0;
        for (var y = 0; y < composition.SurfaceHeightCells; y++)
        for (var x = 0; x < composition.SurfaceWidthCells; x++)
            if (engine.Resolve(x, y).BaseTerrain == BaseTerrain.Water) water++;
        var huangcun = linked.Blueprints.TryGetValue("huangcun", out var blueprint) ? blueprint.ObjectPlacements.Count : 0;
        return new(composition.CompositionId, composition.SurfaceWidthCells, composition.SurfaceHeightCells,
            composition.SurfaceWidthCells / 50 * (composition.SurfaceHeightCells / 50), water, composition.Rivers.Count,
            composition.Roads.Count, composition.WorldObjectPlacements.Count(x => x.KindId == "bridge"),
            composition.WorldObjectPlacements.Count, huangcun, main, geography, worldMap,
            Path.Combine(contentRoot, "BaseGame", "_backups", "continuous-surface-authoring"));
    }

    public static CompatibilityPublishSummary Publish(WorldCompositionDocument composition, LinkedSourceSet linked, string contentRoot)
    {
        var summary = Prepare(composition, linked, contentRoot);
        var stage = Path.Combine(summary.BackupRoot, ".staging-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        try
        {
            var candidates = LegacyWorldMigration.ExportCompatibilityCandidates(composition, linked, contentRoot, stage);
            VerifyCandidate(candidates[0]); VerifyCandidate(candidates[1]); VerifyCandidate(candidates[2]);
            var original = Path.Combine(summary.BackupRoot, "original");
            var previous = Path.Combine(summary.BackupRoot, "previous");
            Directory.CreateDirectory(original); Directory.CreateDirectory(previous);
            BackupOnce(summary.MainRuntimePath, Path.Combine(original, Path.GetFileName(summary.MainRuntimePath)));
            BackupOnce(summary.GeographyRuntimePath, Path.Combine(original, Path.GetFileName(summary.GeographyRuntimePath)));
            if (File.Exists(summary.WorldMapRuntimePath)) BackupOnce(summary.WorldMapRuntimePath, Path.Combine(original, Path.GetFileName(summary.WorldMapRuntimePath)));
            File.Copy(summary.MainRuntimePath, Path.Combine(previous, Path.GetFileName(summary.MainRuntimePath)), true);
            File.Copy(summary.GeographyRuntimePath, Path.Combine(previous, Path.GetFileName(summary.GeographyRuntimePath)), true);
            if (File.Exists(summary.WorldMapRuntimePath)) File.Copy(summary.WorldMapRuntimePath, Path.Combine(previous, Path.GetFileName(summary.WorldMapRuntimePath)), true);
            ReplaceTriple(candidates[0], summary.MainRuntimePath, candidates[1], summary.GeographyRuntimePath, candidates[2], summary.WorldMapRuntimePath, stage);
            return summary;
        }
        finally { if (Directory.Exists(stage)) Directory.Delete(stage, true); }
    }

    public static void Restore(string contentRoot, LegacyMigrationBinding binding, bool original)
    {
        var main = RuntimePath(contentRoot, binding.MainSurfaceRelativePath);
        var geography = RuntimePath(contentRoot, binding.GeographyRelativePath);
        var root = Path.Combine(contentRoot, "BaseGame", "_backups", "continuous-surface-authoring", original ? "original" : "previous");
        var backupMain = Path.Combine(root, Path.GetFileName(main));
        var backupGeography = Path.Combine(root, Path.GetFileName(geography));
        if (!File.Exists(backupMain) || !File.Exists(backupGeography)) throw new InvalidDataException(original ? "首次迁移前备份不存在。" : "上一次运行时发布备份不存在。");
        var stage = Path.Combine(root, ".restore-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(stage);
        try
        {
            var mainStage = Path.Combine(stage, Path.GetFileName(main)); var geographyStage = Path.Combine(stage, Path.GetFileName(geography));
            File.Copy(backupMain, mainStage); File.Copy(backupGeography, geographyStage); VerifyCandidate(mainStage); VerifyCandidate(geographyStage);
            ReplacePair(mainStage, main, geographyStage, geography, stage);
        }
        finally { if (Directory.Exists(stage)) Directory.Delete(stage, true); }
    }

    private static void EnsureReady(WorldCompositionDocument composition, LinkedSourceSet linked)
    {
        if (composition.RuntimeSurface == null || composition.LegacyMigration == null) throw new InvalidDataException("发布要求当前世界组合同时具有 RuntimeSurfaceBinding 与 LegacyMigration binding。");
        if (composition.SurfaceWidthCells % 50 != 0 || composition.SurfaceHeightCells % 50 != 0) throw new InvalidDataException("当前旧 Runtime 的兼容发布要求世界宽高为 50 格整数倍。这只是旧 Runtime 兼容限制，不是 WorldComposer 的制作限制。");
        var errors = SurfaceAuthoringValidation.Validate(composition, linked).Where(x => x.Severity == ValidationSeverity.Error).Select(x => x.Message).ToList();
        if (errors.Count > 0) throw new InvalidDataException("发布前验证失败：\n" + string.Join("\n", errors));
    }

    private static string RuntimePath(string root, string relative)
    {
        var path = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!File.Exists(path)) throw new FileNotFoundException("运行时 JSON 不存在：" + relative, path);
        return path;
    }

    private static void BackupOnce(string source, string target) { if (!File.Exists(target)) File.Copy(source, target, false); }
    private static void VerifyCandidate(string path)
    {
        var root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
        if (root?["definitions"] is not JsonArray definitions || definitions.Count == 0) throw new InvalidDataException("兼容候选 JSON 无 definitions。");
        if (definitions[0] is not JsonObject definition) throw new InvalidDataException("兼容候选 JSON 的首个 definition 无效。");
        if (string.Equals(definition["type"]?.GetValue<string>(), "outdoorSurfaceGeography", StringComparison.Ordinal)) VerifyGeographyBake(definition);
    }

    // Mirrors the runtime loader's chunk/global consistency invariant so a malformed candidate
    // can never replace the playable pair merely because both JSON documents parse.
    private static void VerifyGeographyBake(JsonObject geography)
    {
        var width = geography["width"]?.GetValue<int>() ?? 0;
        var height = geography["height"]?.GetValue<int>() ?? 0;
        if (width <= 0 || height <= 0 || geography["rows"] is not JsonArray rows || rows.Count != height ||
            geography["coverageChunks"] is not JsonArray coverage || geography["chunkRows"] is not JsonArray chunkRows ||
            coverage.Count == 0 || chunkRows.Count != coverage.Count)
            throw new InvalidDataException("兼容 geography 候选的全局行或 Chunk 清单无效。");

        foreach (var row in rows)
            if (row is not JsonValue value || !value.TryGetValue<string>(out var text) || text.Length != width)
                throw new InvalidDataException("兼容 geography 候选的全局行宽度无效。");

        var coverageSet = new HashSet<(int X, int Y)>();
        foreach (var node in coverage)
        {
            if (node is not JsonObject chunk || !TryChunkCoord(chunk, out var coord) || !coverageSet.Add(coord))
                throw new InvalidDataException("兼容 geography 候选的 coverage Chunk 无效。");
        }
        var minX = coverageSet.Min(x => x.X); var maxX = coverageSet.Max(x => x.X);
        var minY = coverageSet.Min(x => x.Y); var maxY = coverageSet.Max(x => x.Y);
        var countX = maxX - minX + 1; var countY = maxY - minY + 1;
        if (width % countX != 0 || height % countY != 0)
            throw new InvalidDataException("兼容 geography 候选无法按 coverage Chunk 切分。");
        var chunkWidth = width / countX; var chunkHeight = height / countY;
        var seen = new HashSet<(int X, int Y)>();
        foreach (var node in chunkRows)
        {
            if (node is not JsonObject chunk || !TryChunkCoord(chunk, out var coord) || !coverageSet.Contains(coord) || !seen.Add(coord) ||
                chunk["rows"] is not JsonArray localRows || localRows.Count != chunkHeight)
                throw new InvalidDataException("兼容 geography 候选的 chunkRows 无效。");
            for (var localY = 0; localY < chunkHeight; localY++)
            {
                if (localRows[localY] is not JsonValue localValue || !localValue.TryGetValue<string>(out var local) || local.Length != chunkWidth)
                    throw new InvalidDataException("兼容 geography 候选的局部行宽度无效。");
                var global = rows[(coord.Y - minY) * chunkHeight + localY]!.GetValue<string>().Substring((coord.X - minX) * chunkWidth, chunkWidth);
                if (!string.Equals(local, global, StringComparison.Ordinal))
                    throw new InvalidDataException($"兼容 geography 候选的 Chunk/global 行不一致：({coord.X},{coord.Y})。");
            }
        }
        if (seen.Count != coverageSet.Count) throw new InvalidDataException("兼容 geography 候选缺少 chunkRows。");
    }

    private static bool TryChunkCoord(JsonObject node, out (int X, int Y) coord)
    {
        coord = default;
        if (node["x"] is not JsonValue x || node["y"] is not JsonValue y || !x.TryGetValue<int>(out var xValue) || !y.TryGetValue<int>(out var yValue)) return false;
        coord = (xValue, yValue); return true;
    }

    private static void ReplacePair(string sourceA, string targetA, string sourceB, string targetB, string stage)
    {
        var rollbackA = Path.Combine(stage, "rollback-a.json"); var rollbackB = Path.Combine(stage, "rollback-b.json");
        File.Copy(targetA, rollbackA, true); File.Copy(targetB, rollbackB, true);
        var firstDone = false;
        try { Replace(sourceA, targetA); firstDone = true; Replace(sourceB, targetB); }
        catch
        {
            if (firstDone) File.Copy(rollbackA, targetA, true);
            File.Copy(rollbackB, targetB, true);
            throw;
        }
    }

    private static void ReplaceTriple(string sourceA,string targetA,string sourceB,string targetB,string sourceC,string targetC,string stage)
    {
        var a=Path.Combine(stage,"rollback-a.json");var b=Path.Combine(stage,"rollback-b.json");var c=Path.Combine(stage,"rollback-c.json");var hadC=File.Exists(targetC);File.Copy(targetA,a,true);File.Copy(targetB,b,true);if(hadC)File.Copy(targetC,c,true);
        try { Replace(sourceA,targetA);Replace(sourceB,targetB);Replace(sourceC,targetC); }
        catch { File.Copy(a,targetA,true);File.Copy(b,targetB,true);if(hadC)File.Copy(c,targetC,true);else if(File.Exists(targetC))File.Delete(targetC);throw; }
    }

    private static void Replace(string source, string target)
    {
        var temporary = target + ".continuous-surface-authoring.tmp";
        File.Copy(source, temporary, true);
        try { File.Move(temporary, target, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
