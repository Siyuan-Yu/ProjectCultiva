using SurfaceAuthoring.Core;
using SurfaceAuthoring.EditorCommon;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WorldComposer;

public enum ComposerTool { Select, Terrain, Forest, River, Road, WorldObject, Blueprint, DetailPatch, RectangleSelect }

public sealed class WorldComposerCanvas : SurfaceCanvasBase
{
    private static readonly Brush BlueprintBrush=FrozenBrush(Color.FromArgb(80,70,160,255));
    private static readonly Brush PatchBrush=FrozenBrush(Color.FromArgb(70,255,190,70));
    private static readonly Brush ObjectBrush=FrozenBrush(Color.FromArgb(150,255,200,30));
    private static readonly Brush SelectionBrush=FrozenBrush(Color.FromArgb(45,70,160,255));
    private static readonly Brush HoverBrush=FrozenBrush(Color.FromArgb(45,255,255,255));
    private static readonly Pen BlueprintPen=FrozenPen(Brushes.DeepSkyBlue,2);
    private static readonly Pen PatchPen=FrozenPen(Brushes.Orange,2);
    private static readonly Pen ObjectPen=FrozenPen(Brushes.Gold,1);
    private static readonly Pen SelectionPen=FrozenPen(Brushes.DodgerBlue,2);
    private static readonly Pen HoverPen=FrozenPen(Brushes.White,1);
    private static readonly Pen GridPen=FrozenPen(FrozenBrush(Color.FromArgb(60,255,255,255)),.5);
    private static readonly Pen ChunkPen=FrozenPen(Brushes.Magenta,1);
    private static readonly Pen ControlPointPen=FrozenPen(Brushes.Black,1);
    private static readonly Dictionary<(int Kind,int Width),Pen> PathPens=[];

    public WorldCompositionDocument Document { get; private set; }=new();
    public LinkedSourceSet Linked { get; private set; }=new();
    public ComposerTool Tool { get; set; }
    public BaseTerrain BaseTerrain { get; set; }=BaseTerrain.Plain;
    public TerrainFeature Feature { get; set; }=TerrainFeature.Forest;
    public double FeatureDensity { get; set; }=.75;
    public int BrushSize { get; set; }=1;
    public RoadClass RoadClass { get; set; }=RoadClass.SmallRoad;
    public double RiverWidth { get; set; }=5;
    public bool ActiveBaseLayer { get; set; }=true;
    public bool EraseMode { get; set; }
    public string WorldObjectKindId { get; set; }="treeM";
    public bool ShowEditorGrid { get; set; }=true;
    public bool ShowRuntimeChunkOverlay { get; set; }
    public Rect? Selection { get; private set; }
    public object? SelectedItem { get; private set; }
    public PathControlPoint? SelectedControlPoint => _selectedPoint;
    public CrossingSource? SelectedCrossing { get; private set; }
    public bool CanRemoveSelectedControlPoint => _selectedPoint != null && SelectedItem is RiverPathSource river && river.ControlPoints.Count > 2 || _selectedPoint != null && SelectedItem is RoadPathSource road && road.ControlPoints.Count > 2;
    public WorldSiteBlueprintPlacement? PendingBlueprint { get; set; }
    public DetailPatchPlacement? PendingPatch { get; set; }
    public event Action? EditStarted;
    public event Action? EditCompleted;
    public event Action<object?>? SelectionChanged;
    public event Action<string>? PreviewStatusChanged;
    public event Action<string>? UserNotice;

    private readonly Dictionary<long,CoarseTerrainSourceCell> _terrainIndex=[];
    private readonly Dictionary<long,CoarseFeatureSourceCell> _featureIndex=[];
    private readonly List<PathControlPoint> _pendingPath=[];
    private bool _editing,_mutationFull,_mutationChanged,_coarseChanged;
    private (int X,int Y)? _lastBrush,_rectStart;
    private (PathControlPoint Point,double X,double Y)? _dragPoint;
    private PathControlPoint? _selectedPoint;
    private (object Item,int Dx,int Dy,int StartX,int StartY)? _dragPlacement;
    private (int X,int Y) _hover=(-1,-1);
    private PreviewRect? _dirtySurface;
    private int? _selectedSegment;
    private WriteableBitmap? _preview;
    private byte[]? _previewPixels;
    private CompositionEngine? _preparedComposition;
    private CancellationTokenSource? _previewCancellation;
    private int _previewRevision;

    public WorldComposerCanvas(){RenderOptions.SetBitmapScalingMode(this,BitmapScalingMode.NearestNeighbor);SnapsToDevicePixels=true;}

    public void SetDocument(WorldCompositionDocument document,LinkedSourceSet linked)
    {
        var changed=!ReferenceEquals(Document,document)||!ReferenceEquals(Linked,linked)||SurfaceWidth!=document.SurfaceWidthCells||SurfaceHeight!=document.SurfaceHeightCells;
        Document=document;Linked=linked;SetSurfaceSize(document.SurfaceWidthCells,document.SurfaceHeightCells);
        if(changed){SelectedItem=null;Selection=null;_pendingPath.Clear();_selectedPoint=null;RebuildSourceIndexes();RequestPreview(null);}
        InvalidateVisual();
    }

