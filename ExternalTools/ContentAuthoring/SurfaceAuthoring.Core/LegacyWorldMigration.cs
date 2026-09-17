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
        contentRoot=Path.GetFullPath(contentRoot);var mainPath=Need(contentRoot,MainRelative);var geographyPath=Need(contentRoot,GeographyRelative);
        var referenceMapPath=Path.Combine(contentRoot,ReferenceMapRelative);var referencePlacesPath=Path.Combine(contentRoot,ReferencePlacesRelative);
        var referenceMap=File.Exists(referenceMapPath)?Definition(referenceMapPath):null;
        var referencePlaces=File.Exists(referencePlacesPath)?Definition(referencePlacesPath):null;
        var main=Definition(mainPath);var geo=Definition(geographyPath);
        var mapPlacements=referenceMap?["placements"]?.AsArray().OfType<JsonObject>().ToList()??new List<JsonObject>();
        var places=referencePlaces?["locations"]?.AsArray().OfType<JsonObject>().ToDictionary(x=>Text(x,"id"),StringComparer.Ordinal)??new Dictionary<string,JsonObject>(StringComparer.Ordinal);
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
            var cellX=CellFloor(Number(source,"worldX"),originX,cell);var cellY=CellFloor(Number(source,"worldY"),originY,cell);var envelopeW=Math.Max(1,CellCeiling(Number(source,"worldX")+Number(source,"worldWidth"),originX,cell)-cellX);var envelopeH=Math.Max(1,CellCeiling(Number(source,"worldY")+Number(source,"worldHeight"),originY,cell)-cellY);var x=cellX-minX;var y=cellY-minY;var kind=Text(source,"kind");
            if(kind=="zoneForest")
            {
                for(var yy=y;yy<Math.Min(blueprint.HeightCells,y+envelopeH);yy++)for(var xx=x;xx<Math.Min(blueprint.WidthCells,x+envelopeW);xx++)blueprint.FineTerrainSources.Add(new(){SurfaceCellX=xx,SurfaceCellY=yy,FeatureOverride=TerrainFeature.Forest,FeatureDensity=.85});
                continue;
            }
            var metadata=new SortedDictionary<string,string>(StringComparer.Ordinal);foreach(var property in source)if(property.Value!=null&&property.Key is not ("stableId" or "kind" or "worldX" or "worldY" or "worldWidth" or "worldHeight"))metadata[property.Key]=ScalarText(property.Value);foreach(var key in new[]{"worldX","worldY","worldWidth","worldHeight"})metadata["legacy"+char.ToUpperInvariant(key[0])+key[1..]]=ScalarText(source[key]!);
            var reference=mapPlacements.FirstOrDefault(p=>Text(p,"kind")==kind&&Integer(p,"x")==Integer(source,"sourceGridX")&&Integer(p,"y")==Integer(source,"sourceGridY"));if(reference!=null){metadata["referenceMapPlacementId"]=Text(reference,"id");foreach(var key in new[]{"label","boundLocationId"})if(!metadata.ContainsKey(key)&&reference[key]!=null)metadata[key]=ScalarText(reference[key]!);}
            if(metadata.TryGetValue("boundLocationId",out var locationId)&&places.TryGetValue(locationId,out var place)){metadata["localPlaceName"]=Text(place,"name");metadata["localPlaceKind"]=Text(place,"kind");if(place["tags"]!=null)metadata["localPlaceTags"]=place["tags"]!.ToJsonString();}
            metadata["legacyGeometryState"]="exact";blueprint.ObjectPlacements.Add(new(){PlacementId=Text(source,"stableId"),KindId=kind,LocalSurfaceX=(Number(source,"worldX")-(originX+minX*cell))/cell,LocalSurfaceY=(Number(source,"worldY")-(originY+minY*cell))/cell,WidthCells=Number(source,"worldWidth")/cell,HeightCells=Number(source,"worldHeight")/cell,Metadata=metadata});
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
        // Current publish preserves Surface authority and strips legacy geometry links from imported sources.
        if(main["chunks"] is JsonArray currentChunks)
            foreach(var chunk in currentChunks.OfType<JsonObject>())chunk.Remove("sourceMapLayoutId");
        if(main["siteRegions"] is JsonArray currentRegions)
            foreach(var region in currentRegions.OfType<JsonObject>())region.Remove("sourceLocalMapId");
        // Interior/Cave places belong to their separate LocalPlaceSet, never the outdoor registry.
        if(main["sitePlaces"] is JsonArray outdoorPlaces)
            foreach(var place in outdoorPlaces.OfType<JsonObject>()
                        .Where(x=>!string.IsNullOrEmpty(Text(x,"localMapId"))).ToList())
                outdoorPlaces.Remove(place);
        if(composition.SurfaceWidthCells%50!=0||composition.SurfaceHeightCells%50!=0)throw new InvalidDataException("当前旧 Runtime 的兼容发布要求世界宽高为 50 格整数倍。这只是旧 Runtime 兼容限制，不是 WorldComposer 的制作限制。");
        var width=composition.SurfaceWidthCells;var height=composition.SurfaceHeightCells;var rows=new JsonArray();var compatibility=new Dictionary<(int X,int Y),char>();foreach(var patchPlacement in composition.DetailPatchPlacements)if(linked.Patches.TryGetValue(patchPlacement.PlacementId,out var patch))foreach(var c in patch.FineTerrainOverrides)if(c.CompatibilityGlyph is{Length:1} glyph)compatibility[(patchPlacement.SurfaceCellX+c.SurfaceCellX,patchPlacement.SurfaceCellY+c.SurfaceCellY)]=glyph[0];
        // Legacy geography stores row zero as world/surface Y zero.  The chunkRows contract
        // uses that identical ascending-Y row order; never flip one representation independently.
        for(var row=0;row<height;row++){var y=row;var chars=new char[width];for(var x=0;x<width;x++){var c=engine.Resolve(x,y);chars[x]=c.BaseTerrain==BaseTerrain.Water?'~':c.Road.HasValue?'=':'.';if(compatibility.TryGetValue((x,y),out var imported))chars[x]=imported;foreach(var o in composition.WorldObjectPlacements.Where(o=>!o.Metadata.ContainsKey("compatibilityMask")&&x>=o.WorldSurfaceX&&y>=o.WorldSurfaceY&&x<o.WorldSurfaceX+o.WidthCells&&y<o.WorldSurfaceY+o.HeightCells))chars[x]=o.KindId=="bridge"?'B':o.KindId=="rock"?'#':chars[x];}rows.Add(new string(chars));}
        geo["originWorldX"]=binding.OriginWorldX;geo["originWorldY"]=binding.OriginWorldY;geo["cellSize"]=binding.SurfaceCellWorldSize;geo["width"]=width;geo["height"]=height;geo["rows"]=rows;
        var coverage=new JsonArray();var chunkRows=new JsonArray();var oldChunks=(main["chunks"] as JsonArray??new JsonArray()).OfType<JsonObject>().ToDictionary(x=>$"{Integer(x,"x")}_{Integer(x,"y")}",StringComparer.Ordinal);var chunks=new JsonArray();var chunkWidth=width/50;var chunkHeight=height/50;
        for(var chunkY=0;chunkY<chunkHeight;chunkY++)for(var chunkX=0;chunkX<chunkWidth;chunkX++){coverage.Add(new JsonObject{{"x",chunkX},{"y",chunkY}});var chunk=new JsonObject{{"id",$"main_{chunkX}_{chunkY}"},{"x",chunkX},{"y",chunkY}};chunks.Add(chunk);var localRows=new JsonArray();for(var localY=0;localY<50;localY++){var globalY=chunkY*50+localY;localRows.Add(rows[globalY]!.GetValue<string>().Substring(chunkX*50,50));}chunkRows.Add(new JsonObject{{"x",chunkX},{"y",chunkY},{"rows",localRows}});}
        geo["coverageChunks"]=coverage;geo["chunkRows"]=chunkRows;main["originWorldX"]=binding.OriginWorldX;main["originWorldY"]=binding.OriginWorldY;main["cellSize"]=binding.SurfaceCellWorldSize;main["chunkWidth"]=50*binding.SurfaceCellWorldSize;main["chunkHeight"]=50*binding.SurfaceCellWorldSize;main["chunks"]=chunks;
        var primitives=new JsonArray();foreach(var river in composition.Rivers)primitives.Add(Primitive(river.PathId,"waterRegion",river.ControlPoints,river.WidthCells,binding));foreach(var road in composition.Roads)primitives.Add(Primitive(road.PathId,"roadPolyline",road.ControlPoints,RoadWidths.GetWidth(road.RoadClass),binding));foreach(var o in composition.WorldObjectPlacements.Where(x=>x.KindId is "bridge" or "rock"))primitives.Add(new JsonObject{{"stableId",o.PlacementId},{"kind",o.KindId=="bridge"?"bridge":"solidBlocker"},{"worldX",binding.OriginWorldX+o.WorldSurfaceX*binding.SurfaceCellWorldSize},{"worldY",binding.OriginWorldY+o.WorldSurfaceY*binding.SurfaceCellWorldSize},{"worldWidth",o.WidthCells*binding.SurfaceCellWorldSize},{"worldHeight",o.HeightCells*binding.SurfaceCellWorldSize},{"strokeWidth",0},{"points",new JsonArray()}});geo["mapPrimitives"]=primitives;
        if(linked.Blueprints.TryGetValue("huangcun",out var bp)&&composition.WorldSiteBlueprintPlacements.FirstOrDefault(x=>x.PlacementId=="huangcun") is{} placement)
        {
            var placements=main["sitePlacements"]!.AsArray();foreach(var old in placements.OfType<JsonObject>().Where(x=>Text(x,"siteId")=="base:site_huangcun").ToList())placements.Remove(old);
            foreach(var o in bp.ObjectPlacements.OrderBy(x=>x.PlacementId,StringComparer.Ordinal)){var wx=binding.OriginWorldX+(placement.SurfaceCellX+o.LocalSurfaceX)*binding.SurfaceCellWorldSize;var wy=binding.OriginWorldY+(placement.SurfaceCellY+o.LocalSurfaceY)*binding.SurfaceCellWorldSize;var item=new JsonObject{{"stableId",o.PlacementId},{"siteId","base:site_huangcun"},{"worldX",wx},{"worldY",wy},{"worldWidth",o.WidthCells*binding.SurfaceCellWorldSize},{"worldHeight",o.HeightCells*binding.SurfaceCellWorldSize},{"kind",o.KindId},{"blocksMovement",o.Metadata.TryGetValue("blocksMovement",out var blocked)&&bool.TryParse(blocked,out var b)&&b}};foreach(var key in new[]{"boundLocationId","label","sourceGridX","sourceGridY","sourceCellsW","sourceCellsH","chunkX","chunkY"})if(o.Metadata.TryGetValue(key,out var value))item[key]=int.TryParse(value,out var n)?JsonValue.Create(n):JsonValue.Create(value);placements.Add(item);}ValidateBlueprintGeometryFidelity(bp,main["sitePlacements"]!.AsArray());
        }
        var terrainRows=new JsonArray();var forestRows=new JsonArray();for(var y=0;y<height;y++){var terrain=new char[width];var forest=new char[width];for(var x=0;x<width;x++){var c=engine.Resolve(x,y);terrain[x]=c.BaseTerrain==BaseTerrain.Water?'W':c.BaseTerrain==BaseTerrain.Mountain?'M':'P';forest[x]=c.Feature==TerrainFeature.Forest?(char)('0'+Math.Clamp((int)Math.Round(c.FeatureDensity*9d),1,9)):'0';}terrainRows.Add(new string(terrain));forestRows.Add(new string(forest));}var mapRoot=new JsonObject{{"schemaVersion",1},{"definitions",new JsonArray{new JsonObject{{"id","base:main_world_surface_map_v1"},{"type","continuousSurfaceWorldMap"},{"name","主世界连续地图表现缓存"},{"compositionId",composition.CompositionId},{"surfaceId",binding.SurfaceId},{"sourceHash",Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(SurfaceAuthoringJson.Serialize(composition)))).ToLowerInvariant()},{"originWorldX",binding.OriginWorldX},{"originWorldY",binding.OriginWorldY},{"cellSize",binding.SurfaceCellWorldSize},{"widthCells",width},{"heightCells",height},{"baseTerrainRows",terrainRows},{"forestRows",forestRows}}}}};
        var mainOut=Path.Combine(outputDirectory,"main_wilderness_surface_v1.candidate.json");var geoOut=Path.Combine(outputDirectory,"w2a_surface_geography_baked_v1.candidate.json");var mapOut=Path.Combine(outputDirectory,"main_world_surface_map_v1.candidate.json");File.WriteAllText(mainOut,mainRoot.ToJsonString(SurfaceAuthoringJson.Options),new System.Text.UTF8Encoding(false));File.WriteAllText(geoOut,geoRoot.ToJsonString(SurfaceAuthoringJson.Options),new System.Text.UTF8Encoding(false));File.WriteAllText(mapOut,mapRoot.ToJsonString(SurfaceAuthoringJson.Options),new System.Text.UTF8Encoding(false));return[mainOut,geoOut,mapOut];
    }

    private static JsonObject Primitive(string id,string kind,List<PathControlPoint> points,double width,RuntimeSurfaceBinding b){var values=new JsonArray();foreach(var p in points){values.Add(b.OriginWorldX+p.X*b.SurfaceCellWorldSize);values.Add(b.OriginWorldY+p.Y*b.SurfaceCellWorldSize);}return new(){{"stableId",id},{"kind",kind},{"worldX",0},{"worldY",0},{"worldWidth",0},{"worldHeight",0},{"strokeWidth",width*b.SurfaceCellWorldSize},{"points",values}};}
    private static void ValidateBlueprintGeometryFidelity(WorldSiteBlueprintDocument blueprint,JsonArray published){var byId=published.OfType<JsonObject>().ToDictionary(x=>Text(x,"stableId"),StringComparer.Ordinal);foreach(var item in blueprint.ObjectPlacements.Where(x=>x.Metadata.TryGetValue("legacyGeometryState",out var state)&&state=="exact")){if(!BlueprintGeometryMigration.TryLegacyRect(item,out var legacy)||!byId.TryGetValue(item.PlacementId,out var candidate)||Math.Abs(Number(candidate,"worldX")-legacy.X)>1e-6||Math.Abs(Number(candidate,"worldY")-legacy.Y)>1e-6||Math.Abs(Number(candidate,"worldWidth")-legacy.Width)>1e-6||Math.Abs(Number(candidate,"worldHeight")-legacy.Height)>1e-6)throw new InvalidDataException($"荒村迁移对象 {item.PlacementId} 的兼容几何发生偏移，发布已取消。");}}
    private static List<PathControlPoint> Points(JsonObject p,double ox,double oy,double cell){var a=p["points"]!.AsArray();var r=new List<PathControlPoint>();for(var i=0;i+1<a.Count;i+=2)r.Add(new(){X=(a[i]!.GetValue<double>()-ox)/cell,Y=(a[i+1]!.GetValue<double>()-oy)/cell});return r;}
    private static string Report(LegacyWorldImportResult r)=>$"当前项目世界导入报告\n\n世界组合：{r.Composition.SurfaceWidthCells}×{r.Composition.SurfaceHeightCells} 连续世界格\nSurface ID：{r.Composition.RuntimeSurface?.SurfaceId}\n导入水域格：{r.WaterCellCount}\n导入道路路径：{r.RoadPathCount}\n导入桥：{r.BridgeCount}\n导入旧阻挡物：{r.LegacyBlockerCount}\n黄村蓝图：{r.HuangcunBlueprint.WidthCells}×{r.HuangcunBlueprint.HeightCells} 连续世界格\n黄村权威放置项：{r.HuangcunSourcePlacementCount}\n黄村对象：{r.HuangcunObjectCount}\n黄村森林覆盖格：{r.HuangcunBlueprint.FineTerrainSources.Count}\n河流 / 水域路径：{r.Composition.Rivers.Count}\n精修块：{r.Composition.DetailPatchPlacements.Count}\n\n未能自动迁移：\n- {string.Join("\n- ",r.Warnings)}\n";
    private static string Need(string root,string relative){var p=Path.Combine(root,relative.Replace('/',Path.DirectorySeparatorChar));if(!File.Exists(p))throw new FileNotFoundException("缺少导入文件："+relative,p);return p;}
    private static JsonObject Definition(string path)=>JsonNode.Parse(File.ReadAllText(path))!["definitions"]![0]!.AsObject();
    private static string Text(JsonObject o,string key)=>o[key]?.GetValue<string>()??string.Empty;private static double Number(JsonObject o,string key)=>o[key]!.GetValue<double>();private static int Integer(JsonObject o,string key)=>o[key]!.GetValue<int>();
    private static string ScalarText(JsonNode value)=>value is JsonValue scalar&&scalar.TryGetValue<string>(out var text)?text:value.ToJsonString();
    private static int CellFloor(double value,double origin,double cell)=>(int)Math.Floor((value-origin)/cell+1e-5);private static int CellCeiling(double value,double origin,double cell)=>(int)Math.Ceiling((value-origin)/cell-1e-5);private static double Cell(double value,double origin,double cell)=>(value-origin)/cell;
}
