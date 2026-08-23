namespace ContentAuthoring.Shared.HexWorld;

/// <summary>
/// Editor-only rendering dirty / culling bookkeeping. Not a second authoritative Hex world.
/// Chunk size mirrors Runtime <c>HexWorldScale.RenderChunkSize</c>.
/// </summary>
public sealed class HexEditorRenderCache
{
    public const int ChunkSize = HexWorldLayoutShared.RenderChunkSize;

    readonly HashSet<(int Cx, int Cy)> _dirty = new();

    public int MapWidth { get; private set; }
    public int MapHeight { get; private set; }
    public int ChunkCountX { get; private set; }
    public int ChunkCountY { get; private set; }
    public int TotalChunks => ChunkCountX * ChunkCountY;
    public int DirtyChunkCount => _dirty.Count;
    public int ContentRevision { get; private set; }
    public int GeometryRebuildCount { get; private set; }
    public int ViewportSyncWithoutRebuildCount { get; private set; }
    public int LastVisibleChunkCount { get; private set; }
    public int SerializeOnRepaintCount { get; private set; }
    public int DeserializeOnRepaintCount { get; private set; }

    public void ResetForWorld(int width, int height)
    {
        MapWidth = Math.Max(0, width);
        MapHeight = Math.Max(0, height);
        ChunkCountX = MapWidth <= 0 ? 0 : (MapWidth + ChunkSize - 1) / ChunkSize;
        ChunkCountY = MapHeight <= 0 ? 0 : (MapHeight + ChunkSize - 1) / ChunkSize;
        _dirty.Clear();
        MarkAllDirty();
        ContentRevision++;
    }

    public void MarkAllDirty()
    {
        _dirty.Clear();
        for (var cy = 0; cy < ChunkCountY; cy++)
        for (var cx = 0; cx < ChunkCountX; cx++)
            _dirty.Add((cx, cy));
    }

    public void MarkHexDirty(int q, int r)
    {
        if (q < 0 || r < 0 || q >= MapWidth || r >= MapHeight)
            return;
        _dirty.Add((q / ChunkSize, r / ChunkSize));
    }

    public void MarkHexesDirty(IEnumerable<(int Q, int R)> hexes)
    {
        foreach (var (q, r) in hexes)
            MarkHexDirty(q, r);
    }

    public IReadOnlyCollection<(int Cx, int Cy)> SnapshotDirtyChunks() => _dirty.ToArray();

    public void ClearDirty((int Cx, int Cy) chunk) => _dirty.Remove(chunk);

    public void ClearAllDirty() => _dirty.Clear();

    public void NoteGeometryRebuild(int chunkCount)
    {
        GeometryRebuildCount += Math.Max(0, chunkCount);
    }

    public void NoteViewportSyncWithoutRebuild() => ViewportSyncWithoutRebuildCount++;

    public void NoteSerializeOnRepaint() => SerializeOnRepaintCount++;

    public void NoteDeserializeOnRepaint() => DeserializeOnRepaintCount++;

    public static (int Cx, int Cy) ChunkOf(int q, int r) => (q / ChunkSize, r / ChunkSize);

    public void ChunkCellRange(int cx, int cy, out int q0, out int r0, out int q1, out int r1)
    {
        q0 = cx * ChunkSize;
        r0 = cy * ChunkSize;
        q1 = Math.Min(MapWidth, q0 + ChunkSize) - 1;
        r1 = Math.Min(MapHeight, r0 + ChunkSize) - 1;
    }

    public List<(int Cx, int Cy)> CollectVisibleChunks(float minWx, float maxWx, float minWy, float maxWy, float hexSize)
    {
        var result = new List<(int Cx, int Cy)>();
        if (ChunkCountX <= 0 || ChunkCountY <= 0 || hexSize <= 0.0001f)
        {
            LastVisibleChunkCount = 0;
            return result;
        }

        var pad = hexSize * 1.2f;
        for (var cy = 0; cy < ChunkCountY; cy++)
        {
            for (var cx = 0; cx < ChunkCountX; cx++)
            {
                ChunkWorldBounds(cx, cy, hexSize, out var cMinX, out var cMaxX, out var cMinY, out var cMaxY);
                if (cMaxX < minWx - pad || cMinX > maxWx + pad || cMaxY < minWy - pad || cMinY > maxWy + pad)
                    continue;
                result.Add((cx, cy));
            }
        }

        LastVisibleChunkCount = result.Count;
        return result;
    }

    public void ChunkWorldBounds(
        int cx,
        int cy,
        float hexSize,
        out float minX,
        out float maxX,
        out float minY,
        out float maxY)
    {
        ChunkCellRange(cx, cy, out var q0, out var r0, out var q1, out var r1);
        minX = minY = float.MaxValue;
        maxX = maxY = float.MinValue;
        if (q1 < q0 || r1 < r0)
        {
            minX = maxX = minY = maxY = 0;
            return;
        }

        Sample(q0, r0, hexSize, ref minX, ref maxX, ref minY, ref maxY);
        Sample(q1, r0, hexSize, ref minX, ref maxX, ref minY, ref maxY);
        Sample(q0, r1, hexSize, ref minX, ref maxX, ref minY, ref maxY);
        Sample(q1, r1, hexSize, ref minX, ref maxX, ref minY, ref maxY);
        // Odd-R stagger: include mid columns on both row parities when present.
        if (q1 > q0)
        {
            Sample(q0 + 1, r0, hexSize, ref minX, ref maxX, ref minY, ref maxY);
            Sample(q0 + 1, r1, hexSize, ref minX, ref maxX, ref minY, ref maxY);
        }
    }

    public static void ComputeVisibleWorldRect(HexMapViewport viewport, out float minWx, out float maxWx, out float minWy, out float maxWy)
    {
        var halfW = viewport.ViewportWidth * 0.5;
        var halfH = viewport.ViewportHeight * 0.5;
        var scale = Math.Max(0.0001, viewport.Scale);
        minWx = (float)(viewport.ViewCenterX - halfW / scale);
        maxWx = (float)(viewport.ViewCenterX + halfW / scale);
        minWy = (float)(viewport.ViewCenterY - halfH / scale);
        maxWy = (float)(viewport.ViewCenterY + halfH / scale);
    }

    static void Sample(int q, int r, float hexSize, ref float minX, ref float maxX, ref float minY, ref float maxY)
    {
        HexWorldLayoutShared.CoordToWorldCenter(new HexCoordDto(q, r), hexSize, out var cx, out var cy);
        minX = Math.Min(minX, cx - hexSize);
        maxX = Math.Max(maxX, cx + hexSize);
        minY = Math.Min(minY, cy - hexSize);
        maxY = Math.Max(maxY, cy + hexSize);
    }
}