    public void CancelOperation(){if(_editing&&_mutationChanged)EndMutation();_pendingPath.Clear();PendingBlueprint=null;PendingPatch=null;_editing=false;_dragPoint=null;_dragPlacement=null;InvalidateVisual();}
    public void FinishPath(){if(_pendingPath.Count<2){CancelOperation();return;}BeginMutation(true);if(Tool==ComposerTool.River)Document.Rivers.Add(new(){PathId=Unique("river",Document.Rivers.Select(x=>x.PathId)),WidthCells=RiverWidth,ControlPoints=_pendingPath.Select(Clone).ToList()});else Document.Roads.Add(new(){PathId=Unique("road",Document.Roads.Select(x=>x.PathId)),RoadClass=RoadClass,ControlPoints=_pendingPath.Select(Clone).ToList()});_pendingPath.Clear();_mutationChanged=true;EndMutation();}
    public void DeleteSelected(){if(SelectedItem==null){UserNotice?.Invoke("当前没有可删除的选择。");return;}BeginMutation(true);switch(SelectedItem){case RiverPathSource x:Document.Rivers.Remove(x);break;case RoadPathSource x:Document.Roads.Remove(x);break;case WorldObjectPlacement x:Document.WorldObjectPlacements.Remove(x);break;case WorldSiteBlueprintPlacement x:Document.WorldSiteBlueprintPlacements.Remove(x);break;case DetailPatchPlacement x:Document.DetailPatchPlacements.Remove(x);break;}_mutationChanged=true;SelectedItem=null;SelectedCrossing=null;SelectionChanged?.Invoke(null);EndMutation();}
    public void RotateSelected(){if(SelectedItem is not WorldSiteBlueprintPlacement&&SelectedItem is not WorldObjectPlacement)return;BeginMutation(true);if(SelectedItem is WorldSiteBlueprintPlacement p)p.RotationQuarterTurns=(p.RotationQuarterTurns+1)%4;else{var o=(WorldObjectPlacement)SelectedItem;o.RotationQuarterTurns=(o.RotationQuarterTurns+1)%4;}_mutationChanged=true;EndMutation();}
    public void RemoveSelectedControlPoint(){if(!CanRemoveSelectedControlPoint){UserNotice?.Invoke("请先选择一条至少有 3 个控制点的路径中的控制点。");return;}var p=_selectedPoint!;BeginMutation(true);var points=SelectedItem is RiverPathSource river?river.ControlPoints:((RoadPathSource)SelectedItem!).ControlPoints;_mutationChanged=points.Remove(p);_selectedPoint=null;EndMutation();}
    public void InsertControlPoint(){if(SelectedItem is not RiverPathSource&&SelectedItem is not RoadPathSource){UserNotice?.Invoke("请先选择道路或河流。");return;}var points=SelectedItem is RiverPathSource r?r.ControlPoints:((RoadPathSource)SelectedItem).ControlPoints;if(points.Count<2){UserNotice?.Invoke("路径至少需要两个控制点。");return;}var selected=_selectedPoint==null?Math.Clamp(_selectedSegment??(points.Count-2),0,points.Count-2):Math.Clamp(points.IndexOf(_selectedPoint),0,points.Count-2);var next=selected+1;BeginMutation(true);var p=new PathControlPoint{X=(points[selected].X+points[next].X)/2,Y=(points[selected].Y+points[next].Y)/2};points.Insert(next,p);_selectedPoint=p;_selectedSegment=next;_mutationChanged=true;EndMutation();}
    public void ApplySelectedPath(double width,RoadClass roadClass){if(SelectedItem is not RiverPathSource&&SelectedItem is not RoadPathSource)return;BeginMutation(true);if(SelectedItem is RiverPathSource r){_mutationChanged=r.WidthCells!=width;r.WidthCells=width;}else{var road=(RoadPathSource)SelectedItem;_mutationChanged=road.RoadClass!=roadClass;road.RoadClass=roadClass;}EndMutation();}
    public void ApplySelectedControlPoint(double x,double y){if(_selectedPoint==null){UserNotice?.Invoke("请先选择一个控制点。");return;}BeginMutation(true);_mutationChanged=_selectedPoint.X!=x||_selectedPoint.Y!=y;_selectedPoint.X=x;_selectedPoint.Y=y;EndMutation();}
    public void ApplySelectedPlacement(int x,int y){if(SelectedItem is not WorldSiteBlueprintPlacement&&SelectedItem is not DetailPatchPlacement&&SelectedItem is not WorldObjectPlacement)return;BeginMutation(true);if(SelectedItem is WorldSiteBlueprintPlacement b){_mutationChanged=b.SurfaceCellX!=x||b.SurfaceCellY!=y;b.SurfaceCellX=x;b.SurfaceCellY=y;}else if(SelectedItem is DetailPatchPlacement p){_mutationChanged=p.SurfaceCellX!=x||p.SurfaceCellY!=y;p.SurfaceCellX=x;p.SurfaceCellY=y;}else{var o=(WorldObjectPlacement)SelectedItem;_mutationChanged=o.WorldSurfaceX!=x||o.WorldSurfaceY!=y;o.WorldSurfaceX=x;o.WorldSurfaceY=y;}EndMutation();}
    public void ResolveSelectedCrossing(CrossingResolution resolution){if(SelectedCrossing==null){UserNotice?.Invoke("请先用选择工具点击道路与河流的交叉标记。");return;}var crossing=SelectedCrossing;BeginMutation(true);Document.Crossings.RemoveAll(x=>x.RiverPathId==crossing.RiverPathId&&x.RoadPathId==crossing.RoadPathId);Document.WorldObjectPlacements.RemoveAll(o=>o.KindId=="bridge"&&o.Metadata.TryGetValue("riverPathId",out var river)&&river==crossing.RiverPathId&&o.Metadata.TryGetValue("roadPathId",out var road)&&road==crossing.RoadPathId);crossing.Resolution=resolution;Document.Crossings.Add(crossing);if(resolution==CrossingResolution.Bridge){var id=Unique("bridge",Document.WorldObjectPlacements.Select(x=>x.PlacementId));Document.WorldObjectPlacements.Add(new(){PlacementId=id,KindId="bridge",WorldSurfaceX=(int)Math.Floor(crossing.X),WorldSurfaceY=(int)Math.Floor(crossing.Y),WidthCells=3,HeightCells=2,Metadata=new(StringComparer.Ordinal){{"riverPathId",crossing.RiverPathId},{"roadPathId",crossing.RoadPathId}}});}_mutationChanged=true;EndMutation();SelectionChanged?.Invoke(SelectedCrossing);}
    public void FillSelectionOrRegion(){if(!Selection.HasValue&&_hover.X<0)return;var cells=Selection.HasValue?CellsInSelection():ConnectedRegion(_hover.X/10,_hover.Y/10);if(cells.Count==0)return;BeginMutation(false);foreach(var c in cells)PaintOne(c.X,c.Y);EndMutation();}
    public void ClearSelection(){Selection=null;InvalidateVisual();}
    public void RebuildPreview()=>RequestPreview(null);

