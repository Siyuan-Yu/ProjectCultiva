namespace ContentAuthoring.Shared.HexWorld;

public sealed class HexMapViewport
{
    public double ViewportWidth { get; private set; } = 800;
    public double ViewportHeight { get; private set; } = 600;
    public double ViewCenterX { get; set; }
    public double ViewCenterY { get; set; }
    public double ViewHalf { get; set; } = 20;
    public float HexSize { get; set; } = HexWorldLayoutShared.DefaultHexSize;

    public double Scale
    {
        get
        {
            var minDim = Math.Min(ViewportWidth, ViewportHeight);
            if (minDim <= 1 || ViewHalf <= 0.0001)
                return 1;
            return minDim / (2.0 * ViewHalf);
        }
    }

    public void SetViewportSize(double width, double height)
    {
        ViewportWidth = Math.Max(1, width);
        ViewportHeight = Math.Max(1, height);
    }

    public void FitWorld(int mapWidth, int mapHeight)
    {
        HexWorldLayoutShared.ComputeWorldBounds(
            mapWidth,
            mapHeight,
            HexSize,
            out var minX,
            out var maxX,
            out var minY,
            out var maxY);
        ViewCenterX = (minX + maxX) * 0.5;
        ViewCenterY = (minY + maxY) * 0.5;
        ViewHalf = Math.Max(maxX - minX, maxY - minY) * 0.5 + HexSize;
    }

    public (double X, double Y) ProjectHexCenter(HexCoordDto coord)
    {
        HexWorldLayoutShared.CoordToWorldCenter(coord, HexSize, out var wx, out var wy);
        var cx = ViewportWidth * 0.5;
        var cy = ViewportHeight * 0.5;
        return (cx + (wx - ViewCenterX) * Scale, cy - (wy - ViewCenterY) * Scale);
    }

    public HexCoordDto ScreenToHex(double screenX, double screenY, int mapWidth, int mapHeight)
    {
        var cx = ViewportWidth * 0.5;
        var cy = ViewportHeight * 0.5;
        var wx = ViewCenterX + (screenX - cx) / Scale;
        var wy = ViewCenterY - (screenY - cy) / Scale;
        var coord = HexWorldLayoutShared.WorldToCoord((float)wx, (float)wy, HexSize);
        if (coord.Q < 0 || coord.R < 0 || coord.Q >= mapWidth || coord.R >= mapHeight)
            return new HexCoordDto(-1, -1);
        return coord;
    }
}
