using System.Security.Cryptography;
using System.Text;

namespace SurfaceAuthoring.Core;

public sealed class LinkedSourceSet
{
    public SortedDictionary<string, WorldSiteBlueprintDocument> Blueprints { get; } = new(StringComparer.Ordinal);
    public SortedDictionary<string, DetailPatchDocument> Patches { get; } = new(StringComparer.Ordinal);
    public List<SurfaceAuthoringValidationIssue> Issues { get; } = [];
}

public readonly record struct ResolvedSurfaceCell(BaseTerrain BaseTerrain, TerrainFeature Feature, double FeatureDensity, bool River, RoadClass? Road);

public static class LinkedSourceLoader
{
    public static LinkedSourceSet Load(WorldCompositionDocument composition, string? compositionPath)
    {
        var result = new LinkedSourceSet();
        var root = compositionPath == null ? null : Path.GetDirectoryName(Path.GetFullPath(compositionPath));
        foreach (var p in composition.WorldSiteBlueprintPlacements.OrderBy(x => x.PlacementId, StringComparer.Ordinal))
        {
            Load(p.SourcePath, root, p.PlacementId, path => SurfaceAuthoringJson.LoadWorldSiteBlueprint(path), d => d.BlueprintId == p.BlueprintId, result.Blueprints, result.Issues);
            if (composition.RuntimeSurface != null && result.Blueprints.TryGetValue(p.PlacementId, out var blueprint))
            {
                var migration = BlueprintGeometryMigration.RestoreLegacyExactGeometry(composition, p, blueprint);
                foreach (var warning in migration.Warnings) result.Issues.Add(new(ValidationSeverity.Warning, warning));
            }
        }
        foreach (var p in composition.DetailPatchPlacements.OrderBy(x => x.PlacementId, StringComparer.Ordinal))
        {
            Load(p.SourcePath, root, p.PlacementId, path => SurfaceAuthoringJson.LoadDetailPatch(path), d => d.PatchId == p.PatchId, result.Patches, result.Issues);
        }
        ValidateBounds(composition, result);
        return result;
    }

    private static void Load<T>(string source, string? root, string placementId, Func<string, T> load, Func<T, bool> idMatches, IDictionary<string, T> target, List<SurfaceAuthoringValidationIssue> issues)
    {
        if (root == null) { issues.Add(new(ValidationSeverity.Error, $"放置项 {placementId}：解析引用资源前请先保存世界组合。")); return; }
        try
        {
            var path = Path.GetFullPath(Path.Combine(root, source));
            if (!File.Exists(path)) { issues.Add(new(ValidationSeverity.Error, $"放置项 {placementId}：引用资源不存在：{source}")); return; }
            var document = load(path);
            if (!idMatches(document)) { issues.Add(new(ValidationSeverity.Error, $"放置项 {placementId}：引用资源的技术 ID 不匹配。")); return; }
            target[placementId] = document;
        }
        catch (Exception) { issues.Add(new(ValidationSeverity.Error, $"放置项 {placementId}：读取引用资源失败。")); }
    }

    private static void ValidateBounds(WorldCompositionDocument c, LinkedSourceSet linked)
    {
        foreach (var b in linked.Blueprints.Values) linked.Issues.AddRange(SurfaceAuthoringValidation.Validate(b));
        foreach (var p in linked.Patches.Values) linked.Issues.AddRange(SurfaceAuthoringValidation.Validate(p));
        var occupied = new List<(string Id, int X, int Y, int W, int H)>();
        foreach (var p in c.WorldSiteBlueprintPlacements)
        {
            if (!linked.Blueprints.TryGetValue(p.PlacementId, out var b)) continue;
            var w = p.RotationQuarterTurns % 2 == 0 ? b.WidthCells : b.HeightCells; var h = p.RotationQuarterTurns % 2 == 0 ? b.HeightCells : b.WidthCells;
            if (p.SurfaceCellX < 0 || p.SurfaceCellY < 0 || p.SurfaceCellX + w > c.SurfaceWidthCells || p.SurfaceCellY + h > c.SurfaceHeightCells) linked.Issues.Add(new(ValidationSeverity.Error, $"据点蓝图放置 {p.PlacementId} 超出连续世界范围。"));
            occupied.Add((p.PlacementId,p.SurfaceCellX,p.SurfaceCellY,w,h));
        }
        foreach (var p in c.DetailPatchPlacements) if (linked.Patches.TryGetValue(p.PlacementId, out var d)) { if (p.SurfaceCellX < 0 || p.SurfaceCellY < 0 || p.SurfaceCellX + d.WidthCells > c.SurfaceWidthCells || p.SurfaceCellY + d.HeightCells > c.SurfaceHeightCells) linked.Issues.Add(new(ValidationSeverity.Error, $"精修块放置 {p.PlacementId} 超出连续世界范围。")); occupied.Add((p.PlacementId,p.SurfaceCellX,p.SurfaceCellY,d.WidthCells,d.HeightCells)); }
        for(var i=0;i<occupied.Count;i++)for(var j=i+1;j<occupied.Count;j++){var a=occupied[i];var b=occupied[j];if(a.X<b.X+b.W&&a.X+a.W>b.X&&a.Y<b.Y+b.H&&a.Y+a.H>b.Y)linked.Issues.Add(new(ValidationSeverity.Warning,$"引用放置项 {a.Id} 与 {b.Id} 重叠；后合成的来源会覆盖先前结果。"));}
    }
}