    protected override bool BeginEdit(MouseButtonEventArgs e,(double X,double Y) w)
    {
        if(e.ChangedButton!=MouseButton.Left||w.X<0||w.Y<0||w.X>=SurfaceWidth||w.Y>=SurfaceHeight)return false;
        var sx=(int)Math.Floor(w.X);var sy=(int)Math.Floor(w.Y);_hover=(sx,sy);
        if(Tool is ComposerTool.Terrain or ComposerTool.Forest){BeginMutation(false);_editing=true;PaintInterpolated(sx/10,sy/10);return true;}
        if(Tool==ComposerTool.RectangleSelect){_rectStart=(sx,sy);Selection=new(sx,sy,1,1);_editing=true;return true;}
        if(Tool is ComposerTool.River or ComposerTool.Road){if(e.ClickCount>=2){FinishPath();return true;}_pendingPath.Add(new(){X=w.X,Y=w.Y});InvalidateVisual();return true;}
        if(Tool==ComposerTool.WorldObject){BeginMutation(true);var item=new WorldObjectPlacement{PlacementId=Unique(WorldObjectKindId,Document.WorldObjectPlacements.Select(x=>x.PlacementId)),KindId=WorldObjectKindId,WorldSurfaceX=sx,WorldSurfaceY=sy};Document.WorldObjectPlacements.Add(item);SelectedItem=item;_mutationChanged=true;EndMutation();SelectionChanged?.Invoke(item);return true;}
        if(Tool==ComposerTool.Blueprint&&PendingBlueprint!=null){BeginMutation(true);PendingBlueprint.SurfaceCellX=sx;PendingBlueprint.SurfaceCellY=sy;Document.WorldSiteBlueprintPlacements.Add(PendingBlueprint);SelectedItem=PendingBlueprint;PendingBlueprint=null;_mutationChanged=true;EndMutation();SelectionChanged?.Invoke(SelectedItem);return true;}
        if(Tool==ComposerTool.DetailPatch&&PendingPatch!=null){BeginMutation(true);PendingPatch.SurfaceCellX=sx;PendingPatch.SurfaceCellY=sy;Document.DetailPatchPlacements.Add(PendingPatch);SelectedItem=PendingPatch;PendingPatch=null;_mutationChanged=true;EndMutation();SelectionChanged?.Invoke(SelectedItem);return true;}
        return Tool==ComposerTool.Select&&BeginSelection(w.X,w.Y);
    }

