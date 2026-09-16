using System.Text.Json.Nodes;

namespace SurfaceAuthoring.Core;

public sealed class LegacyWorldImportResult
{
    public required WorldCompositionDocument Composition { get; init; }
    public required WorldSiteBlueprintDocument HuangcunBlueprint { get; init; }
    public required string CompositionPath { get; init; }
    public required string BlueprintPath { get; init; }
    public required string ReportPath { get; init; }
    public int HuangcunSourcePlacementCount { get; init; }
    public int HuangcunObjectCount { get; init; }
    public int WaterCellCount { get; init; }
    public int RoadPathCount { get; init; }
    public int BridgeCount { get; init; }
    public int LegacyBlockerCount { get; init; }
    public List<string> Warnings { get; } = [];
}

public static class LegacyWorldMigration
{
    public const string MainRelative = "BaseGame/Data/Worlds/main_wilderness_surface_v1.json";
    public const string GeographyRelative = "BaseGame/Data/Worlds/w2a_surface_geography_baked_v1.json";
    public const string ReferenceMapRelative = "BaseGame/Data/Maps/ch01_reference_map.json";
    public const string ReferencePlacesRelative = "BaseGame/Data/LocalPlaces/ch01_reference_places.json";

    public static LegacyWorldImportResult ImportCurrentWorld(string contentRoot)
    {
        contentRoot=Path.GetFullPath(contentRoot);var mainPath=Need(contentRoot,MainRelative);var geographyPath=Need(contentRoot,GeographyRelative);var referenceMap=Definition(Need(contentRoot,ReferenceMapRelative));var referencePlaces=Definition(Need(contentRoot,ReferencePlacesRelative));
        var main=Definition(mainPath);var geo=Definition(geographyPath);var mapPlacements=referenceMap["placements"]!.AsArray().OfType<JsonObject>().ToList();var places=referencePlaces["locations"]!.AsArray().OfType<JsonObject>().ToDictionary(x=>Text(x,"id"),StringComparer.Ordinal);
        var cell=Number(main,"cellSize");var originX=Number(main,"originWorldX");var originY=Number(main,"originWorldY");
        var chunks=main["chunks"]!.AsArray().OfType<JsonObject>().ToList();var chunkCellWidth=(int)Math.Round(Number(main,"chunkWidth")/cell);var chunkCellHeight=(int)Math.Round(Number(main,"chunkHeight")/cell);
        var width=(chunks.Max(x=>Integer(x,"x"))-chunks.Min(x=>Integer(x,"x"))+1)*chunkCellWidth;var height=(chunks.Max(x=>Integer(x,"y"))-chunks.Min(x=>Integer(x,"y"))+1)*chunkCellHeight;
        var composition=new WorldCompositionDocument{CompositionId="main_world",DisplayName="当前项目大世界",SurfaceWidthCells=width,SurfaceHeightCells=height,DefaultBaseTerrain=BaseTerrain.Plain,RuntimeSurface=new(){SurfaceId=Text(main,"id"),OriginWorldX=originX,OriginWorldY=originY,SurfaceCellWorldSize=cell},LegacyMigration=new(){MainSurfaceRelativePath=MainRelative,GeographyRelativePath=GeographyRelative}};
        var geographyPatch=ImportGeography(geo,composition,originX,originY,cell);
        var all=main["sitePlacements"]!.AsArray().OfType<JsonObject>().Where(x=>Text(x,"siteId")=="base:site_huangcun").ToList();
        if(all.Count==0)throw new InvalidDataException("主世界中没有找到黄村 sitePlacements。");
        var minX=all.Min(x=>CellFloor(Number(x,"worldX"),originX,cell))-2;var minY=all.Min(x=>CellFloor(Number(x,"worldY"),originY,cell))-2;
        var maxX=all.Max(x=>CellCeiling(Number(x,"worldX")+Number(x,"worldWidth"),originX,cell))+2;var maxY=all.Max(x=>CellCeiling(Number(x,"worldY")+Number(x,"worldHeight"),originY,cell))+2;
        var blueprint=new WorldSiteBlueprintDocument{BlueprintId="base:site_huangcun",DisplayName="黄村",WidthCells=maxX-minX,HeightCells=maxY-minY};
        foreach(var source in all.OrderBy(x=>Text(x,"stableId"),StringComparer.Ordinal))
        {
            var x=CellFloor(Number(source,"worldX"),originX,cell)-minX;var y=CellFloor(Number(source,"worldY"),originY,cell)-minY;var w=Math.Max(1,CellCeiling(Number(source,"worldX")+Number(source,"worldWidth"),originX,cell)-CellFloor(Number(source,"worldX"),originX,cell));var h=Math.Max(1,CellCeiling(Number(source,"worldY")+Number(source,"worldHeight"),originY,cell)-CellFloor(Number(source,"worldY"),originY,cell));var kind=Text(source,"kind");
            if(kind=="zoneForest")
            {
                for(var yy=y;yy<Math.Min(blueprint.HeightCells,y+h);yy++)for(var xx=x;xx<Math.Min(blueprint.WidthCells,x+w);xx++)blueprint.FineTerrainSources.Add(new(){SurfaceCellX=xx,SurfaceCellY=yy,FeatureOverride=TerrainFeature.Forest,FeatureDensity=.85});
                continue;
            }
            var metadata=new SortedDictionary<string,string>(StringComparer.Ordinal);foreach(var property in source)if(property.Value!=null&&property.Key is not ("stableId" or "kind" or "worldX" or "worldY" or "worldWidth" or "worldHeight"))metadata[property.Key]=ScalarText(property.Value);foreach(var key in new[]{"worldX","worldY","worldWidth","worldHeight"})metadata["legacy"+char.ToUpperInvariant(key[0])+key[1..]]=ScalarText(source[key]!);
            var reference=mapPlacements.FirstOrDefault(p=>Text(p,"kind")==kind&&Integer(p,"x")==Integer(source,"sourceGridX")&&Integer(p,"y")==Integer(source,"sourceGridY"));if(reference!=null){metadata["referenceMapPlacementId"]=Text(reference,"id");foreach(var key in new[]{"label","boundLocationId"})if(!metadata.ContainsKey(key)&&reference[key]!=null)metadata[key]=ScalarText(reference[key]!);}
            if(metadata.TryGetValue("boundLocationId",out var locationId)&&places.TryGetValue(locationId,out var place)){metadata["localPlaceName"]=Text(place,"name");metadata["localPlaceKind"]=Text(place,"kind");if(place["tags"]!=null)metadata["localPlaceTags"]=place["tags"]!.ToJsonString();}
            blueprint.ObjectPlacements.Add(new(){PlacementId=Text(source,"stableId"),KindId=kind,LocalSurfaceX=x,LocalSurfaceY=y,WidthCells=w,HeightCells=h,Metadata=metadata});
        }
        var sourceRoot=Path.Combine(contentRoot,"BaseGame","Authoring","ContinuousSurface");var worldDir=Path.Combine(sourceRoot,"Worlds");var blueprintDir=Path.Combine(sourceRoot,"Blueprints");Directory.CreateDirectory(worldDir);Directory.CreateDirectory(blueprintDir);
        var blueprintPath=Path.Combine(blueprintDir,"huangcun.worldsiteblueprint.json");var compositionPath=Path.Combine(worldDir,"main_world.worldcomposition.json");
        composition.WorldSiteBlueprintPlacements.Add(new(){PlacementId="huangcun",BlueprintId=blueprint.BlueprintId,SourcePath=Path.GetRelativePath(worldDir,blueprintPath).Replace('\\','/'),SurfaceCellX=minX,SurfaceCellY=minY});
        SurfaceAuthoringJson.Save(Path.Combine(sourceRoot,"Patches","w2a_geography_detail.detailpatch.json"),geographyPatch);SurfaceAuthoringJson.Save(blueprintPath,blueprint);SurfaceAuthoringJson.Save(compositionPath,composition);
        var waterCells=geo["rows"]!.AsArray().Sum(row=>row!.GetValue<string>().Count(c=>c is '~' or 'B'));var reportPath=Path.Combine(sourceRoot,"import-report.txt");var result=new LegacyWorldImportResult{Composition=composition,HuangcunBlueprint=blueprint,CompositionPath=compositionPath,BlueprintPath=blueprintPath,ReportPath=reportPath,HuangcunSourcePlacementCount=all.Count,HuangcunObjectCount=blueprint.ObjectPlacements.Count,WaterCellCount=waterCells,RoadPathCount=composition.Roads.Count,BridgeCount=composition.WorldObjectPlacements.Count(x=>x.KindId=="bridge"),LegacyBlockerCount=composition.WorldObjectPlacements.Count(x=>x.KindId=="rock")};
        result.Warnings.Add("W2A 的道路和水域已优先由 mapPrimitives 重建；旧栅格的道路、桥、岩石与水域细节保存在 W2A 精修块中。后续编辑路径时，样条边缘可能与旧栅格边缘略有不同。");
        result.Warnings.Add("除黄村外的既有据点仍留在运行时主世界模板中，未自动迁移为蓝图。");
        File.WriteAllText(reportPath,Report(result),new System.Text.UTF8Encoding(false));return result;
    }