public sealed class CompositionEngine
{
    private readonly WorldCompositionDocument _source;
    private readonly LinkedSourceSet _linked;
    private readonly Dictionary<(int, int), CoarseTerrainSourceCell> _terrain;
    private readonly Dictionary<(int, int), CoarseFeatureSourceCell> _features;
    private readonly Dictionary<(int, int), FineTerrainSourceCell> _fine = [];
    private readonly HashSet<long> _riverCells=[];
    private readonly Dictionary<long,RoadClass> _roadCells=[];

    public CompositionEngine(WorldCompositionDocument source, LinkedSourceSet linked)
    {
        _source = source; _linked = linked;
        _terrain = source.CoarseTerrainSources.ToDictionary(x => (x.EditorCellX, x.EditorCellY));
        _features = source.CoarseFeatureSources.ToDictionary(x => (x.EditorCellX, x.EditorCellY));
        foreach(var p in source.Rivers)RasterizePath(p.ControlPoints,p.WidthCells,(x,y)=>_riverCells.Add(Key(x,y)));
        foreach(var p in source.Roads)RasterizePath(p.ControlPoints,RoadWidths.GetWidth(p.RoadClass),(x,y)=>_roadCells[Key(x,y)]=p.RoadClass);
        foreach (var p in source.DetailPatchPlacements) if (linked.Patches.TryGetValue(p.PlacementId, out var patch)) foreach (var cell in patch.FineTerrainOverrides) _fine[(p.SurfaceCellX + cell.SurfaceCellX, p.SurfaceCellY + cell.SurfaceCellY)] = cell;
        foreach (var p in source.WorldSiteBlueprintPlacements) if (linked.Blueprints.TryGetValue(p.PlacementId, out var blueprint)) foreach (var cell in blueprint.FineTerrainSources) { var xy = Rotate(cell.SurfaceCellX, cell.SurfaceCellY, blueprint.WidthCells, blueprint.HeightCells, p.RotationQuarterTurns); _fine[(p.SurfaceCellX + xy.X, p.SurfaceCellY + xy.Y)] = cell; }
    }

    public ResolvedSurfaceCell Resolve(int x, int y)
    {
        var ex = x / 10; var ey = y / 10; var lx = x % 10; var ly = y % 10;
        var baseTerrain = TerrainAt(ex, ey);
        var noise = Hash01(_source.WorldSeed, x, y, 17);
        if (lx < 2 && noise < (2 - lx) * .23) baseTerrain = TerrainAt(ex - 1, ey);
        else if (lx > 7 && noise < (lx - 7) * .23) baseTerrain = TerrainAt(ex + 1, ey);
        if (ly < 2 && Hash01(_source.WorldSeed, x, y, 29) < (2 - ly) * .23) baseTerrain = TerrainAt(ex, ey - 1);
        else if (ly > 7 && Hash01(_source.WorldSeed, x, y, 31) < (ly - 7) * .23) baseTerrain = TerrainAt(ex, ey + 1);
        var feature = FeatureAt(ex, ey); var density = feature?.Density ?? 0; var actualFeature = feature != null && Hash01(_source.WorldSeed, x, y, 43) <= density ? feature.Feature : TerrainFeature.None;
        var river=_riverCells.Contains(Key(x,y));RoadClass? road=_roadCells.TryGetValue(Key(x,y),out var roadClass)?roadClass:null;
        if (_fine.TryGetValue((x, y), out var fine)) { if (fine.BaseTerrainOverride.HasValue) baseTerrain = fine.BaseTerrainOverride.Value; if (fine.FeatureOverride.HasValue) { actualFeature = fine.FeatureOverride.Value; density = fine.FeatureDensity; } }
        if(river)baseTerrain=BaseTerrain.Water;
        if(baseTerrain==BaseTerrain.Water)actualFeature=TerrainFeature.None;
        return new(baseTerrain, actualFeature, density, river, road);
    }