    protected override void ContinueEdit(MouseEventArgs e,(double X,double Y) w)
    {
        var sx=(int)Math.Floor(w.X);var sy=(int)Math.Floor(w.Y);var hoverChanged=_hover!=(sx,sy);_hover=(sx,sy);var visualChanged=false;
        if(_editing&&e.LeftButton==MouseButtonState.Pressed&&Tool is ComposerTool.Terrain or ComposerTool.Forest){PaintInterpolated(sx/10,sy/10);visualChanged=true;}
        if(_editing&&_rectStart.HasValue&&e.LeftButton==MouseButtonState.Pressed){var a=_rectStart.Value;var next=Normalize(a.X,a.Y,sx,sy);if(Selection!=next){Selection=next;visualChanged=true;}}
        if(_dragPoint.HasValue&&e.LeftButton==MouseButtonState.Pressed){var nx=Math.Clamp(w.X,0,SurfaceWidth);var ny=Math.Clamp(w.Y,0,SurfaceHeight);if(_dragPoint.Value.Point.X!=nx||_dragPoint.Value.Point.Y!=ny){_dragPoint.Value.Point.X=nx;_dragPoint.Value.Point.Y=ny;_mutationChanged=true;visualChanged=true;}}
        if(_dragPlacement.HasValue&&e.LeftButton==MouseButtonState.Pressed){var d=_dragPlacement.Value;var nx=sx-d.Dx;var ny=sy-d.Dy;if(d.Item is WorldSiteBlueprintPlacement b&&(b.SurfaceCellX!=nx||b.SurfaceCellY!=ny)){b.SurfaceCellX=nx;b.SurfaceCellY=ny;_mutationChanged=true;visualChanged=true;}else if(d.Item is DetailPatchPlacement p&&(p.SurfaceCellX!=nx||p.SurfaceCellY!=ny)){p.SurfaceCellX=nx;p.SurfaceCellY=ny;_mutationChanged=true;visualChanged=true;}else if(d.Item is WorldObjectPlacement o&&(o.WorldSurfaceX!=nx||o.WorldSurfaceY!=ny)){o.WorldSurfaceX=nx;o.WorldSurfaceY=ny;_mutationChanged=true;visualChanged=true;}}
        if(hoverChanged&&Tool is ComposerTool.Terrain or ComposerTool.Forest)visualChanged=true;
        if(visualChanged)InvalidateVisual();
    }

    protected override void EndEdit(MouseButtonEventArgs e,(double X,double Y) w)
    {
        if(_editing&&Tool is ComposerTool.Terrain or ComposerTool.Forest){_editing=false;_lastBrush=null;EndMutation();}
        else if(_editing&&_rectStart.HasValue){_editing=false;_rectStart=null;InvalidateVisual();}
        if(_dragPoint.HasValue||_dragPlacement.HasValue){_dragPoint=null;_dragPlacement=null;EndMutation();}
    }

    protected override void DrawSurface(DrawingContext dc)
    {
        if(_preview!=null)dc.DrawImage(_preview,WorldRect(0,0,SurfaceWidth,SurfaceHeight));
        DrawForestMarkers(dc);
        foreach(var o in Document.WorldObjectPlacements){dc.DrawRectangle(ObjectBrush,ObjectPen,WorldRect(o.WorldSurfaceX,o.WorldSurfaceY,o.WidthCells,o.HeightCells));DrawLabel(dc,ObjectText(o.KindId),o.WorldSurfaceX,o.WorldSurfaceY+o.HeightCells);}
        foreach(var p in Document.WorldSiteBlueprintPlacements){var missing=!Linked.Blueprints.TryGetValue(p.PlacementId,out var b);var w=b?.WidthCells??20;var h=b?.HeightCells??20;if(p.RotationQuarterTurns%2!=0)(w,h)=(h,w);dc.DrawRectangle(missing?Brushes.IndianRed:BlueprintBrush,BlueprintPen,WorldRect(p.SurfaceCellX,p.SurfaceCellY,w,h));DrawLabel(dc,p.BlueprintId,p.SurfaceCellX,p.SurfaceCellY+h);if(b!=null)foreach(var o in b.ObjectPlacements){var q=RotateLocal(o.LocalSurfaceX,o.LocalSurfaceY,o.WidthCells,o.HeightCells,b.WidthCells,b.HeightCells,p.RotationQuarterTurns);dc.DrawRectangle(ObjectBrush,ObjectPen,WorldRect(p.SurfaceCellX+q.X,p.SurfaceCellY+q.Y,q.Width,q.Height));}}
        foreach(var p in Document.DetailPatchPlacements){var missing=!Linked.Patches.TryGetValue(p.PlacementId,out var d);dc.DrawRectangle(missing?Brushes.IndianRed:PatchBrush,PatchPen,WorldRect(p.SurfaceCellX,p.SurfaceCellY,d?.WidthCells??12,d?.HeightCells??12));}
        DrawPaths(dc);DrawOverlays(dc);
        if(Selection.HasValue)dc.DrawRectangle(SelectionBrush,SelectionPen,WorldRect(Selection.Value.X,Selection.Value.Y,Selection.Value.Width,Selection.Value.Height));
        if(_hover.X>=0&&Tool is ComposerTool.Terrain or ComposerTool.Forest){var ex=_hover.X/10;var ey=_hover.Y/10;var startX=ex-BrushSize/2;var startY=ey-BrushSize/2;dc.DrawRectangle(HoverBrush,HoverPen,WorldRect(startX*10,startY*10,BrushSize*10,BrushSize*10));}
    }

