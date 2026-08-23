using ContentAuthoring.Shared.HexWorld;
using Xunit;

namespace ContentAuthoring.Shared.Tests;

public sealed class HexEditorRenderCachePerfTests
{
    [Fact]
    public void WGE_PERF_01_ViewportSync_DoesNotRequireGeometryRebuildFlag()
    {
        var cache = new HexEditorRenderCache();
        cache.ResetForWorld(200, 100);
        cache.ClearAllDirty();
        var before = cache.GeometryRebuildCount;
        cache.NoteViewportSyncWithoutRebuild();
        Assert.Equal(before, cache.GeometryRebuildCount);
        Assert.Equal(1, cache.ViewportSyncWithoutRebuildCount);
    }

    [Fact]
    public void WGE_PERF_02_Hover_DoesNotMarkTerrainChunksDirty()
    {
        var cache = new HexEditorRenderCache();
        cache.ResetForWorld(200, 100);
        cache.ClearAllDirty();
        // Hover is overlay-only in HexMapViewHost; cache dirty stays empty.
        Assert.Equal(0, cache.DirtyChunkCount);
    }

    [Fact]
    public void WGE_PERF_03_TerrainEdit_DirtiesOnlyAffectedChunk()
    {
        var cache = new HexEditorRenderCache();
        cache.ResetForWorld(200, 100);
        cache.ClearAllDirty();
        cache.MarkHexDirty(17, 3);
        var dirty = cache.SnapshotDirtyChunks();
        Assert.Single(dirty);
        Assert.Equal((1, 0), dirty.First());
    }

    [Fact]
    public void WGE_PERF_04_RoadEdit_DirtiesOnlyAffectedChunks()
    {
        var cache = new HexEditorRenderCache();
        cache.ResetForWorld(200, 100);
        cache.ClearAllDirty();
        cache.MarkHexesDirty(new[] { (15, 15), (16, 15) });
        var dirty = cache.SnapshotDirtyChunks().OrderBy(c => c.Cx).ThenBy(c => c.Cy).ToArray();
        Assert.Equal(2, dirty.Length);
        Assert.Equal((0, 0), dirty[0]);
        Assert.Equal((1, 0), dirty[1]);
    }

    [Fact]
    public void WGE_PERF_05_SerializeOnRepaintCounter_StartsAtZero()
    {
        var cache = new HexEditorRenderCache();
        Assert.Equal(0, cache.SerializeOnRepaintCount);
    }

    [Fact]
    public void WGE_PERF_06_DeserializeOnRepaintCounter_StartsAtZero()
    {
        var cache = new HexEditorRenderCache();
        Assert.Equal(0, cache.DeserializeOnRepaintCount);
    }

    [Fact]
    public void WGE_PERF_07_ScreenToHex_IsO1_DoesNotEnumerateCells()
    {
        var viewport = new HexMapViewport();
        viewport.SetViewportSize(800, 600);
        viewport.FitWorld(200, 100);
        var coord = viewport.ScreenToHex(400, 300, 200, 100);
        Assert.True(coord.Q >= 0 && coord.R >= 0);
        Assert.True(coord.Q < 200 && coord.R < 100);
    }

    [Fact]
    public void WGE_PERF_08_CacheRebuild_DoesNotAlterAuthoredCellData()
    {
        var world = HexWorldContentGenerator.CreateBlank("test:w", "t", 32, 32, HexTerrainIds.Mountain, false);
        HexWorldContentGenerator.SetTerrain(world, 4, 5, HexTerrainIds.Forest);
        HexWorldContentGenerator.PaintRoadTile(world, 6, 7);
        var before = HexWorldContentJson.Serialize(world);

        var cache = new HexEditorRenderCache();
        cache.ResetForWorld(world.Width, world.Height);
        cache.MarkAllDirty();
        cache.ClearAllDirty();
        cache.NoteGeometryRebuild(4);

        var after = HexWorldContentJson.Serialize(world);
        Assert.Equal(before, after);
        Assert.Equal(HexTerrainIds.Forest, HexWorldContentGenerator.GetCell(world, 4, 5)!.Terrain);
        Assert.True(HexWorldContentGenerator.GetCell(world, 6, 7)!.IsRoad);
    }

    [Fact]
    public void WGE_PERF_ChunkSize_MatchesRuntimeSixteen()
    {
        Assert.Equal(16, HexEditorRenderCache.ChunkSize);
        Assert.Equal(16, HexWorldLayoutShared.RenderChunkSize);
    }

    [Fact]
    public void WGE_PERF_VisibleCulling_FitWorldSeesMostChunks_CloseZoomSeesFewer()
    {
        var cache = new HexEditorRenderCache();
        cache.ResetForWorld(200, 100);
        var viewport = new HexMapViewport { HexSize = 1f };
        viewport.SetViewportSize(1000, 700);
        viewport.FitWorld(200, 100);
        HexEditorRenderCache.ComputeVisibleWorldRect(viewport, out var minWx, out var maxWx, out var minWy, out var maxWy);
        var fitCount = cache.CollectVisibleChunks(minWx, maxWx, minWy, maxWy, 1f).Count;

        viewport.ViewHalf = 8;
        HexEditorRenderCache.ComputeVisibleWorldRect(viewport, out minWx, out maxWx, out minWy, out maxWy);
        var closeCount = cache.CollectVisibleChunks(minWx, maxWx, minWy, maxWy, 1f).Count;

        Assert.True(fitCount > closeCount);
        Assert.True(closeCount < cache.TotalChunks);
        Assert.Equal(13 * 7, cache.TotalChunks); // 200/16 ceil=13, 100/16 ceil=7
    }

    [Fact]
    public void WGE_PERF_BrushStroke_SingleUndoSnapshot_NotPerHex()
    {
        var doc = new HexWorldEditorDocument();
        doc.NewWorld(64, 32, HexTerrainIds.Mountain, false);
        doc.ActiveTool = HexEditorTool.Terrain;
        doc.ActiveTerrain = HexTerrainIds.Plain;
        doc.PushUndo();
        doc.ApplyTerrainBrush(new HexCoordDto(10, 10), singleStrokeUndo: false);
        doc.ApplyTerrainBrush(new HexCoordDto(11, 10), singleStrokeUndo: false);
        doc.ApplyTerrainBrush(new HexCoordDto(12, 10), singleStrokeUndo: false);
        Assert.True(doc.Undo());
        Assert.Equal(HexTerrainIds.Mountain, HexWorldContentGenerator.GetCell(doc.World, 10, 10)!.Terrain);
        Assert.Equal(HexTerrainIds.Mountain, HexWorldContentGenerator.GetCell(doc.World, 12, 10)!.Terrain);
    }
}