    private static DetailPatchDocument ImportGeography(JsonObject geo,WorldCompositionDocument composition,double worldOriginX,double worldOriginY,double cell)
    {
        var gx=CellFloor(Number(geo,"originWorldX"),worldOriginX,cell);var gy=CellFloor(Number(geo,"originWorldY"),worldOriginY,cell);var width=Integer(geo,"width");var height=Integer(geo,"height");
        var patch=new DetailPatchDocument{PatchId="w2a_geography_detail",DisplayName="W2A 地理精修",WidthCells=width,HeightCells=height};var rows=geo["rows"]!.AsArray();
        for(var row=0;row<rows.Count;row++){var text=rows[row]!.GetValue<string>();var y=height-1-row;for(var x=0;x<Math.Min(width,text.Length);x++){var glyph=text[x];if(glyph is '~' or 'B')patch.FineTerrainOverrides.Add(new(){SurfaceCellX=x,SurfaceCellY=y,BaseTerrainOverride=BaseTerrain.Water,CompatibilityGlyph=glyph=='B'?"B":null});else if(glyph is '=' or '#')patch.FineTerrainOverrides.Add(new(){SurfaceCellX=x,SurfaceCellY=y,CompatibilityGlyph=glyph.ToString()});}}
        foreach(var p in geo["mapPrimitives"]!.AsArray().OfType<JsonObject>())
        {
            var kind=Text(p,"kind");if(kind=="roadPolyline")composition.Roads.Add(new(){PathId=Text(p,"stableId"),RoadClass=RoadClass.Road,ControlPoints=Points(p,worldOriginX,worldOriginY,cell)});
            else if(kind=="waterRegion"){var x=Cell(Number(p,"worldX")+Number(p,"worldWidth")/2,worldOriginX,cell);var y0=Cell(Number(p,"worldY"),worldOriginY,cell);var y1=Cell(Number(p,"worldY")+Number(p,"worldHeight"),worldOriginY,cell);composition.Rivers.Add(new(){PathId=Text(p,"stableId"),WidthCells=Math.Max(1,Number(p,"worldWidth")/cell),ControlPoints=[new(){X=x,Y=y0},new(){X=x,Y=y1}]});}
            else if(kind is "bridge" or "solidBlocker")composition.WorldObjectPlacements.Add(new(){PlacementId=Text(p,"stableId"),KindId=kind=="bridge"?"bridge":"rock",WorldSurfaceX=CellFloor(Number(p,"worldX"),worldOriginX,cell),WorldSurfaceY=CellFloor(Number(p,"worldY"),worldOriginY,cell),WidthCells=Math.Max(1,(int)Math.Ceiling(Number(p,"worldWidth")/cell)),HeightCells=Math.Max(1,(int)Math.Ceiling(Number(p,"worldHeight")/cell)),Metadata=new(StringComparer.Ordinal){{"compatibilityMask","w2a_geography_detail"}}});
        }
        foreach(var crossing in CompositionEngine.FindCrossings(composition))
        {
            var bridge=composition.WorldObjectPlacements.Where(o=>o.KindId=="bridge").OrderBy(o=>Math.Pow(crossing.X-(o.WorldSurfaceX+o.WidthCells/2d),2)+Math.Pow(crossing.Y-(o.WorldSurfaceY+o.HeightCells/2d),2)).FirstOrDefault();if(bridge==null||Math.Sqrt(Math.Pow(crossing.X-(bridge.WorldSurfaceX+bridge.WidthCells/2d),2)+Math.Pow(crossing.Y-(bridge.WorldSurfaceY+bridge.HeightCells/2d),2))>25)continue;crossing.Resolution=CrossingResolution.Bridge;composition.Crossings.Add(crossing);bridge.Metadata["riverPathId"]=crossing.RiverPathId;bridge.Metadata["roadPathId"]=crossing.RoadPathId;
        }
        // Exact imported water cells are kept as a linked patch; its file is written after the output root is known.
        composition.DetailPatchPlacements.Add(new(){PlacementId="w2a_geography_detail",PatchId=patch.PatchId,SourcePath="../Patches/w2a_geography_detail.detailpatch.json",SurfaceCellX=gx,SurfaceCellY=gy});
        return patch;
    }