    private void DrawPaths(DrawingContext dc){foreach(var p in Document.Rivers)DrawPath(dc,p.ControlPoints,Brushes.DodgerBlue,p.WidthCells,p==SelectedItem);foreach(var p in Document.Roads)DrawPath(dc,p.ControlPoints,Brushes.Peru,RoadWidths.GetWidth(p.RoadClass),p==SelectedItem);if(_pendingPath.Count>0)DrawPath(dc,_pendingPath,Tool==ComposerTool.River?Brushes.DodgerBlue:Brushes.Peru,Tool==ComposerTool.River?RiverWidth:RoadWidths.GetWidth(RoadClass),true);foreach(var crossing in CompositionEngine.FindCrossings(Document)){var center=WorldToScreen(crossing.X,crossing.Y);var brush=crossing.Resolution switch{CrossingResolution.Bridge=>Brushes.Gold,CrossingResolution.Ford=>Brushes.LightSkyBlue,_=>Brushes.OrangeRed};dc.DrawEllipse(brush,ControlPointPen,center,6,6);}}
    private void DrawForestMarkers(DrawingContext dc)
    {
        if(Scale<4||_preparedComposition==null)return;var left=Math.Clamp((int)Math.Floor(-Offset.X/Scale),0,SurfaceWidth);var right=Math.Clamp((int)Math.Ceiling((ActualWidth-Offset.X)/Scale),0,SurfaceWidth);var bottom=Math.Clamp((int)Math.Floor(SurfaceHeight-(ActualHeight-Offset.Y)/Scale),0,SurfaceHeight);var top=Math.Clamp((int)Math.Ceiling(SurfaceHeight+Offset.Y/Scale),0,SurfaceHeight);if((right-left)*(top-bottom)>25000)return;
        for(var y=bottom;y<top;y++)for(var x=left;x<right;x++){var cell=_preparedComposition.Resolve(x,y);if(cell.Feature!=TerrainFeature.Forest)continue;var h=unchecked((uint)(x*73856093)^(uint)(y*19349663)^(uint)Document.WorldSeed);var dx=((h&255)/255d-.5)*.35;var dy=(((h>>8)&255)/255d-.5)*.35;var center=WorldToScreen(x+.5+dx,y+.5+dy);var radius=Math.Clamp(Scale*.18,1.2,4);dc.DrawEllipse(Brushes.LightGreen,null,center,radius,radius);}
    }
    private void DrawPath(DrawingContext dc,List<PathControlPoint> points,Brush brush,double width,bool selected){var key=(ReferenceEquals(brush,Brushes.DodgerBlue)?0:1,Math.Max(10,(int)Math.Round(width*Scale*10)));if(!PathPens.TryGetValue(key,out var pen)){pen=new Pen(brush,key.Item2/10d){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round};pen.Freeze();PathPens[key]=pen;}foreach(var s in CompositionEngine.SmoothSegments(points))dc.DrawLine(pen,WorldToScreen(s.A.X,s.A.Y),WorldToScreen(s.B.X,s.B.Y));if(selected)foreach(var p in points)dc.DrawEllipse(Brushes.White,ControlPointPen,WorldToScreen(p.X,p.Y),5,5);}
    private void DrawOverlays(DrawingContext dc){if(ShowEditorGrid){for(var x=0;x<=SurfaceWidth;x+=10)dc.DrawLine(GridPen,WorldToScreen(x,0),WorldToScreen(x,SurfaceHeight));for(var y=0;y<=SurfaceHeight;y+=10)dc.DrawLine(GridPen,WorldToScreen(0,y),WorldToScreen(SurfaceWidth,y));}if(ShowRuntimeChunkOverlay){for(var x=0;x<=SurfaceWidth;x+=50)dc.DrawLine(ChunkPen,WorldToScreen(x,0),WorldToScreen(x,SurfaceHeight));for(var y=0;y<=SurfaceHeight;y+=50)dc.DrawLine(ChunkPen,WorldToScreen(0,y),WorldToScreen(SurfaceWidth,y));}}

