namespace ContentAuthoring.Shared.HexWorld;

/// <summary>
/// Odd-R offset, pointy-top — MUST stay identical to Runtime <c>XianXia.Core.World.Hex.HexWorldLayout</c>
/// and neighbor/distance Authority in <c>HexMath</c>.
/// JSON / Domain: HexCoord.Q = column, HexCoord.R = row（Odd-R offset，不是 axial）。
/// </summary>
public static class HexWorldLayoutShared
{
    public const int DefaultWidth = 100;
    public const int DefaultHeight = 50;
    public const int PlayableOriginQ = 8;
    public const int PlayableOriginR = 10;
    public const float DefaultHexSize = 1f;
    public const int RenderChunkSize = 16;
    public const string CoordinateSystem = "OddROffsetPointyTop";
    public const int DirectionCount = 6;

    /// <summary>Axial 方向；仅作用于 axial 坐标。存储 Odd-R 请用 <see cref="Neighbor"/>。</summary>
    static readonly (int Q, int R)[] AxialDirections =
    {
        (1, 0), (1, -1), (0, -1), (-1, 0), (-1, 1), (0, 1),
    };

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

    public static int Distance(HexCoordDto a, HexCoordDto b)
    {
        OffsetOddRToAxial(a, out var aq, out var ar);
        OffsetOddRToAxial(b, out var bq, out var br);
        var asCube = -aq - ar;
        var bsCube = -bq - br;
        return (Math.Abs(aq - bq) + Math.Abs(ar - br) + Math.Abs(asCube - bsCube)) / 2;
    }

    /// <summary>
    /// Odd-R 直线：先转 axial/cube，再 lerp + round，再转回 Odd-R。
    /// 禁止对存储的 (Q,R) 直接做 cube lerp。
    /// </summary>
    public static void CollectHexLine(HexCoordDto from, HexCoordDto to, List<HexCoordDto> pathOut)
    {
        if (pathOut == null)
            throw new ArgumentNullException(nameof(pathOut));

        pathOut.Clear();
        var steps = Distance(from, to);
        if (steps <= 0)
        {
            pathOut.Add(from);
            return;
        }

        OffsetOddRToAxial(from, out var aq, out var ar);
        OffsetOddRToAxial(to, out var bq, out var br);
        var asCube = -aq - ar;
        var bsCube = -bq - br;

        for (var i = 0; i <= steps; i++)
        {
            var t = i / (float)steps;
            var q = aq + (bq - aq) * t;
            var r = ar + (br - ar) * t;
            var s = asCube + (bsCube - asCube) * t;
            CubeRound(q, r, s, out var rq, out var rr);
            pathOut.Add(AxialToOffsetOddR(rq, rr));
        }
    }

    public static HexCoordDto Neighbor(HexCoordDto coord, int directionIndex)
    {
        if (directionIndex < 0 || directionIndex >= DirectionCount)
            throw new ArgumentOutOfRangeException(nameof(directionIndex));

        OffsetOddRToAxial(coord, out var aq, out var ar);
        var d = AxialDirections[directionIndex];
        return AxialToOffsetOddR(aq + d.Q, ar + d.R);
    }

    public static bool AreNeighbors(HexCoordDto a, HexCoordDto b) => Distance(a, b) == 1;

    public static void OffsetOddRToAxial(HexCoordDto offset, out int axialQ, out int axialR)
    {
        axialQ = offset.Q - (offset.R - (offset.R & 1)) / 2;
        axialR = offset.R;
    }

    public static HexCoordDto AxialToOffsetOddR(int axialQ, int axialR)
    {
        var col = axialQ + (axialR - (axialR & 1)) / 2;
        return new HexCoordDto(col, axialR);
    }

    static void CubeRound(float q, float r, float s, out int roundQ, out int roundR)
    {
        var rq = Math.Round(q);
        var rr = Math.Round(r);
        var rs = Math.Round(s);

        var dq = Math.Abs(rq - q);
        var dr = Math.Abs(rr - r);
        var ds = Math.Abs(rs - s);

        if (dq > dr && dq > ds)
            rq = -rr - rs;
        else if (dr > ds)
            rr = -rq - rs;

        roundQ = (int)rq;
        roundR = (int)rr;
    }

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
