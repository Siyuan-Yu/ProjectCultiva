using SurfaceAuthoring.Core;
using SurfaceAuthoring.EditorCommon;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FineEditor;

public enum FineTool { Select, BaseTerrainBrush, FeatureBrush, Eraser, Fill, RectangleSelect, ObjectPlacement }

public sealed class FineEditorCanvas:SurfaceCanvasBase
{
    private static readonly Brush ObjectBrush=FrozenBrush(Color.FromArgb(120,250,190,50));
    private static readonly Brush SelectionBrush=FrozenBrush(Color.FromArgb(50,60,150,255));
    private static readonly Brush HoverBrush=FrozenBrush(Color.FromArgb(50,255,255,255));
    private static readonly Pen ObjectPen=FrozenPen(Brushes.DarkOrange,1);
    private static readonly Pen SelectedObjectPen=FrozenPen(Brushes.White,3);
    private static readonly Pen SelectionPen=FrozenPen(Brushes.DeepSkyBlue,2);
    private static readonly Pen HoverPen=FrozenPen(Brushes.White,1);
    private static readonly Pen GridPen=FrozenPen(FrozenBrush(Color.FromArgb(80,255,255,255)),.5);

    public FineTool Tool { get; set; }
    public BaseTerrain BaseTerrain { get; set; }=BaseTerrain.Plain;
    public TerrainFeature Feature { get; set; }=TerrainFeature.Forest;
    public double FeatureDensity { get; set; }=.75;
    public int BrushSize { get; set; }=1;
    public bool ActiveBaseLayer { get; set; }=true;
    public object Document { get; private set; }=new WorldSiteBlueprintDocument();
    public BlueprintObjectPlacement? PendingObject { get; set; }
    public BlueprintObjectPlacement? SelectedObject { get; private set; }
    public Rect? Selection { get; private set; }
    public event Action? EditStarted;
    public event Action? EditCompleted;
    public event Action<BlueprintObjectPlacement?>? ObjectSelectionChanged;
    public event Action<string>? UserNotice;

    private readonly Dictionary<long,FineTerrainSourceCell> _cellIndex=[];
    private bool _editing,_terrainMutation,_mutationChanged;
    private (int X,int Y)? _last,_rectStart;
    private (int X,int Y) _hover=(-1,-1);
    private (BlueprintObjectPlacement Item,double Dx,double Dy)? _drag;
    private PixelRect? _dirty;
    private WriteableBitmap? _preview;

    public FineEditorCanvas(){RenderOptions.SetBitmapScalingMode(this,BitmapScalingMode.NearestNeighbor);SnapsToDevicePixels=true;}
    private List<FineTerrainSourceCell> Cells=>Document is WorldSiteBlueprintDocument b?b.FineTerrainSources:((DetailPatchDocument)Document).FineTerrainOverrides;
    private List<BlueprintObjectPlacement>? Objects=>(Document as WorldSiteBlueprintDocument)?.ObjectPlacements;