    private bool BeginSelection(double x,double y)
    {
        var crossing=CompositionEngine.FindCrossings(Document).OrderBy(c=>Distance(c.X,c.Y,x,y)).FirstOrDefault(c=>Distance(c.X,c.Y,x,y)<=Math.Max(5,10/Scale));if(crossing!=null){SelectedItem=null;SelectedCrossing=crossing;_selectedPoint=null;_selectedSegment=null;SelectionChanged?.Invoke(crossing);InvalidateVisual();return true;}
        foreach(var path in Document.Rivers.Cast<object>().Concat(Document.Roads)){var points=path is RiverPathSource r?r.ControlPoints:((RoadPathSource)path).ControlPoints;var point=points.FirstOrDefault(p=>Math.Sqrt((p.X-x)*(p.X-x)+(p.Y-y)*(p.Y-y))<Math.Max(4,7/Scale));if(point!=null){SelectedItem=path;SelectedCrossing=null;_selectedPoint=point;_dragPoint=(point,point.X,point.Y);BeginMutation(true);SelectionChanged?.Invoke(path);InvalidateVisual();return true;}}
        var hit=Document.Rivers.Cast<object>().Concat(Document.Roads).Select(path=>new{Path=path,Distance=CompositionEngine.DistanceToPath(x,y,path is RiverPathSource river?river.ControlPoints:((RoadPathSource)path).ControlPoints,path is RiverPathSource riverWidth?riverWidth.WidthCells:RoadWidths.GetWidth(((RoadPathSource)path).RoadClass))}).OrderBy(x=>x.Distance).FirstOrDefault();if(hit!=null&&hit.Distance<=Math.Max(5,10/Scale)){SelectedItem=hit.Path;SelectedCrossing=null;_selectedPoint=null;_selectedSegment=NearestSegment(hit.Path,x,y);SelectionChanged?.Invoke(hit.Path);InvalidateVisual();return true;}
        foreach(var p in Document.WorldObjectPlacements.Cast<object>().Concat(Document.WorldSiteBlueprintPlacements).Concat(Document.DetailPatchPlacements)){var px=p switch{WorldObjectPlacement o=>o.WorldSurfaceX,WorldSiteBlueprintPlacement b=>b.SurfaceCellX,_=>((DetailPatchPlacement)p).SurfaceCellX};var py=p switch{WorldObjectPlacement o=>o.WorldSurfaceY,WorldSiteBlueprintPlacement b=>b.SurfaceCellY,_=>((DetailPatchPlacement)p).SurfaceCellY};var size=PlacementSize(p);if(x>=px&&y>=py&&x<px+size.Width&&y<py+size.Height){SelectedItem=p;SelectedCrossing=null;_selectedPoint=null;_dragPlacement=(p,(int)x-px,(int)y-py,px,py);BeginMutation(true);SelectionChanged?.Invoke(p);InvalidateVisual();return true;}}
        SelectedItem=null;SelectedCrossing=null;_selectedPoint=null;_selectedSegment=null;SelectionChanged?.Invoke(null);InvalidateVisual();return true;
    }

    private void PaintInterpolated(int x,int y){if(_lastBrush is not{}old){PaintArea(x,y);_lastBrush=(x,y);return;}var n=Math.Max(Math.Abs(x-old.X),Math.Abs(y-old.Y));for(var i=1;i<=Math.Max(1,n);i++)PaintArea(old.X+(x-old.X)*i/Math.Max(1,n),old.Y+(y-old.Y)*i/Math.Max(1,n));_lastBrush=(x,y);}
    private void PaintArea(int x,int y){var startX=x-BrushSize/2;var startY=y-BrushSize/2;for(var yy=startY;yy<startY+BrushSize;yy++)for(var xx=startX;xx<startX+BrushSize;xx++)if(xx>=0&&yy>=0&&xx<Document.WorldEditorGridWidth&&yy<Document.WorldEditorGridHeight)PaintOne(xx,yy);InvalidateVisual();}
    private void PaintOne(int x,int y){var key=Key(x,y);var changed=false;if(ActiveBaseLayer){if(EraseMode||BaseTerrain==Document.DefaultBaseTerrain)changed=_terrainIndex.Remove(key);else if(!_terrainIndex.TryGetValue(key,out var old)||old.BaseTerrain!=BaseTerrain){_terrainIndex[key]=new(){EditorCellX=x,EditorCellY=y,BaseTerrain=BaseTerrain};changed=true;}if(BaseTerrain==BaseTerrain.Water)changed=_featureIndex.Remove(key)||changed;}else{var terrain=_terrainIndex.TryGetValue(key,out var source)?source.BaseTerrain:Document.DefaultBaseTerrain;if(!EraseMode&&Feature==TerrainFeature.Forest&&terrain==BaseTerrain.Water){UserNotice?.Invoke("水域不能生成森林。");return;}if(EraseMode||Feature==TerrainFeature.None)changed=_featureIndex.Remove(key);else if(!_featureIndex.TryGetValue(key,out var old)||old.Feature!=Feature||old.Density!=FeatureDensity){_featureIndex[key]=new(){EditorCellX=x,EditorCellY=y,Feature=Feature,Density=FeatureDensity};changed=true;}}if(changed){_mutationChanged=true;_coarseChanged=true;MarkDirtyEditorCell(x,y);}}
    private List<(int X,int Y)> CellsInSelection(){var r=Selection!.Value;var list=new List<(int,int)>();for(var y=(int)r.Y/10;y<(int)Math.Ceiling(r.Bottom/10);y++)for(var x=(int)r.X/10;x<(int)Math.Ceiling(r.Right/10);x++)list.Add((x,y));return list;}
    private List<(int X,int Y)> ConnectedRegion(int sx,int sy){if(sx<0||sy<0)return[];string Value(int x,int y)=>ActiveBaseLayer?_terrainIndex.TryGetValue(Key(x,y),out var t)?t.BaseTerrain.ToString():Document.DefaultBaseTerrain.ToString():_featureIndex.TryGetValue(Key(x,y),out var f)?f.Feature.ToString():"None";var target=Value(sx,sy);var seen=new HashSet<(int,int)>();var q=new Queue<(int,int)>();q.Enqueue((sx,sy));while(q.Count>0){var p=q.Dequeue();if(!seen.Add(p)||p.Item1<0||p.Item2<0||p.Item1>=Document.WorldEditorGridWidth||p.Item2>=Document.WorldEditorGridHeight)continue;if(Value(p.Item1,p.Item2)!=target){seen.Remove(p);continue;}q.Enqueue((p.Item1+1,p.Item2));q.Enqueue((p.Item1-1,p.Item2));q.Enqueue((p.Item1,p.Item2+1));q.Enqueue((p.Item1,p.Item2-1));}return seen.ToList();}
    private (int Width,int Height) PlacementSize(object p){if(p is WorldObjectPlacement o)return(o.WidthCells,o.HeightCells);if(p is WorldSiteBlueprintPlacement b&&Linked.Blueprints.TryGetValue(b.PlacementId,out var bp)){var w=bp.WidthCells;var h=bp.HeightCells;if(b.RotationQuarterTurns%2!=0)(w,h)=(h,w);return(w,h);}if(p is DetailPatchPlacement d&&Linked.Patches.TryGetValue(d.PlacementId,out var patch))return(patch.WidthCells,patch.HeightCells);return(20,20);}

