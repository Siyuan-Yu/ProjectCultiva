using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ContentAuthoring.Shared.HexWorld;

namespace WorldGraphEditor;

public static class HexMapCanvasRenderer
{
    public static void Render(
        Canvas canvas,
        HexWorldEditorDocument document,
        HexMapViewport viewport,
        bool painting)
    {
        canvas.Children.Clear();
        var world = document.World;
        viewport.HexSize = world.HexSize;
        viewport.SetViewportSize(canvas.ActualWidth, canvas.ActualHeight);

        var hexRadius = Math.Max(2.0, world.HexSize * viewport.Scale * 0.92);
        if (hexRadius < 1.5)
            return;

        var cellIndex = BuildCellIndex(world);
        for (var r = 0; r < world.Height; r++)
        {
            for (var q = 0; q < world.Width; q++)
            {
                if (!cellIndex.TryGetValue((q, r), out var cell))
                    continue;
                var center = viewport.ProjectHexCenter(new HexCoordDto(q, r));
                if (center.X < -hexRadius || center.Y < -hexRadius ||
                    center.X > viewport.ViewportWidth + hexRadius ||
                    center.Y > viewport.ViewportHeight + hexRadius)
                    continue;

                var passable = cell.Passable ?? HexTerrainPalette.DefaultPassable(cell.Terrain);
                var rgb = HexTerrainPalette.ResolveRgb(cell.Terrain, cell.IsRoad, passable);
                var fill = new SolidColorBrush(Color.FromRgb(rgb.R, rgb.G, rgb.B));
                var poly = CreateHexPolygon(center.X, center.Y, hexRadius, fill, new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)));
                canvas.Children.Add(poly);
            }
        }

        foreach (var site in world.Sites)
        {
            var anchor = new HexCoordDto(site.AnchorQ, site.AnchorR);
            var center = viewport.ProjectHexCenter(anchor);
            DrawSiteIcon(canvas, center, site.SiteType, hexRadius * 1.8);
            if (viewport.Scale > 4.5)
            {
                var label = new TextBlock
                {
                    Text = site.DisplayName,
                    Foreground = Brushes.Black,
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                };
                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(label, center.X - label.DesiredSize.Width * 0.5);
                Canvas.SetTop(label, center.Y - hexRadius * 2.4 - label.DesiredSize.Height);
                canvas.Children.Add(label);
            }
        }

        if (document.SelectedHex is { } sel && sel.Q >= 0)
        {
            var center = viewport.ProjectHexCenter(sel);
            var highlight = CreateHexPolygon(center.X, center.Y, hexRadius, Brushes.Transparent, new SolidColorBrush(Color.FromArgb(220, 255, 180, 40)));
            highlight.StrokeThickness = 2;
            canvas.Children.Add(highlight);
        }
    }

    static Dictionary<(int Q, int R), HexCellDto> BuildCellIndex(HexWorldDefinitionDto world)
    {
        var map = new Dictionary<(int, int), HexCellDto>(world.Cells.Count);
        foreach (var cell in world.Cells)
            map[(cell.Q, cell.R)] = cell;
        return map;
    }

    static Polygon CreateHexPolygon(double cx, double cy, double radius, Brush fill, Brush stroke)
    {
        var points = new PointCollection();
        for (var i = 0; i < 6; i++)
        {
            var angle = Math.PI / 3.0 * i + Math.PI / 6.0;
            points.Add(new Point(cx + radius * Math.Cos(angle), cy + radius * Math.Sin(angle)));
        }

        return new Polygon
        {
            Points = points,
            Fill = fill,
            Stroke = stroke,
            StrokeThickness = 1,
        };
    }

    static void DrawSiteIcon(Canvas canvas, (double X, double Y) center, string siteType, double size)
    {
        var body = new Rectangle
        {
            Width = size * 0.55,
            Height = size * 0.45,
            Fill = new SolidColorBrush(Color.FromRgb(0xD6, 0xB8, 0x80)),
            Stroke = new SolidColorBrush(Color.FromRgb(0x8F, 0x52, 0x33)),
            StrokeThickness = 1,
        };
        Canvas.SetLeft(body, center.X - body.Width * 0.5);
        Canvas.SetTop(body, center.Y - body.Height * 0.35);
        canvas.Children.Add(body);

        var roof = new Polygon
        {
            Fill = new SolidColorBrush(Color.FromRgb(0x8F, 0x52, 0x33)),
            Points = new PointCollection
            {
                new(center.X, center.Y - size * 0.55),
                new(center.X - size * 0.34, center.Y - size * 0.18),
                new(center.X + size * 0.34, center.Y - size * 0.18),
            },
        };
        canvas.Children.Add(roof);
    }
}