    public void SetDocument(object document){var changed=!ReferenceEquals(Document,document)||SurfaceWidth!=Size(document).W||SurfaceHeight!=Size(document).H;Document=document;var size=Size();SetSurfaceSize(size.W,size.H);if(changed){SelectedObject=null;Selection=null;RebuildIndex();BuildFullPreview();}InvalidateVisual();}
    public void Cancel(){PendingObject=null;_editing=false;_drag=null;InvalidateVisual();}
    public void DeleteSelected(){if(SelectedObject==null||Objects==null)return;Start(false);_mutationChanged=Objects.Remove(SelectedObject);SelectedObject=null;Finish();ObjectSelectionChanged?.Invoke(null);}
    public void DuplicateSelected(){if(SelectedObject==null||Objects==null)return;Start(false);var c=SurfaceAuthoringJson.Clone(SelectedObject);c.PlacementId=Unique(c.PlacementId+"_copy",Objects.Select(x=>x.PlacementId));c.LocalSurfaceX=Math.Min(SurfaceWidth-c.WidthCells,c.LocalSurfaceX+1);c.LocalSurfaceY=Math.Min(SurfaceHeight-c.HeightCells,c.LocalSurfaceY+1);c.Metadata["legacyGeometryState"]="edited";Objects.Add(c);SelectedObject=c;_mutationChanged=true;Finish();ObjectSelectionChanged?.Invoke(c);}
    public void RotateSelected(){if(SelectedObject==null)return;Start(false);SelectedObject.RotationQuarterTurns=(SelectedObject.RotationQuarterTurns+1)%4;(SelectedObject.WidthCells,SelectedObject.HeightCells)=(SelectedObject.HeightCells,SelectedObject.WidthCells);MarkGeometryEdited(SelectedObject);_mutationChanged=true;Finish();}
    public void ApplyObjectProperties(string kind,string?content,string?asset,double x,double y,double w,double h){if(SelectedObject==null)return;Start(false);var before=SurfaceAuthoringJson.Serialize(SelectedObject);var geometryChanged=SelectedObject.LocalSurfaceX!=x||SelectedObject.LocalSurfaceY!=y||SelectedObject.WidthCells!=w||SelectedObject.HeightCells!=h;SelectedObject.KindId=kind;SelectedObject.ContentRef=Blank(content);SelectedObject.AssetRef=Blank(asset);SelectedObject.LocalSurfaceX=x;SelectedObject.LocalSurfaceY=y;SelectedObject.WidthCells=w;SelectedObject.HeightCells=h;if(geometryChanged)MarkGeometryEdited(SelectedObject);_mutationChanged=before!=SurfaceAuthoringJson.Serialize(SelectedObject);Finish();}
    public void FillSelectionOrRegion(){var targets=Selection.HasValue?SelectedCells():Flood(_hover.X,_hover.Y);if(targets.Count==0)return;Start(true);foreach(var p in targets)Paint(p.X,p.Y);Finish();}
    public void ClearSelection(){Selection=null;InvalidateVisual();}

    protected override bool BeginEdit(MouseButtonEventArgs e,(double X,double Y) w)
    {
        if(e.ChangedButton!=MouseButton.Left||w.X<0||w.Y<0||w.X>=SurfaceWidth||w.Y>=SurfaceHeight)return false;var x=(int)w.X;var y=(int)w.Y;_hover=(x,y);
        if(Tool is FineTool.BaseTerrainBrush or FineTool.FeatureBrush or FineTool.Eraser){Start(true);_editing=true;Stroke(x,y);return true;}
        if(Tool==FineTool.Fill){FillSelectionOrRegion();return true;}
        if(Tool==FineTool.RectangleSelect){_rectStart=(x,y);Selection=new(x,y,1,1);_editing=true;return true;}
        if(Tool==FineTool.ObjectPlacement&&PendingObject!=null&&Objects!=null){Start(false);PendingObject.LocalSurfaceX=x;PendingObject.LocalSurfaceY=y;Objects.Add(PendingObject);SelectedObject=PendingObject;PendingObject=null;_mutationChanged=true;Finish();ObjectSelectionChanged?.Invoke(SelectedObject);return true;}
        if(Tool==FineTool.Select&&Objects!=null){var hit=Objects.LastOrDefault(o=>x>=o.LocalSurfaceX&&y>=o.LocalSurfaceY&&x<o.LocalSurfaceX+o.WidthCells&&y<o.LocalSurfaceY+o.HeightCells);SelectedObject=hit;ObjectSelectionChanged?.Invoke(hit);if(hit!=null){Start(false);_drag=(hit,x-hit.LocalSurfaceX,y-hit.LocalSurfaceY);}InvalidateVisual();return true;}
        return false;
    }