    private void BeginMutation(bool full){_mutationFull=full;_mutationChanged=false;_coarseChanged=false;_dirtySurface=null;EditStarted?.Invoke();}
    private void EndMutation(){if(_coarseChanged)SynchronizeCoarseLists();EditCompleted?.Invoke();if(_mutationChanged)RequestPreview(_mutationFull?null:_dirtySurface);InvalidateVisual();}
    private void RebuildSourceIndexes(){_terrainIndex.Clear();foreach(var c in Document.CoarseTerrainSources)_terrainIndex[Key(c.EditorCellX,c.EditorCellY)]=c;_featureIndex.Clear();foreach(var c in Document.CoarseFeatureSources)_featureIndex[Key(c.EditorCellX,c.EditorCellY)]=c;}
    private void SynchronizeCoarseLists(){Document.CoarseTerrainSources=_terrainIndex.Values.OrderBy(x=>x.EditorCellY).ThenBy(x=>x.EditorCellX).ToList();Document.CoarseFeatureSources=_featureIndex.Values.OrderBy(x=>x.EditorCellY).ThenBy(x=>x.EditorCellX).ToList();}
    private void MarkDirtyEditorCell(int x,int y){var r=ClipRegion(new((x-1)*10,(y-1)*10,30,30));_dirtySurface=_dirtySurface.HasValue?Union(_dirtySurface.Value,r):r;}

    private async void RequestPreview(PreviewRect? requested)
    {
        var revision=0;
        try
        {
            var region=requested.HasValue?ClipRegion(requested.Value):new PreviewRect(0,0,Document.SurfaceWidthCells,Document.SurfaceHeightCells);if(region.Width<=0||region.Height<=0)return;
            revision=++_previewRevision;_previewCancellation?.Cancel();var cancellation=new CancellationTokenSource();_previewCancellation=cancellation;
            var source=SurfaceAuthoringJson.Clone(Document);var linked=CloneLinked(Linked);var isFull=!requested.HasValue||_preview==null||_preview.PixelWidth!=source.SurfaceWidthCells||_preview.PixelHeight!=source.SurfaceHeightCells;if(isFull)region=new(0,0,source.SurfaceWidthCells,source.SurfaceHeightCells);
            PreviewStatusChanged?.Invoke("正在生成世界预览…");
            var result=await Task.Run(()=>BuildPreview(source,linked,region,cancellation.Token),cancellation.Token);
            if(cancellation.IsCancellationRequested||revision!=_previewRevision)return;
            _preparedComposition=result.Engine;
            if(isFull){_previewPixels=result.Pixels;_preview=new(source.SurfaceWidthCells,source.SurfaceHeightCells,96,96,PixelFormats.Bgra32,null);_preview.WritePixels(new(0,0,source.SurfaceWidthCells,source.SurfaceHeightCells),result.Pixels,source.SurfaceWidthCells*4,0);}
            else{_preview!.WritePixels(new(region.X,source.SurfaceHeightCells-region.Y-region.Height,region.Width,region.Height),result.Pixels,region.Width*4,0);CopyRegionIntoFullBuffer(result.Pixels,region,source.SurfaceWidthCells,source.SurfaceHeightCells);}
            PreviewStatusChanged?.Invoke("世界预览已更新");InvalidateVisual();
        }
        catch(OperationCanceledException){}
        catch(Exception ex){if(revision==0||revision==_previewRevision)ReportPreviewFailure(ex);}
    }

    private void ReportPreviewFailure(Exception ex){try{PreviewStatusChanged?.Invoke("世界预览生成失败："+ex.Message);}catch{}try{InvalidateVisual();}catch{}}

