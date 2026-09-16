using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace SurfaceAuthoring.EditorCommon;

public sealed class GridViewport : FrameworkElement
{
    private const double BaseCellPixels = 18d;
    private Point _offset = new(20, 20);
    private double _scale = 1d;
    private bool _panning;
    private Point _lastMouse;

    public int Columns { get; set; } = 1;
    public int Rows { get; set; } = 1;
    public event Action<int, int>? CursorCellChanged;

    public GridViewport()
    {
        Focusable = true;
        ClipToBounds = true;
    }

    public void ZoomIn() => ZoomAt(new Point(ActualWidth / 2, ActualHeight / 2), 1.25d);
    public void ZoomOut() => ZoomAt(new Point(ActualWidth / 2, ActualHeight / 2), 0.8d);
    public void ActualSize() { _scale = 1d; _offset = new Point(20, 20); InvalidateVisual(); }
    public void Fit()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0 || Columns <= 0 || Rows <= 0) return;
        _scale = Math.Max(.03d, Math.Min(ActualWidth / (Columns * BaseCellPixels), ActualHeight / (Rows * BaseCellPixels)) * .92d);
        _offset = new Point((ActualWidth - Columns * BaseCellPixels * _scale) / 2d, (ActualHeight - Rows * BaseCellPixels * _scale) / 2d);
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.WhiteSmoke, null, new Rect(0, 0, ActualWidth, ActualHeight));
        if (Columns <= 0 || Rows <= 0) return;
        var cell = BaseCellPixels * _scale;
        var width = Columns * cell;
        var height = Rows * cell;
        dc.PushClip(new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight)));
        dc.DrawRectangle(Brushes.White, new Pen(Brushes.SlateGray, 1), new Rect(_offset.X, _offset.Y, width, height));
        var pen = new Pen(Brushes.LightSlateGray, Math.Max(.35d, .7d));
        var firstX = Math.Max(0, (int)Math.Floor(-_offset.X / cell));
        var lastX = Math.Min(Columns, (int)Math.Ceiling((ActualWidth - _offset.X) / cell));
        var firstY = Math.Max(0, (int)Math.Floor(-_offset.Y / cell));
        var lastY = Math.Min(Rows, (int)Math.Ceiling((ActualHeight - _offset.Y) / cell));
        for (var x = firstX; x <= lastX; x++) dc.DrawLine(pen, new Point(_offset.X + x * cell, _offset.Y), new Point(_offset.X + x * cell, _offset.Y + height));
        for (var y = firstY; y <= lastY; y++) dc.DrawLine(pen, new Point(_offset.X, _offset.Y + y * cell), new Point(_offset.X + width, _offset.Y + y * cell));
        dc.Pop();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
        ZoomAt(e.GetPosition(this), e.Delta > 0 ? 1.15d : 1d / 1.15d);
        e.Handled = true;
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        Focus();
        if (e.LeftButton == MouseButtonState.Pressed && Keyboard.IsKeyDown(Key.Space)) { _panning = true; _lastMouse = e.GetPosition(this); CaptureMouse(); e.Handled = true; }
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        if (_panning) { _panning = false; ReleaseMouseCapture(); e.Handled = true; }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var point = e.GetPosition(this);
        if (_panning)
        {
            _offset += point - _lastMouse;
            _lastMouse = point;
            InvalidateVisual();
        }
        var cell = BaseCellPixels * _scale;
        var x = (int)Math.Floor((point.X - _offset.X) / cell);
        var screenY = (int)Math.Floor((point.Y - _offset.Y) / cell);
        if (x >= 0 && x < Columns && screenY >= 0 && screenY < Rows) CursorCellChanged?.Invoke(x, Rows - 1 - screenY);
        else CursorCellChanged?.Invoke(-1, -1);
    }

    private void ZoomAt(Point screenPoint, double multiplier)
    {
        var oldCell = BaseCellPixels * _scale;
        var logicalX = (screenPoint.X - _offset.X) / oldCell;
        var logicalY = (screenPoint.Y - _offset.Y) / oldCell;
        _scale = Math.Clamp(_scale * multiplier, .03d, 20d);
        var newCell = BaseCellPixels * _scale;
        _offset = new Point(screenPoint.X - logicalX * newCell, screenPoint.Y - logicalY * newCell);
        InvalidateVisual();
    }
}
