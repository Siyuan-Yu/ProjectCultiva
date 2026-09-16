using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace SurfaceAuthoring.EditorCommon;

public abstract class SurfaceCanvasBase : FrameworkElement
{
    private static readonly Brush BackgroundBrush=CreateBackground();
    protected Point Offset = new(20,20);
    protected double Scale = 1;
    private bool _panning;
    private Point _last;
    private (int X,int Y) _lastCursor=(int.MinValue,int.MinValue);
    public int SurfaceWidth { get; protected set; } = 1;
    public int SurfaceHeight { get; protected set; } = 1;
    public event Action<int,int>? CursorCellChanged;

    protected SurfaceCanvasBase(){Focusable=true;ClipToBounds=true;}
    public void SetSurfaceSize(int width,int height){width=Math.Max(1,width);height=Math.Max(1,height);if(SurfaceWidth==width&&SurfaceHeight==height)return;SurfaceWidth=width;SurfaceHeight=height;InvalidateVisual();}
    public void Fit(){if(ActualWidth<=0||ActualHeight<=0)return;Scale=Math.Max(.02,Math.Min(ActualWidth/SurfaceWidth,ActualHeight/SurfaceHeight)*.94);Offset=new((ActualWidth-SurfaceWidth*Scale)/2,(ActualHeight-SurfaceHeight*Scale)/2);InvalidateVisual();}
    public void ActualSize(){Scale=8;Offset=new(20,20);InvalidateVisual();}
    public void ZoomIn()=>ZoomAt(new(ActualWidth/2,ActualHeight/2),1.25);
    public void ZoomOut()=>ZoomAt(new(ActualWidth/2,ActualHeight/2),.8);
    protected Point WorldToScreen(double x,double y)=>new(Offset.X+x*Scale,Offset.Y+(SurfaceHeight-y)*Scale);
    protected (double X,double Y) ScreenToWorld(Point p)=>((p.X-Offset.X)/Scale,SurfaceHeight-(p.Y-Offset.Y)/Scale);
    protected Rect WorldRect(double x,double y,double width,double height){var topLeft=WorldToScreen(x,y+height);return new(topLeft.X,topLeft.Y,width*Scale,height*Scale);}
    protected override void OnRender(DrawingContext dc){dc.DrawRectangle(BackgroundBrush,null,new(0,0,ActualWidth,ActualHeight));dc.PushClip(new RectangleGeometry(new(0,0,ActualWidth,ActualHeight)));DrawSurface(dc);dc.Pop();}
    protected abstract void DrawSurface(DrawingContext dc);
    protected virtual bool BeginEdit(MouseButtonEventArgs e,(double X,double Y) world)=>false;
    protected virtual void ContinueEdit(MouseEventArgs e,(double X,double Y) world){}
    protected virtual void EndEdit(MouseButtonEventArgs e,(double X,double Y) world){}
    protected override void OnMouseWheel(MouseWheelEventArgs e){var modifiers=Keyboard.Modifiers;if((modifiers&ModifierKeys.Alt)==0&&(modifiers&ModifierKeys.Control)==0)return;ZoomAt(e.GetPosition(this),e.Delta>0?1.15:1/1.15);e.Handled=true;}
    protected override void OnMouseDown(MouseButtonEventArgs e){Focus();_last=e.GetPosition(this);if(e.ChangedButton==MouseButton.Middle||(e.ChangedButton==MouseButton.Left&&Keyboard.IsKeyDown(Key.Space))){_panning=true;CaptureMouse();e.Handled=true;return;}var w=ScreenToWorld(_last);if(BeginEdit(e,w)){CaptureMouse();e.Handled=true;}}
    protected override void OnMouseMove(MouseEventArgs e){var p=e.GetPosition(this);if(_panning){Offset+=p-_last;_last=p;InvalidateVisual();return;}var w=ScreenToWorld(p);var x=(int)Math.Floor(w.X);var y=(int)Math.Floor(w.Y);var next=x>=0&&y>=0&&x<SurfaceWidth&&y<SurfaceHeight?(x,y):(-1,-1);if(next!=_lastCursor){_lastCursor=next;CursorCellChanged?.Invoke(next.Item1,next.Item2);}ContinueEdit(e,w);}
    protected override void OnMouseUp(MouseButtonEventArgs e){var w=ScreenToWorld(e.GetPosition(this));if(_panning){_panning=false;ReleaseMouseCapture();e.Handled=true;return;}EndEdit(e,w);if(IsMouseCaptured)ReleaseMouseCapture();}
    private void ZoomAt(Point p,double m){var w=ScreenToWorld(p);Scale=Math.Clamp(Scale*m,.02,48);var now=WorldToScreen(w.X,w.Y);Offset+=p-now;InvalidateVisual();}
    private static Brush CreateBackground(){var brush=new SolidColorBrush(Color.FromRgb(35,39,46));brush.Freeze();return brush;}
}