    private static PreviewResult BuildPreview(WorldCompositionDocument source,LinkedSourceSet linked,PreviewRect region,CancellationToken token){var engine=new CompositionEngine(source,linked);var pixels=new byte[region.Width*region.Height*4];for(var row=0;row<region.Height;row++){token.ThrowIfCancellationRequested();var y=region.Y+region.Height-1-row;for(var col=0;col<region.Width;col++)WritePixel(pixels,(row*region.Width+col)*4,engine.Resolve(region.X+col,y));}return new(engine,pixels);}
    private void CopyRegionIntoFullBuffer(byte[] regionPixels,PreviewRect region,int fullWidth,int fullHeight){if(_previewPixels==null||_previewPixels.Length!=fullWidth*fullHeight*4)return;var top=fullHeight-region.Y-region.Height;for(var row=0;row<region.Height;row++)Buffer.BlockCopy(regionPixels,row*region.Width*4,_previewPixels,((top+row)*fullWidth+region.X)*4,region.Width*4);}
    private static void WritePixel(byte[] pixels,int i,ResolvedSurfaceCell c){var color=ColorFor(c);pixels[i]=color.B;pixels[i+1]=color.G;pixels[i+2]=color.R;pixels[i+3]=255;}
    private static Color ColorFor(ResolvedSurfaceCell c){if(c.Road.HasValue)return Color.FromRgb(170,125,70);if(c.Feature==TerrainFeature.Forest)return Color.FromRgb(36,100,55);return c.BaseTerrain switch{BaseTerrain.Plain=>Color.FromRgb(94,145,72),BaseTerrain.Mountain=>Color.FromRgb(110,110,115),BaseTerrain.Water=>Color.FromRgb(45,105,170),_=>Colors.Gray};}
    private static LinkedSourceSet CloneLinked(LinkedSourceSet source){var clone=new LinkedSourceSet();foreach(var p in source.Blueprints)clone.Blueprints[p.Key]=SurfaceAuthoringJson.Clone(p.Value);foreach(var p in source.Patches)clone.Patches[p.Key]=SurfaceAuthoringJson.Clone(p.Value);clone.Issues.AddRange(source.Issues);return clone;}
    private PreviewRect ClipRegion(PreviewRect r){var x=Math.Clamp(r.X,0,SurfaceWidth);var y=Math.Clamp(r.Y,0,SurfaceHeight);var right=Math.Clamp(r.X+r.Width,0,SurfaceWidth);var top=Math.Clamp(r.Y+r.Height,0,SurfaceHeight);return new(x,y,Math.Max(0,right-x),Math.Max(0,top-y));}
    private static PreviewRect Union(PreviewRect a,PreviewRect b){var x=Math.Min(a.X,b.X);var y=Math.Min(a.Y,b.Y);var right=Math.Max(a.X+a.Width,b.X+b.Width);var top=Math.Max(a.Y+a.Height,b.Y+b.Height);return new(x,y,right-x,top-y);}
    private static double Distance(double ax,double ay,double bx,double by)=>Math.Sqrt((ax-bx)*(ax-bx)+(ay-by)*(ay-by));
    private static int NearestSegment(object path,double x,double y)
    {
        var points=path is RiverPathSource river?river.ControlPoints:((RoadPathSource)path).ControlPoints;
        var best=0;var distance=double.MaxValue;
        for(var i=0;i<points.Count-1;i++){var d=DistanceToSegment(x,y,points[i],points[i+1]);if(d<distance){distance=d;best=i;}}
        return best;
    }
    private static double DistanceToSegment(double x,double y,PathControlPoint a,PathControlPoint b)
    {
        var dx=b.X-a.X;var dy=b.Y-a.Y;var length=dx*dx+dy*dy;if(length<=0)return Distance(x,y,a.X,a.Y);
        var t=Math.Clamp(((x-a.X)*dx+(y-a.Y)*dy)/length,0,1);return Distance(x,y,a.X+t*dx,a.Y+t*dy);
    }
    private static long Key(int x,int y)=>((long)y<<32)|(uint)x;
    private static Rect Normalize(int x1,int y1,int x2,int y2)=>new(Math.Min(x1,x2),Math.Min(y1,y2),Math.Abs(x2-x1)+1,Math.Abs(y2-y1)+1);
    private static PathControlPoint Clone(PathControlPoint p)=>new(){X=p.X,Y=p.Y,WidthCells=p.WidthCells};
    private static string Unique(string prefix,IEnumerable<string> ids){var set=ids.ToHashSet(StringComparer.Ordinal);for(var i=1;;i++){var id=$"{prefix}_{i:000}";if(!set.Contains(id))return id;}}
    private static string ObjectText(string kind)=>kind switch{"treeS"=>"小树","treeM"=>"中树","treeL"=>"大树","bridge"=>"桥","rock"=>"岩石",_=>kind};
    private static (double X,double Y,double Width,double Height) RotateLocal(double x,double y,double width,double height,int blueprintWidth,int blueprintHeight,int turns)=>(((turns%4)+4)%4) switch{0=>(x,y,width,height),1=>(blueprintHeight-y-height,x,height,width),2=>(blueprintWidth-x-width,blueprintHeight-y-height,width,height),_=>(y,blueprintWidth-x-width,height,width)};
    private void DrawLabel(DrawingContext dc,string text,double x,double y)=>dc.DrawText(new(text,System.Globalization.CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),11,Brushes.White,VisualTreeHelper.GetDpi(this).PixelsPerDip),WorldToScreen(x,y));
    private static Brush FrozenBrush(Color color){var b=new SolidColorBrush(color);b.Freeze();return b;}
    private static Pen FrozenPen(Brush brush,double width){var p=new Pen(brush,width);p.Freeze();return p;}
    private readonly record struct PreviewRect(int X,int Y,int Width,int Height);
    private sealed record PreviewResult(CompositionEngine Engine,byte[] Pixels);
}