    public static IReadOnlyList<string> ExportCompatibilityCandidates(WorldCompositionDocument composition,LinkedSourceSet linked,string contentRoot,string outputDirectory)
    {
        if(composition.RuntimeSurface==null||composition.LegacyMigration==null)throw new InvalidDataException("当前世界组合没有运行时 Surface 绑定或 Legacy 导入信息。");Directory.CreateDirectory(outputDirectory);
        var mainPath=Need(contentRoot,composition.LegacyMigration.MainSurfaceRelativePath);var geoPath=Need(contentRoot,composition.LegacyMigration.GeographyRelativePath);var mainRoot=JsonNode.Parse(File.ReadAllText(mainPath))!.AsObject();var main=mainRoot["definitions"]![0]!.AsObject();var geoRoot=JsonNode.Parse(File.ReadAllText(geoPath))!.AsObject();var geo=geoRoot["definitions"]![0]!.AsObject();var binding=composition.RuntimeSurface;var engine=new CompositionEngine(composition,linked);
        var gx=CellFloor(Number(geo,"originWorldX"),binding.OriginWorldX,binding.SurfaceCellWorldSize);var gy=CellFloor(Number(geo,"originWorldY"),binding.OriginWorldY,binding.SurfaceCellWorldSize);var width=Integer(geo,"width");var height=Integer(geo,"height");var rows=new JsonArray();var compatibility=new Dictionary<(int X,int Y),char>();foreach(var patchPlacement in composition.DetailPatchPlacements)if(linked.Patches.TryGetValue(patchPlacement.PlacementId,out var patch))foreach(var c in patch.FineTerrainOverrides)if(c.CompatibilityGlyph is{Length:1} glyph)compatibility[(patchPlacement.SurfaceCellX+c.SurfaceCellX,patchPlacement.SurfaceCellY+c.SurfaceCellY)]=glyph[0];
        for(var row=0;row<height;row++){var y=gy+height-1-row;var chars=new char[width];for(var x=0;x<width;x++){var worldX=gx+x;var c=engine.Resolve(worldX,y);chars[x]=c.BaseTerrain==BaseTerrain.Water?'~':c.Road.HasValue?'=':'.';if(compatibility.TryGetValue((worldX,y),out var imported))chars[x]=imported;foreach(var o in composition.WorldObjectPlacements.Where(o=>!o.Metadata.ContainsKey("compatibilityMask")&&worldX>=o.WorldSurfaceX&&y>=o.WorldSurfaceY&&worldX<o.WorldSurfaceX+o.WidthCells&&y<o.WorldSurfaceY+o.HeightCells))chars[x]=o.KindId=="bridge"?'B':o.KindId=="rock"?'#':chars[x];}rows.Add(new string(chars));}geo["rows"]=rows;
        var primitives=new JsonArray();foreach(var river in composition.Rivers)primitives.Add(Primitive(river.PathId,"waterRegion",river.ControlPoints,river.WidthCells,binding));foreach(var road in composition.Roads)primitives.Add(Primitive(road.PathId,"roadPolyline",road.ControlPoints,RoadWidths.GetWidth(road.RoadClass),binding));foreach(var o in composition.WorldObjectPlacements.Where(x=>x.KindId is "bridge" or "rock"))primitives.Add(new JsonObject{{"stableId",o.PlacementId},{"kind",o.KindId=="bridge"?"bridge":"solidBlocker"},{"worldX",binding.OriginWorldX+o.WorldSurfaceX*binding.SurfaceCellWorldSize},{"worldY",binding.OriginWorldY+o.WorldSurfaceY*binding.SurfaceCellWorldSize},{"worldWidth",o.WidthCells*binding.SurfaceCellWorldSize},{"worldHeight",o.HeightCells*binding.SurfaceCellWorldSize},{"strokeWidth",0},{"points",new JsonArray()}});geo["mapPrimitives"]=primitives;
        if(linked.Blueprints.TryGetValue("huangcun",out var bp)&&composition.WorldSiteBlueprintPlacements.FirstOrDefault(x=>x.PlacementId=="huangcun") is{} placement)
        {
            var placements=main["sitePlacements"]!.AsArray();foreach(var old in placements.OfType<JsonObject>().Where(x=>Text(x,"siteId")=="base:site_huangcun").ToList())placements.Remove(old);
            foreach(var o in bp.ObjectPlacements.OrderBy(x=>x.PlacementId,StringComparer.Ordinal)){var wx=binding.OriginWorldX+(placement.SurfaceCellX+o.LocalSurfaceX)*binding.SurfaceCellWorldSize;var wy=binding.OriginWorldY+(placement.SurfaceCellY+o.LocalSurfaceY)*binding.SurfaceCellWorldSize;var item=new JsonObject{{"stableId",o.PlacementId},{"siteId","base:site_huangcun"},{"worldX",wx},{"worldY",wy},{"worldWidth",o.WidthCells*binding.SurfaceCellWorldSize},{"worldHeight",o.HeightCells*binding.SurfaceCellWorldSize},{"kind",o.KindId},{"blocksMovement",o.Metadata.TryGetValue("blocksMovement",out var blocked)&&bool.TryParse(blocked,out var b)&&b}};foreach(var key in new[]{"boundLocationId","label","sourceGridX","sourceGridY","sourceCellsW","sourceCellsH","chunkX","chunkY"})if(o.Metadata.TryGetValue(key,out var value))item[key]=int.TryParse(value,out var n)?JsonValue.Create(n):JsonValue.Create(value);placements.Add(item);}
        }
        var mainOut=Path.Combine(outputDirectory,"main_wilderness_surface_v1.candidate.json");var geoOut=Path.Combine(outputDirectory,"w2a_surface_geography_baked_v1.candidate.json");File.WriteAllText(mainOut,mainRoot.ToJsonString(SurfaceAuthoringJson.Options),new System.Text.UTF8Encoding(false));File.WriteAllText(geoOut,geoRoot.ToJsonString(SurfaceAuthoringJson.Options),new System.Text.UTF8Encoding(false));return[mainOut,geoOut];
    }