    private BaseTerrain TerrainAt(int x, int y) => _terrain.TryGetValue((x, y), out var t) ? t.BaseTerrain : _source.DefaultBaseTerrain;
    private CoarseFeatureSourceCell? FeatureAt(int x, int y) => _features.TryGetValue((x, y), out var f) ? f : null;
    private static (int X, int Y) Rotate(int x, int y, int w, int h, int q) => (((q % 4) + 4) % 4) switch { 1 => (h - 1 - y, x), 2 => (w - 1 - x, h - 1 - y), 3 => (y, w - 1 - x), _ => (x, y) };
    private static double Hash01(ulong seed, int x, int y, ulong salt) { var z = seed ^ ((ulong)(uint)x * 0x9E3779B185EBCA87UL) ^ ((ulong)(uint)y * 0xC2B2AE3D27D4EB4FUL) ^ salt; z ^= z >> 30; z *= 0xBF58476D1CE4E5B9UL; z ^= z >> 27; z *= 0x94D049BB133111EBUL; z ^= z >> 31; return (z >> 11) * (1d / (1UL << 53)); }
    public static double DistanceToPath(double x, double y, IReadOnlyList<PathControlPoint> points, double width)
    {
        if (points.Count < 2) return double.MaxValue; var best = double.MaxValue;
        foreach (var sample in SmoothSegments(points)) { var d = DistanceToSegment(x, y, sample.A.X, sample.A.Y, sample.B.X, sample.B.Y); if (d < best) best = d; }
        return best;
    }
    private void RasterizePath(IReadOnlyList<PathControlPoint> points,double width,Action<int,int> add){var radius=width/2d;foreach(var s in SmoothSegments(points)){var minX=Math.Max(0,(int)Math.Floor(Math.Min(s.A.X,s.B.X)-radius));var maxX=Math.Min(_source.SurfaceWidthCells-1,(int)Math.Ceiling(Math.Max(s.A.X,s.B.X)+radius));var minY=Math.Max(0,(int)Math.Floor(Math.Min(s.A.Y,s.B.Y)-radius));var maxY=Math.Min(_source.SurfaceHeightCells-1,(int)Math.Ceiling(Math.Max(s.A.Y,s.B.Y)+radius));for(var y=minY;y<=maxY;y++)for(var x=minX;x<=maxX;x++)if(DistanceToSegment(x+.5,y+.5,s.A.X,s.A.Y,s.B.X,s.B.Y)<=radius)add(x,y);}}
    private static long Key(int x,int y)=>((long)y<<32)|(uint)x;
    public static IEnumerable<(PathControlPoint A, PathControlPoint B)> SmoothSegments(IReadOnlyList<PathControlPoint> p)
    {
        if (p.Count < 2) yield break; PathControlPoint? previous = null;
        for (var segment = 0; segment < p.Count - 1; segment++) for (var step = 0; step <= 8; step++) { var t = step / 8d; var point = Catmull(p[Math.Max(0, segment - 1)], p[segment], p[segment + 1], p[Math.Min(p.Count - 1, segment + 2)], t); if (previous != null) yield return (previous, point); previous = point; }
    }
    private static PathControlPoint Catmull(PathControlPoint p0, PathControlPoint p1, PathControlPoint p2, PathControlPoint p3, double t) { var t2 = t * t; var t3 = t2 * t; return new() { X = .5 * ((2 * p1.X) + (-p0.X + p2.X) * t + (2*p0.X-5*p1.X+4*p2.X-p3.X)*t2 + (-p0.X+3*p1.X-3*p2.X+p3.X)*t3), Y = .5 * ((2 * p1.Y) + (-p0.Y + p2.Y) * t + (2*p0.Y-5*p1.Y+4*p2.Y-p3.Y)*t2 + (-p0.Y+3*p1.Y-3*p2.Y+p3.Y)*t3) }; }
    private static double DistanceToSegment(double x, double y, double ax, double ay, double bx, double by) { var dx = bx-ax; var dy=by-ay; var l=dx*dx+dy*dy; if(l==0)return Math.Sqrt((x-ax)*(x-ax)+(y-ay)*(y-ay)); var t=Math.Clamp(((x-ax)*dx+(y-ay)*dy)/l,0,1); var px=ax+t*dx; var py=ay+t*dy; return Math.Sqrt((x-px)*(x-px)+(y-py)*(y-py)); }

    public static List<CrossingSource> FindCrossings(WorldCompositionDocument c)
    {
        var result = new List<CrossingSource>(); foreach (var river in c.Rivers) foreach (var road in c.Roads) foreach (var a in SmoothSegments(river.ControlPoints)) foreach (var b in SmoothSegments(road.ControlPoints)) if (TryIntersect(a.A,a.B,b.A,b.B,out var x,out var y)) { var known=c.Crossings.FirstOrDefault(k=>k.RiverPathId==river.PathId&&k.RoadPathId==road.PathId); result.Add(new(){RiverPathId=river.PathId,RoadPathId=road.PathId,X=x,Y=y,Resolution=known?.Resolution??CrossingResolution.Unresolved}); break; } return result.OrderBy(x=>x.RiverPathId,StringComparer.Ordinal).ThenBy(x=>x.RoadPathId,StringComparer.Ordinal).ToList();
    }
    private static bool TryIntersect(PathControlPoint a,PathControlPoint b,PathControlPoint c,PathControlPoint d,out double x,out double y){x=y=0;var den=(a.X-b.X)*(c.Y-d.Y)-(a.Y-b.Y)*(c.X-d.X);if(Math.Abs(den)<1e-8)return false;var t=((a.X-c.X)*(c.Y-d.Y)-(a.Y-c.Y)*(c.X-d.X))/den;var u=-((a.X-b.X)*(a.Y-c.Y)-(a.Y-b.Y)*(a.X-c.X))/den;if(t<0||t>1||u<0||u>1)return false;x=a.X+t*(b.X-a.X);y=a.Y+t*(b.Y-a.Y);return true;}
}
