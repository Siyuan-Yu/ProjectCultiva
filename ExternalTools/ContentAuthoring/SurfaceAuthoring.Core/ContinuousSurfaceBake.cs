namespace SurfaceAuthoring.Core;

public sealed class FinalContinuousSurfaceBake
{
    public const int CurrentSchemaVersion = 2;
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public string CompositionId { get; set; } = string.Empty;
    public int SurfaceWidthCells { get; set; }
    public int SurfaceHeightCells { get; set; }
    public ulong Seed { get; set; }
    public string DeterministicSourceHash { get; set; } = string.Empty;
    public List<BakeRow> Rows { get; set; } = [];
    public List<CrossingSource> Crossings { get; set; } = [];
    public List<BakedBlueprintInstance> BlueprintInstances { get; set; } = [];
    public List<BakedObjectPlacement> ObjectPlacements { get; set; } = [];
}

public sealed class BakeRow { public int Y { get; set; } public List<BakeRun> Runs { get; set; } = []; }
public sealed class BakeRun
{
    public int StartX { get; set; }
    public int Length { get; set; }
    public BaseTerrain BaseTerrain { get; set; }
    public TerrainFeature Feature { get; set; }
    public int FeatureDensityPercent { get; set; }
    public bool River { get; set; }
    public RoadClass? Road { get; set; }
}
public sealed class BakedBlueprintInstance { public string PlacementId { get; set; } = string.Empty; public string BlueprintId { get; set; } = string.Empty; public int SurfaceCellX { get; set; } public int SurfaceCellY { get; set; } public int RotationQuarterTurns { get; set; } }
public sealed class BakedObjectPlacement { public string BlueprintPlacementId { get; set; } = string.Empty; public string PlacementId { get; set; } = string.Empty; public string KindId { get; set; } = string.Empty; public string? ContentRef { get; set; } public string? AssetRef { get; set; } public int WorldSurfaceX { get; set; } public int WorldSurfaceY { get; set; } public int WidthCells { get; set; } public int HeightCells { get; set; } public int RotationQuarterTurns { get; set; } public SortedDictionary<string,string> Metadata { get; set; } = new(StringComparer.Ordinal); }

public static class ContinuousSurfaceBaker
{
    public static FinalContinuousSurfaceBake Bake(WorldCompositionDocument source, LinkedSourceSet linked)
    {
        var issues = SurfaceAuthoringValidation.Validate(source, linked);
        var errors = issues.Where(x => x.Severity == ValidationSeverity.Error).ToList();
        if (errors.Count > 0) throw new InvalidDataException(string.Join(Environment.NewLine, errors.Select(x => x.Message)));
        var engine = new CompositionEngine(source, linked);
        var bake = new FinalContinuousSurfaceBake { CompositionId=source.CompositionId,SurfaceWidthCells=source.SurfaceWidthCells,SurfaceHeightCells=source.SurfaceHeightCells,Seed=source.WorldSeed,DeterministicSourceHash=SourceHash(source, linked),Crossings=CompositionEngine.FindCrossings(source) };
        for(var y=0;y<source.SurfaceHeightCells;y++)
        {
            var row=new BakeRow{Y=y}; BakeRun? run=null;
            for(var x=0;x<source.SurfaceWidthCells;x++)
            {
                var c=engine.Resolve(x,y); var density=(int)Math.Round(c.FeatureDensity*100,MidpointRounding.AwayFromZero);
                if(run!=null&&run.BaseTerrain==c.BaseTerrain&&run.Feature==c.Feature&&run.FeatureDensityPercent==density&&run.River==c.River&&run.Road==c.Road) run.Length++;
                else { run=new BakeRun{StartX=x,Length=1,BaseTerrain=c.BaseTerrain,Feature=c.Feature,FeatureDensityPercent=density,River=c.River,Road=c.Road}; row.Runs.Add(run); }
            }
            bake.Rows.Add(row);
        }
        foreach(var p in source.WorldSiteBlueprintPlacements.OrderBy(x=>x.PlacementId,StringComparer.Ordinal))
        {
            bake.BlueprintInstances.Add(new(){PlacementId=p.PlacementId,BlueprintId=p.BlueprintId,SurfaceCellX=p.SurfaceCellX,SurfaceCellY=p.SurfaceCellY,RotationQuarterTurns=p.RotationQuarterTurns});
            if(!linked.Blueprints.TryGetValue(p.PlacementId,out var b))continue;
            foreach(var o in b.ObjectPlacements.OrderBy(x=>x.PlacementId,StringComparer.Ordinal)){var xy=Rotate(o.LocalSurfaceX,o.LocalSurfaceY,b.WidthCells,b.HeightCells,p.RotationQuarterTurns);bake.ObjectPlacements.Add(new(){BlueprintPlacementId=p.PlacementId,PlacementId=o.PlacementId,KindId=o.KindId,ContentRef=o.ContentRef,AssetRef=o.AssetRef,WorldSurfaceX=p.SurfaceCellX+xy.X,WorldSurfaceY=p.SurfaceCellY+xy.Y,WidthCells=o.WidthCells,HeightCells=o.HeightCells,RotationQuarterTurns=(o.RotationQuarterTurns+p.RotationQuarterTurns)%4,Metadata=new(o.Metadata,StringComparer.Ordinal)});}
        }
        foreach(var o in source.WorldObjectPlacements.OrderBy(x=>x.PlacementId,StringComparer.Ordinal))bake.ObjectPlacements.Add(new(){BlueprintPlacementId=string.Empty,PlacementId=o.PlacementId,KindId=o.KindId,WorldSurfaceX=o.WorldSurfaceX,WorldSurfaceY=o.WorldSurfaceY,WidthCells=o.WidthCells,HeightCells=o.HeightCells,RotationQuarterTurns=o.RotationQuarterTurns,Metadata=new(o.Metadata,StringComparer.Ordinal)});
        return bake;
    }
    public static void Save(string path,FinalContinuousSurfaceBake bake)=>SurfaceAuthoringJson.Write(path,bake);
    private static string SourceHash(WorldCompositionDocument source,LinkedSourceSet linked)
    {
        var text=new System.Text.StringBuilder(SurfaceAuthoringJson.Serialize(source)); foreach(var x in linked.Blueprints)text.Append('\n').Append(x.Key).Append('\n').Append(SurfaceAuthoringJson.Serialize(x.Value));foreach(var x in linked.Patches)text.Append('\n').Append(x.Key).Append('\n').Append(SurfaceAuthoringJson.Serialize(x.Value));return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text.ToString()))).ToLowerInvariant();
    }
    private static (int X,int Y) Rotate(int x,int y,int w,int h,int q)=>(((q%4)+4)%4) switch{1=>(h-1-y,x),2=>(w-1-x,h-1-y),3=>(y,w-1-x),_=>(x,y)};
}