    private static JsonObject Primitive(string id,string kind,List<PathControlPoint> points,double width,RuntimeSurfaceBinding b){var values=new JsonArray();foreach(var p in points){values.Add(b.OriginWorldX+p.X*b.SurfaceCellWorldSize);values.Add(b.OriginWorldY+p.Y*b.SurfaceCellWorldSize);}return new(){{"stableId",id},{"kind",kind},{"worldX",0},{"worldY",0},{"worldWidth",0},{"worldHeight",0},{"strokeWidth",width*b.SurfaceCellWorldSize},{"points",values}};}
    private static List<PathControlPoint> Points(JsonObject p,double ox,double oy,double cell){var a=p["points"]!.AsArray();var r=new List<PathControlPoint>();for(var i=0;i+1<a.Count;i+=2)r.Add(new(){X=(a[i]!.GetValue<double>()-ox)/cell,Y=(a[i+1]!.GetValue<double>()-oy)/cell});return r;}
    private static string Report(LegacyWorldImportResult r)=>$"当前项目世界导入报告\n\n世界组合：{r.Composition.SurfaceWidthCells}×{r.Composition.SurfaceHeightCells} 连续世界格\nSurface ID：{r.Composition.RuntimeSurface?.SurfaceId}\n导入水域格：{r.WaterCellCount}\n导入道路路径：{r.RoadPathCount}\n导入桥：{r.BridgeCount}\n导入旧阻挡物：{r.LegacyBlockerCount}\n黄村蓝图：{r.HuangcunBlueprint.WidthCells}×{r.HuangcunBlueprint.HeightCells} 连续世界格\n黄村权威放置项：{r.HuangcunSourcePlacementCount}\n黄村对象：{r.HuangcunObjectCount}\n黄村森林覆盖格：{r.HuangcunBlueprint.FineTerrainSources.Count}\n河流 / 水域路径：{r.Composition.Rivers.Count}\n精修块：{r.Composition.DetailPatchPlacements.Count}\n\n未能自动迁移：\n- {string.Join("\n- ",r.Warnings)}\n";
    private static string Need(string root,string relative){var p=Path.Combine(root,relative.Replace('/',Path.DirectorySeparatorChar));if(!File.Exists(p))throw new FileNotFoundException("缺少导入文件："+relative,p);return p;}
    private static JsonObject Definition(string path)=>JsonNode.Parse(File.ReadAllText(path))!["definitions"]![0]!.AsObject();
    private static string Text(JsonObject o,string key)=>o[key]?.GetValue<string>()??string.Empty;private static double Number(JsonObject o,string key)=>o[key]!.GetValue<double>();private static int Integer(JsonObject o,string key)=>o[key]!.GetValue<int>();
    private static string ScalarText(JsonNode value)=>value is JsonValue scalar&&scalar.TryGetValue<string>(out var text)?text:value.ToJsonString();
    private static int CellFloor(double value,double origin,double cell)=>(int)Math.Floor((value-origin)/cell+1e-5);private static int CellCeiling(double value,double origin,double cell)=>(int)Math.Ceiling((value-origin)/cell-1e-5);private static double Cell(double value,double origin,double cell)=>(value-origin)/cell;
}