    protected override void ContinueEdit(MouseEventArgs e,(double X,double Y) w)
    {
        var x=(int)Math.Floor(w.X);var y=(int)Math.Floor(w.Y);var hoverChanged=_hover!=(x,y);_hover=(x,y);var visualChanged=false;
        if(_editing&&e.LeftButton==MouseButtonState.Pressed&&Tool is FineTool.BaseTerrainBrush or FineTool.FeatureBrush or FineTool.Eraser){Stroke(x,y);visualChanged=true;}
        if(_editing&&_rectStart.HasValue&&e.LeftButton==MouseButtonState.Pressed){var a=_rectStart.Value;var next=new Rect(Math.Min(a.X,x),Math.Min(a.Y,y),Math.Abs(x-a.X)+1,Math.Abs(y-a.Y)+1);if(Selection!=next){Selection=next;visualChanged=true;}}
        if(_drag.HasValue&&e.LeftButton==MouseButtonState.Pressed){var d=_drag.Value;var nx=Math.Clamp(Math.Round(x-d.Dx),0,Math.Max(0,SurfaceWidth-d.Item.WidthCells));var ny=Math.Clamp(Math.Round(y-d.Dy),0,Math.Max(0,SurfaceHeight-d.Item.HeightCells));if(d.Item.LocalSurfaceX!=nx||d.Item.LocalSurfaceY!=ny){d.Item.LocalSurfaceX=nx;d.Item.LocalSurfaceY=ny;MarkGeometryEdited(d.Item);_mutationChanged=true;visualChanged=true;}}
        if(hoverChanged&&Tool is FineTool.BaseTerrainBrush or FineTool.FeatureBrush or FineTool.Eraser)visualChanged=true;
        if(visualChanged)InvalidateVisual();
    }

    protected override void EndEdit(MouseButtonEventArgs e,(double X,double Y) w){if(_editing&&Tool is FineTool.BaseTerrainBrush or FineTool.FeatureBrush or FineTool.Eraser){_editing=false;_last=null;Finish();}else if(_editing&&_rectStart.HasValue){_editing=false;_rectStart=null;InvalidateVisual();}if(_drag.HasValue){_drag=null;Finish();}}

