using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;

namespace SurfaceAuthoring.Core;

/// Source coordinates match runtime coordinates: origin is the lower-left Surface Cell,
/// X grows east/right and Y grows north/up. Screen Y inversion belongs only to viewports.
public static class SurfaceAuthoringCoordinates
{
    public const int SurfaceCellSize = 1;
    public const int WorldEditorCellSize = 10;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BaseTerrain { Plain, Mountain, Water }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TerrainFeature { None, Forest }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RoadClass { Trail, SmallRoad, Road, MajorRoad }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CrossingResolution { Unresolved, Bridge, Ford }

public static class RoadWidths
{
    public static double GetWidth(RoadClass roadClass) => roadClass switch
    {
        RoadClass.Trail => 1.25, RoadClass.SmallRoad => 2.5, RoadClass.Road => 4, RoadClass.MajorRoad => 7, _ => 2.5
    };
}

public sealed class CoarseTerrainSourceCell
{
    public int EditorCellX { get; set; }
    public int EditorCellY { get; set; }
    public BaseTerrain BaseTerrain { get; set; } = BaseTerrain.Plain;
}

public sealed class CoarseFeatureSourceCell
{
    public int EditorCellX { get; set; }
    public int EditorCellY { get; set; }
    public TerrainFeature Feature { get; set; }
    public double Density { get; set; } = 1;
}

public sealed class FineTerrainSourceCell
{
    public int SurfaceCellX { get; set; }
    public int SurfaceCellY { get; set; }
    public BaseTerrain? BaseTerrainOverride { get; set; }
    public TerrainFeature? FeatureOverride { get; set; }
    public double FeatureDensity { get; set; } = 1;
    public string? CompatibilityGlyph { get; set; }
}

public sealed class PathControlPoint
{
    public double X { get; set; }
    public double Y { get; set; }
    public double? WidthCells { get; set; }
}

public sealed class RiverPathSource
{
    public string PathId { get; set; } = string.Empty;
    public double WidthCells { get; set; } = 5;
    public List<PathControlPoint> ControlPoints { get; set; } = [];
}

public sealed class RoadPathSource
{
    public string PathId { get; set; } = string.Empty;
    public RoadClass RoadClass { get; set; } = RoadClass.SmallRoad;
    public List<PathControlPoint> ControlPoints { get; set; } = [];
}

public sealed class CrossingSource
{
    public string RiverPathId { get; set; } = string.Empty;
    public string RoadPathId { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public CrossingResolution Resolution { get; set; }
}

public sealed class WorldSiteBlueprintPlacement
{
    public string PlacementId { get; set; } = string.Empty;
    public string BlueprintId { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public int SurfaceCellX { get; set; }
    public int SurfaceCellY { get; set; }
    public int RotationQuarterTurns { get; set; }
}

public sealed class DetailPatchPlacement
{
    public string PlacementId { get; set; } = string.Empty;
    public string PatchId { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public int SurfaceCellX { get; set; }
    public int SurfaceCellY { get; set; }
}

public sealed class WorldObjectPlacement
{
    public string PlacementId { get; set; } = string.Empty;
    public string KindId { get; set; } = string.Empty;
    public int WorldSurfaceX { get; set; }
    public int WorldSurfaceY { get; set; }
    public int WidthCells { get; set; } = 1;
    public int HeightCells { get; set; } = 1;
    public int RotationQuarterTurns { get; set; }
    public SortedDictionary<string, string> Metadata { get; set; } = new(StringComparer.Ordinal);
}

public sealed class RuntimeSurfaceBinding
{
    public string SurfaceId { get; set; } = string.Empty;
    public double OriginWorldX { get; set; }
    public double OriginWorldY { get; set; }
    public double SurfaceCellWorldSize { get; set; }
}

public sealed class LegacyMigrationBinding
{
    public string MainSurfaceRelativePath { get; set; } = string.Empty;
    public string GeographyRelativePath { get; set; } = string.Empty;
}

public sealed class BlueprintObjectPlacement
{
    public string PlacementId { get; set; } = string.Empty;
    public string KindId { get; set; } = string.Empty;
    public string? ContentRef { get; set; }
    public string? AssetRef { get; set; }
    public int LocalSurfaceX { get; set; }
    public int LocalSurfaceY { get; set; }
    public int WidthCells { get; set; } = 1;
    public int HeightCells { get; set; } = 1;
    public int RotationQuarterTurns { get; set; }
    public SortedDictionary<string, string> Metadata { get; set; } = new(StringComparer.Ordinal);
}

public sealed class WorldCompositionDocument
{
    public const int CurrentSchemaVersion = 3;
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public string CompositionId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int SurfaceWidthCells { get; set; } = 1900;
    public int SurfaceHeightCells { get; set; } = 850;
    public int WorldEditorCellSize { get; set; } = SurfaceAuthoringCoordinates.WorldEditorCellSize;
    public ulong WorldSeed { get; set; } = 1;
    public BaseTerrain DefaultBaseTerrain { get; set; } = BaseTerrain.Plain;
    public RuntimeSurfaceBinding? RuntimeSurface { get; set; }
    public LegacyMigrationBinding? LegacyMigration { get; set; }
    public List<CoarseTerrainSourceCell> CoarseTerrainSources { get; set; } = [];
    public List<CoarseFeatureSourceCell> CoarseFeatureSources { get; set; } = [];
    public List<RiverPathSource> Rivers { get; set; } = [];
    public List<RoadPathSource> Roads { get; set; } = [];
    public List<CrossingSource> Crossings { get; set; } = [];
    public List<WorldSiteBlueprintPlacement> WorldSiteBlueprintPlacements { get; set; } = [];
    public List<DetailPatchPlacement> DetailPatchPlacements { get; set; } = [];
    public List<WorldObjectPlacement> WorldObjectPlacements { get; set; } = [];
    [JsonIgnore] public int WorldEditorGridWidth => (int)Math.Ceiling(SurfaceWidthCells / 10d);
    [JsonIgnore] public int WorldEditorGridHeight => (int)Math.Ceiling(SurfaceHeightCells / 10d);
}

public sealed class WorldSiteBlueprintDocument
{
    public const int CurrentSchemaVersion = 3;
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public string BlueprintId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int WidthCells { get; set; } = 1;
    public int HeightCells { get; set; } = 1;
    public List<FineTerrainSourceCell> FineTerrainSources { get; set; } = [];
    public List<BlueprintObjectPlacement> ObjectPlacements { get; set; } = [];
}

public sealed class DetailPatchDocument
{
    public const int CurrentSchemaVersion = 3;
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public string PatchId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int WidthCells { get; set; } = 1;
    public int HeightCells { get; set; } = 1;
    public List<FineTerrainSourceCell> FineTerrainOverrides { get; set; } = [];
}

public enum ValidationSeverity { Warning, Error }
public sealed record SurfaceAuthoringValidationIssue(ValidationSeverity Severity, string Message);

public static class SurfaceAuthoringValidation
{
    public static IReadOnlyList<SurfaceAuthoringValidationIssue> Validate(WorldCompositionDocument d, LinkedSourceSet? linked = null)
    {
        var issues = Identity(d.CompositionId, d.DisplayName);
        if (d.SchemaVersion != WorldCompositionDocument.CurrentSchemaVersion) Error(issues, "不支持此 WorldComposition schemaVersion。");
        if (d.SurfaceWidthCells <= 0 || d.SurfaceHeightCells <= 0) Error(issues, "连续世界尺寸必须为正数。");
        if (d.WorldEditorCellSize != 10) Error(issues, "worldEditorCellSize 必须为 10。");
        Duplicates(issues, d.Rivers.Select(x => x.PathId).Concat(d.Roads.Select(x => x.PathId)).Concat(d.WorldSiteBlueprintPlacements.Select(x => x.PlacementId)).Concat(d.DetailPatchPlacements.Select(x => x.PlacementId)).Concat(d.WorldObjectPlacements.Select(x => x.PlacementId)));
        foreach(var group in d.CoarseTerrainSources.GroupBy(x=>(x.EditorCellX,x.EditorCellY)).Where(x=>x.Count()>1))Error(issues,$"大地图编辑格 ({group.Key.EditorCellX},{group.Key.EditorCellY}) 存在重复基础地形源。");
        foreach(var group in d.CoarseFeatureSources.GroupBy(x=>(x.EditorCellX,x.EditorCellY)).Where(x=>x.Count()>1))Error(issues,$"大地图编辑格 ({group.Key.EditorCellX},{group.Key.EditorCellY}) 存在重复地貌特征源。");
        foreach(var cell in d.CoarseTerrainSources.Cast<object>().Concat(d.CoarseFeatureSources)){var x=cell is CoarseTerrainSourceCell terrain?terrain.EditorCellX:((CoarseFeatureSourceCell)cell).EditorCellX;var y=cell is CoarseTerrainSourceCell terrainCell?terrainCell.EditorCellY:((CoarseFeatureSourceCell)cell).EditorCellY;if(x<0||y<0||x>=d.WorldEditorGridWidth||y>=d.WorldEditorGridHeight)Error(issues,$"大地图编辑格 ({x},{y}) 的制作源超出世界范围。");}
        foreach(var p in d.WorldSiteBlueprintPlacements){if(string.IsNullOrWhiteSpace(p.PlacementId)||string.IsNullOrWhiteSpace(p.BlueprintId)||string.IsNullOrWhiteSpace(p.SourcePath))Error(issues,"据点蓝图放置必须提供 placementId、blueprintId 和 sourcePath。");if(p.RotationQuarterTurns is <0 or >3)Error(issues,$"据点蓝图放置 {p.PlacementId} 的 rotationQuarterTurns 必须为 0 到 3。");}
        foreach(var p in d.DetailPatchPlacements)if(string.IsNullOrWhiteSpace(p.PlacementId)||string.IsNullOrWhiteSpace(p.PatchId)||string.IsNullOrWhiteSpace(p.SourcePath))Error(issues,"精修块放置必须提供 placementId、patchId 和 sourcePath。");
        foreach(var p in d.WorldObjectPlacements){if(string.IsNullOrWhiteSpace(p.PlacementId)||string.IsNullOrWhiteSpace(p.KindId))Error(issues,"世界物件必须提供 placementId 和 kindId。");if(p.WidthCells<=0||p.HeightCells<=0||p.WorldSurfaceX<0||p.WorldSurfaceY<0||p.WorldSurfaceX+p.WidthCells>d.SurfaceWidthCells||p.WorldSurfaceY+p.HeightCells>d.SurfaceHeightCells)Error(issues,$"世界物件 {p.PlacementId} 的范围无效或超出世界。");if(p.RotationQuarterTurns is <0 or >3)Error(issues,$"世界物件 {p.PlacementId} 的 rotationQuarterTurns 必须为 0 到 3。");}
        foreach(var f in d.CoarseFeatureSources)if(f.Feature==TerrainFeature.Forest&&(d.CoarseTerrainSources.FirstOrDefault(t=>t.EditorCellX==f.EditorCellX&&t.EditorCellY==f.EditorCellY)?.BaseTerrain??d.DefaultBaseTerrain)==BaseTerrain.Water)Error(issues,$"大地图编辑格 ({f.EditorCellX},{f.EditorCellY})：水域不能生成森林。");
        foreach(var c in d.Crossings.Where(x=>x.Resolution==CrossingResolution.Bridge))if(!d.WorldObjectPlacements.Any(o=>o.KindId=="bridge"&&o.Metadata.TryGetValue("riverPathId",out var river)&&river==c.RiverPathId&&o.Metadata.TryGetValue("roadPathId",out var road)&&road==c.RoadPathId))Error(issues,$"道路/河流交叉口 {c.RoadPathId} / {c.RiverPathId} 缺少绑定的桥物件。");
        foreach (var river in d.Rivers) ValidatePath(issues, river.PathId, river.WidthCells, river.ControlPoints, d);
        foreach (var road in d.Roads) ValidatePath(issues, road.PathId, RoadWidths.GetWidth(road.RoadClass), road.ControlPoints, d);
        foreach (var crossing in CompositionEngine.FindCrossings(d)) if (crossing.Resolution == CrossingResolution.Unresolved) Warn(issues, $"坐标 ({crossing.X:0.#},{crossing.Y:0.#}) 附近的道路/河流交叉口尚未处理。");
        if (linked != null) issues.AddRange(linked.Issues);
        return issues;
    }

