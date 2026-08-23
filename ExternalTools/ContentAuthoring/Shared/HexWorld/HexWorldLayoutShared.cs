namespace ContentAuthoring.Shared.HexWorld;

/// <summary>
/// Odd-R offset, pointy-top — MUST stay identical to Runtime <c>XianXia.Core.World.Hex.HexWorldLayout</c>.
/// JSON / Domain: HexCoord.Q = column, HexCoord.R = row.
/// </summary>
public static class HexWorldLayoutShared
{
    public const int DefaultWidth = 100;
    public const int DefaultHeight = 50;
    public const int PlayableOriginQ = 8;
    public const int PlayableOriginR = 10;
    public const float DefaultHexSize = 1f;
    public const string CoordinateSystem = "OddROffsetPointyTop";

    public static float HorizontalPitch(float hexSize) =>
        (float)(Math.Sqrt(3) * hexSize);

    public static float VerticalPitch(float hexSize) =>
        1.5f * hexSize;

    public static void CoordToWorldCenter(HexCoordDto coord, float hexSize, out float worldX, out float worldY)
    {
        var col = coord.Q;
        var row = coord.R;
        worldX = HorizontalPitch(hexSize) * (col + 0.5f * (row & 1));
        worldY = VerticalPitch(hexSize) * row;
    }

    public static HexCoordDto WorldToCoord(float worldX, float worldY, float hexSize)
    {
        if (hexSize <= 0.0001f)
            return new HexCoordDto(0, 0);

        var row = (int)Math.Round(worldY / VerticalPitch(hexSize));
        var col = (int)Math.Round(worldX / HorizontalPitch(hexSize) - 0.5 * (row & 1));
        return new HexCoordDto(col, row);
    }

    public static void ComputeWorldBounds(
        int width,
        int height,
        float hexSize,
        out float minX,
        out float maxX,
        out float minY,
        out float maxY)
    {
        minX = minY = float.MaxValue;
        maxX = maxY = float.MinValue;
        if (width <= 0 || height <= 0)
        {
            minX = maxX = minY = maxY = 0f;
            return;
        }

        SampleCellBounds(new HexCoordDto(0, 0), hexSize, ref minX, ref maxX, ref minY, ref maxY);
        SampleCellBounds(new HexCoordDto(width - 1, 0), hexSize, ref minX, ref maxX, ref minY, ref maxY);
        SampleCellBounds(new HexCoordDto(0, height - 1), hexSize, ref minX, ref maxX, ref minY, ref maxY);
        SampleCellBounds(new HexCoordDto(width - 1, height - 1), hexSize, ref minX, ref maxX, ref minY, ref maxY);

        if (maxX < minX)
            minX = maxX = minY = maxY = 0f;
    }

    public static bool ValidateCenterRoundTrip(HexCoordDto coord, float hexSize, out HexCoordDto roundTripped)
    {
        roundTripped = default;
        if (hexSize <= 0.0001f)
            return false;
        CoordToWorldCenter(coord, hexSize, out var wx, out var wy);
        roundTripped = WorldToCoord(wx, wy, hexSize);
        return roundTripped.Q == coord.Q && roundTripped.R == coord.R;
    }

    /// <summary>Cube / axial distance — matches Runtime <see cref="HexMath.Distance"/> on stored Q,R.</summary>
    public static int Distance(HexCoordDto a, HexCoordDto b)
    {
        var dq = Math.Abs(a.Q - b.Q);
        var dr = Math.Abs(a.R - b.R);
        var aS = -a.Q - a.R;
        var bS = -b.Q - b.R;
        var ds = Math.Abs(aS - bS);
        return (dq + dr + ds) / 2;
    }

    public static HexCoordDto Neighbor(HexCoordDto coord, int directionIndex)
    {
        ReadOnlySpan<(int Q, int R)> dirs = stackalloc (int, int)[]
        {
            (1, 0), (1, -1), (0, -1), (-1, 0), (-1, 1), (0, 1)
        };
        var d = dirs[directionIndex];
        return new HexCoordDto(coord.Q + d.Q, coord.R + d.R);
    }

    public static bool AreNeighbors(HexCoordDto a, HexCoordDto b) => Distance(a, b) == 1;

    static void SampleCellBounds(
        HexCoordDto coord,
        float hexSize,
        ref float minX,
        ref float maxX,
        ref float minY,
        ref float maxY)
    {
        CoordToWorldCenter(coord, hexSize, out var cx, out var cy);
        for (var i = 0; i < 6; i++)
        {
            var angle = (Math.PI / 3.0) * i + Math.PI / 6.0;
            var x = cx + hexSize * (float)Math.Cos(angle);
            var y = cy + hexSize * (float)Math.Sin(angle);
            minX = Math.Min(minX, x);
            maxX = Math.Max(maxX, x);
            minY = Math.Min(minY, y);
            maxY = Math.Max(maxY, y);
        }
    }
}