    protected override void DrawSurface(DrawingContext dc)
    {
        if(_preview!=null)dc.DrawImage(_preview,WorldRect(0,0,SurfaceWidth,SurfaceHeight));
        if(Scale>=3){var bounds=VisibleBounds();for(var x=bounds.Left;x<=bounds.Right;x++)dc.DrawLine(GridPen,WorldToScreen(x,bounds.Bottom),WorldToScreen(x,bounds.Top));for(var y=bounds.Bottom;y<=bounds.Top;y++)dc.DrawLine(GridPen,WorldToScreen(bounds.Left,y),WorldToScreen(bounds.Right,y));}
        if(Objects!=null)foreach(var o in Objects){dc.DrawRectangle(ObjectBrush,o==SelectedObject?SelectedObjectPen:ObjectPen,WorldRect(o.LocalSurfaceX,o.LocalSurfaceY,o.WidthCells,o.HeightCells));dc.DrawText(new(o.KindId,System.Globalization.CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),11,Brushes.Black,VisualTreeHelper.GetDpi(this).PixelsPerDip),WorldToScreen(o.LocalSurfaceX,o.LocalSurfaceY+o.HeightCells));}
        if(Selection.HasValue)dc.DrawRectangle(SelectionBrush,SelectionPen,WorldRect(Selection.Value.X,Selection.Value.Y,Selection.Value.Width,Selection.Value.Height));
        if(_hover.X>=0&&Tool is FineTool.BaseTerrainBrush or FineTool.FeatureBrush or FineTool.Eraser)dc.DrawRectangle(HoverBrush,HoverPen,WorldRect(_hover.X-BrushSize/2,_hover.Y-BrushSize/2,BrushSize,BrushSize));
    }

    private void Stroke(int x,int y){if(_last is not{}p){PaintBrush(x,y);_last=(x,y);return;}var n=Math.Max(Math.Abs(x-p.X),Math.Abs(y-p.Y));for(var i=1;i<=Math.Max(1,n);i++)PaintBrush(p.X+(x-p.X)*i/Math.Max(1,n),p.Y+(y-p.Y)*i/Math.Max(1,n));_last=(x,y);}
    private void PaintBrush(int x,int y){var startX=x-BrushSize/2;var startY=y-BrushSize/2;for(var yy=startY;yy<startY+BrushSize;yy++)for(var xx=startX;xx<startX+BrushSize;xx++)if(xx>=0&&yy>=0&&xx<SurfaceWidth&&yy<SurfaceHeight)Paint(xx,yy);InvalidateVisual();}
    private void Paint(int x,int y){var key=Key(x,y);_cellIndex.TryGetValue(key,out var c);if(Tool==FineTool.Eraser){if(c!=null){_cellIndex.Remove(key);_mutationChanged=true;MarkDirty(x,y);}return;}if(!ActiveBaseLayer&&Feature==TerrainFeature.Forest&&c?.BaseTerrainOverride==BaseTerrain.Water){UserNotice?.Invoke("水域不能生成森林。");return;}var existed=c!=null;var oldBase=c?.BaseTerrainOverride;var oldFeature=c?.FeatureOverride;var oldDensity=c?.FeatureDensity;var oldGlyph=c?.CompatibilityGlyph;c??=new FineTerrainSourceCell{SurfaceCellX=x,SurfaceCellY=y};c.CompatibilityGlyph=null;if(ActiveBaseLayer){c.BaseTerrainOverride=BaseTerrain;if(BaseTerrain==BaseTerrain.Water)c.FeatureOverride=TerrainFeature.None;}else{c.FeatureOverride=Feature;c.FeatureDensity=FeatureDensity;}_cellIndex[key]=c;if(!existed||oldBase!=c.BaseTerrainOverride||oldFeature!=c.FeatureOverride||oldDensity!=c.FeatureDensity||oldGlyph!=c.CompatibilityGlyph){_mutationChanged=true;MarkDirty(x,y);}}
    private List<(int X,int Y)> SelectedCells(){var r=Selection!.Value;var a=new List<(int,int)>();for(var y=(int)r.Y;y<r.Bottom;y++)for(var x=(int)r.X;x<r.Right;x++)a.Add((x,y));return a;}
    private List<(int X,int Y)> Flood(int sx,int sy){if(sx<0||sy<0)return[];_cellIndex.TryGetValue(Key(sx,sy),out var start);var key=ValueKey(start);var seen=new HashSet<(int,int)>();var q=new Queue<(int,int)>();q.Enqueue((sx,sy));while(q.Count>0){var p=q.Dequeue();if(p.Item1<0||p.Item2<0||p.Item1>=SurfaceWidth||p.Item2>=SurfaceHeight||!seen.Add(p))continue;_cellIndex.TryGetValue(Key(p.Item1,p.Item2),out var cell);if(ValueKey(cell)!=key){seen.Remove(p);continue;}q.Enqueue((p.Item1+1,p.Item2));q.Enqueue((p.Item1-1,p.Item2));q.Enqueue((p.Item1,p.Item2+1));q.Enqueue((p.Item1,p.Item2-1));}return seen.ToList();}
    private string ValueKey(FineTerrainSourceCell?c)=>ActiveBaseLayer?(c?.BaseTerrainOverride?.ToString()??"inherit"):(c?.FeatureOverride?.ToString()??"inherit");

    private void Start(bool terrain){_terrainMutation=terrain;_mutationChanged=false;_dirty=null;EditStarted?.Invoke();}
    private void Finish(){if(_terrainMutation&&_mutationChanged){SynchronizeCells();UpdatePreview(_dirty);}EditCompleted?.Invoke();InvalidateVisual();}
    private void RebuildIndex(){_cellIndex.Clear();foreach(var c in Cells)_cellIndex[Key(c.SurfaceCellX,c.SurfaceCellY)]=c;}
    private void SynchronizeCells(){var ordered=_cellIndex.Values.OrderBy(x=>x.SurfaceCellY).ThenBy(x=>x.SurfaceCellX).ToList();if(Document is WorldSiteBlueprintDocument b)b.FineTerrainSources=ordered;else ((DetailPatchDocument)Document).FineTerrainOverrides=ordered;}
    private void BuildFullPreview(){var pixels=new byte[SurfaceWidth*SurfaceHeight*4];for(var row=0;row<SurfaceHeight;row++){var y=SurfaceHeight-1-row;for(var x=0;x<SurfaceWidth;x++){_cellIndex.TryGetValue(Key(x,y),out var cell);WritePixel(pixels,(row*SurfaceWidth+x)*4,x,y,cell);}}_preview=new(SurfaceWidth,SurfaceHeight,96,96,PixelFormats.Bgra32,null);_preview.WritePixels(new(0,0,SurfaceWidth,SurfaceHeight),pixels,SurfaceWidth*4,0);}
    private void UpdatePreview(PixelRect? dirty){if(_preview==null||!dirty.HasValue){BuildFullPreview();return;}var r=dirty.Value;var pixels=new byte[r.Width*r.Height*4];for(var row=0;row<r.Height;row++){var y=r.Y+r.Height-1-row;for(var col=0;col<r.Width;col++){var x=r.X+col;_cellIndex.TryGetValue(Key(x,y),out var cell);WritePixel(pixels,(row*r.Width+col)*4,x,y,cell);}}_preview.WritePixels(new(r.X,SurfaceHeight-r.Y-r.Height,r.Width,r.Height),pixels,r.Width*4,0);}
    private static void WritePixel(byte[] p,int i,int x,int y,FineTerrainSourceCell?cell){var checker=((x+y)&1)==0?Color.FromRgb(90,90,95):Color.FromRgb(110,110,115);var c=cell==null?checker:ColorFor(cell);if(c.A==0)c=checker;p[i]=c.B;p[i+1]=c.G;p[i+2]=c.R;p[i+3]=255;}
    private static Color ColorFor(FineTerrainSourceCell c){if(c.FeatureOverride==TerrainFeature.Forest)return Color.FromRgb(0,100,0);return c.BaseTerrainOverride switch{BaseTerrain.Plain=>Color.FromRgb(107,142,35),BaseTerrain.Mountain=>Color.FromRgb(128,128,128),BaseTerrain.Water=>Color.FromRgb(70,130,180),_=>Color.FromArgb(0,0,0,0)};}
    private void MarkDirty(int x,int y){var r=new PixelRect(x,y,1,1);_dirty=_dirty.HasValue?Union(_dirty.Value,r):r;}
    private (int Left,int Right,int Bottom,int Top) VisibleBounds(){var left=Math.Clamp((int)Math.Floor(-Offset.X/Scale),0,SurfaceWidth);var right=Math.Clamp((int)Math.Ceiling((ActualWidth-Offset.X)/Scale),0,SurfaceWidth);var bottom=Math.Clamp((int)Math.Floor(SurfaceHeight-(ActualHeight-Offset.Y)/Scale),0,SurfaceHeight);var top=Math.Clamp((int)Math.Ceiling(SurfaceHeight+Offset.Y/Scale),0,SurfaceHeight);return(left,right,bottom,top);}
    private (int W,int H) Size()=>Size(Document);private static (int W,int H) Size(object document)=>document is WorldSiteBlueprintDocument b?(b.WidthCells,b.HeightCells):(((DetailPatchDocument)document).WidthCells,((DetailPatchDocument)document).HeightCells);
    private static PixelRect Union(PixelRect a,PixelRect b){var x=Math.Min(a.X,b.X);var y=Math.Min(a.Y,b.Y);var right=Math.Max(a.X+a.Width,b.X+b.Width);var top=Math.Max(a.Y+a.Height,b.Y+b.Height);return new(x,y,right-x,top-y);}
    private static long Key(int x,int y)=>((long)y<<32)|(uint)x;
    private static string? Blank(string?s)=>string.IsNullOrWhiteSpace(s)?null:s;
    private static void MarkGeometryEdited(BlueprintObjectPlacement item){if(item.Metadata.ContainsKey("legacyGeometryState"))item.Metadata["legacyGeometryState"]="edited";}
    private static string Unique(string p,IEnumerable<string>ids){var set=ids.ToHashSet(StringComparer.Ordinal);if(!set.Contains(p))return p;for(var i=2;;i++)if(!set.Contains(p+"_"+i))return p+"_"+i;}
    private static Brush FrozenBrush(Color color){var b=new SolidColorBrush(color);b.Freeze();return b;}
    private static Pen FrozenPen(Brush brush,double width){var p=new Pen(brush,width);p.Freeze();return p;}
    private readonly record struct PixelRect(int X,int Y,int Width,int Height);
}