    public static IReadOnlyList<SurfaceAuthoringValidationIssue> Validate(WorldSiteBlueprintDocument d)
    {
        var issues = Fine(d.SchemaVersion, d.BlueprintId, d.DisplayName, d.WidthCells, d.HeightCells, "WorldSiteBlueprint");
        Duplicates(issues, d.ObjectPlacements.Select(x => x.PlacementId));
        ValidateFineCells(issues,d.FineTerrainSources,d.WidthCells,d.HeightCells);
        foreach (var o in d.ObjectPlacements) { if(string.IsNullOrWhiteSpace(o.PlacementId)||string.IsNullOrWhiteSpace(o.KindId))Error(issues,"对象放置必须提供 placementId 和 kindId。");if(o.RotationQuarterTurns is <0 or >3)Error(issues,$"对象 {o.PlacementId} 的 rotationQuarterTurns 必须为 0 到 3。");if (o.WidthCells <= 0 || o.HeightCells <= 0 || o.LocalSurfaceX < 0 || o.LocalSurfaceY < 0 || o.LocalSurfaceX + o.WidthCells > d.WidthCells || o.LocalSurfaceY + o.HeightCells > d.HeightCells) Error(issues, $"对象 {o.PlacementId} 的范围无效或超出文档。"); }
        return issues;
    }
    public static IReadOnlyList<SurfaceAuthoringValidationIssue> Validate(DetailPatchDocument d) {var issues=Fine(d.SchemaVersion,d.PatchId,d.DisplayName,d.WidthCells,d.HeightCells,"DetailPatch");ValidateFineCells(issues,d.FineTerrainOverrides,d.WidthCells,d.HeightCells);return issues;}

    private static List<SurfaceAuthoringValidationIssue> Identity(string id, string name) { var r = new List<SurfaceAuthoringValidationIssue>(); if (string.IsNullOrWhiteSpace(id)) Error(r, "文档技术 ID 不能为空。"); if (string.IsNullOrWhiteSpace(name)) Error(r, "文档名称不能为空。"); return r; }
    private static List<SurfaceAuthoringValidationIssue> Fine(int version, string id, string name, int w, int h, string label) { var r = Identity(id, name); if (version != 3) Error(r, $"不支持此 {label} schemaVersion。"); if (w <= 0 || h <= 0) Error(r, "连续世界格尺寸必须为正数。"); return r; }
    private static void ValidatePath(List<SurfaceAuthoringValidationIssue> r, string id, double width, List<PathControlPoint> points, WorldCompositionDocument d) { if (string.IsNullOrWhiteSpace(id)) Error(r, "路径 ID 不能为空。"); if (width <= 0) Error(r, $"路径 {id} 的宽度必须为正数。"); if (points.Count < 2) Error(r, $"路径 {id} 至少需要两个控制点。"); foreach (var p in points) if (p.X < 0 || p.Y < 0 || p.X > d.SurfaceWidthCells || p.Y > d.SurfaceHeightCells) Error(r, $"路径 {id} 存在超出世界范围的控制点。"); }
    private static void ValidateFineCells(List<SurfaceAuthoringValidationIssue> r,List<FineTerrainSourceCell> cells,int width,int height){foreach(var group in cells.GroupBy(x=>(x.SurfaceCellX,x.SurfaceCellY)).Where(x=>x.Count()>1))Error(r,$"连续世界格 ({group.Key.SurfaceCellX},{group.Key.SurfaceCellY}) 存在重复精修地形源。");foreach(var c in cells){if(c.SurfaceCellX<0||c.SurfaceCellY<0||c.SurfaceCellX>=width||c.SurfaceCellY>=height)Error(r,$"连续世界格 ({c.SurfaceCellX},{c.SurfaceCellY}) 的精修地形源超出文档范围。");if(c.FeatureDensity is <0 or >1)Error(r,$"连续世界格 ({c.SurfaceCellX},{c.SurfaceCellY}) 的特征密度必须在 0 到 1 之间。");if(c.CompatibilityGlyph!=null&&c.CompatibilityGlyph is not ("=" or "B" or "#"))Error(r,$"连续世界格 ({c.SurfaceCellX},{c.SurfaceCellY}) 的兼容栅格标记无效。");}}
    private static void Duplicates(List<SurfaceAuthoringValidationIssue> r, IEnumerable<string> ids) { foreach (var id in ids.Where(x => !string.IsNullOrWhiteSpace(x)).GroupBy(x => x, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key)) Error(r, $"技术 ID 重复：{id}"); }
    private static void Error(List<SurfaceAuthoringValidationIssue> r, string m) => r.Add(new(ValidationSeverity.Error, m));
    private static void Warn(List<SurfaceAuthoringValidationIssue> r, string m) => r.Add(new(ValidationSeverity.Warning, m));
}

public static class SurfaceAuthoringJson
{
    public const string WorldCompositionExtension = ".worldcomposition.json";
    public const string WorldSiteBlueprintExtension = ".worldsiteblueprint.json";
    public const string DetailPatchExtension = ".detailpatch.json";
    public const string BakeExtension = ".continuoussurface.bake.json";
    public static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, Converters = { new JsonStringEnumConverter() } };
    public static string Serialize<T>(T document) => JsonSerializer.Serialize(document, Options);
    public static void Save(string p, WorldCompositionDocument d) { Ensure(SurfaceAuthoringValidation.Validate(d)); Write(p, d); }
    public static void Save(string p, WorldSiteBlueprintDocument d) { Ensure(SurfaceAuthoringValidation.Validate(d)); Write(p, d); }
    public static void Save(string p, DetailPatchDocument d) { Ensure(SurfaceAuthoringValidation.Validate(d)); Write(p, d); }
    public static WorldCompositionDocument LoadWorldComposition(string p) => Read<WorldCompositionDocument>(p, d => SurfaceAuthoringValidation.Validate(d));
    public static WorldSiteBlueprintDocument LoadWorldSiteBlueprint(string p) => Read<WorldSiteBlueprintDocument>(p, SurfaceAuthoringValidation.Validate);
    public static DetailPatchDocument LoadDetailPatch(string p) => Read<DetailPatchDocument>(p, SurfaceAuthoringValidation.Validate);
    public static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(Serialize(value), Options)!;
    internal static void Write<T>(string path, T value) { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!); File.WriteAllText(path, Serialize(value), new UTF8Encoding(false)); }
    private static T Read<T>(string p, Func<T, IReadOnlyList<SurfaceAuthoringValidationIssue>> validator) where T : class { try{var node=JsonNode.Parse(File.ReadAllText(p))??throw new InvalidDataException("制作源文档为空。");UpgradeNode<T>(node);var d=node.Deserialize<T>(Options)??throw new InvalidDataException("制作源文档为空。");Ensure(validator(d));return d;}catch(JsonException ex){throw new InvalidDataException($"制作源 JSON 格式无效（第 {ex.LineNumber??0} 行）。");} }
    private static void UpgradeNode<T>(JsonNode node)
    {
        if(node is not JsonObject root)return;var version=root["schemaVersion"]?.GetValue<int>()??1;if(version>=3)return;
        static string Terrain(string? value)=>value switch{"Rock"=>"Mountain","Water"=>"Water",_=>"Plain"};
        if(root["defaultBaseTerrain"] is JsonValue dv)root["defaultBaseTerrain"]=Terrain(dv.GetValue<string>());
        foreach(var key in new[]{"coarseTerrainSources","fineTerrainSources","fineTerrainOverrides"})if(root[key] is JsonArray cells)foreach(var item in cells.OfType<JsonObject>())if(item["baseTerrain"] is JsonValue b)item["baseTerrain"]=Terrain(b.GetValue<string>());else if(item["baseTerrainOverride"] is JsonValue bo)item["baseTerrainOverride"]=Terrain(bo.GetValue<string>());
        if(typeof(T)==typeof(WorldCompositionDocument)&&root["coarseFeatureSources"] is JsonArray features)
        {
            var terrain=root["coarseTerrainSources"] as JsonArray??new JsonArray();root["coarseTerrainSources"]=terrain;
            foreach(var item in features.OfType<JsonObject>().ToList())if(item["feature"]?.GetValue<string>()=="Mountain"){var x=item["editorCellX"]!.GetValue<int>();var y=item["editorCellY"]!.GetValue<int>();foreach(var old in terrain.OfType<JsonObject>().Where(t=>t["editorCellX"]?.GetValue<int>()==x&&t["editorCellY"]?.GetValue<int>()==y).ToList())terrain.Remove(old);terrain.Add(new JsonObject{{"editorCellX",x},{"editorCellY",y},{"baseTerrain","Mountain"}});features.Remove(item);}
        }
        foreach(var key in new[]{"fineTerrainSources","fineTerrainOverrides"})if(root[key] is JsonArray fine)foreach(var item in fine.OfType<JsonObject>())if(item["featureOverride"]?.GetValue<string>()=="Mountain"){item["baseTerrainOverride"]="Mountain";item["featureOverride"]="None";}
        root["schemaVersion"]=3;
    }
    private static void Ensure(IReadOnlyList<SurfaceAuthoringValidationIssue> issues) { var errors = issues.Where(x => x.Severity == ValidationSeverity.Error).ToList(); if (errors.Count > 0) throw new InvalidDataException(string.Join(Environment.NewLine, errors.Select(x => x.Message))); }
}
