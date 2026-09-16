namespace SurfaceAuthoring.Core;

public enum WorldResizeAnchor { LeftBottom, RightBottom, LeftTop, RightTop }

public sealed record WorldCompositionResizeResult(WorldCompositionDocument? Document, IReadOnlyList<string> BlockingReasons)
{
    public bool Succeeded => Document != null && BlockingReasons.Count == 0;
}

/// <summary>
/// Applies the Composer-only resize contract. It deliberately moves source placement
/// coordinates rather than changing Blueprint/Patch local coordinates.
/// </summary>
public static class WorldCompositionResize
{
    public static WorldCompositionResizeResult TryResize(
        WorldCompositionDocument source,
        LinkedSourceSet linked,
        int newWidthCells,
        int newHeightCells,
        WorldResizeAnchor anchor)
    {
        var errors = new List<string>();
        if (newWidthCells <= 0 || newHeightCells <= 0)
            errors.Add("新宽度和新高度必须为正数。");
        if (newWidthCells % SurfaceAuthoringCoordinates.WorldEditorCellSize != 0 ||
            newHeightCells % SurfaceAuthoringCoordinates.WorldEditorCellSize != 0)
            errors.Add("WorldComposer 的世界宽高必须是 10 格的整数倍。");
        if (errors.Count > 0) return new(null, errors);

        var copy = SurfaceAuthoringJson.Clone(source);
        var dx = newWidthCells - source.SurfaceWidthCells;
        var dy = newHeightCells - source.SurfaceHeightCells;
        var shiftX = anchor is WorldResizeAnchor.RightBottom or WorldResizeAnchor.RightTop ? dx : 0;
        var shiftY = anchor is WorldResizeAnchor.LeftTop or WorldResizeAnchor.RightTop ? dy : 0;
        var editorShiftX = shiftX / SurfaceAuthoringCoordinates.WorldEditorCellSize;
        var editorShiftY = shiftY / SurfaceAuthoringCoordinates.WorldEditorCellSize;

        foreach (var cell in copy.CoarseTerrainSources) { cell.EditorCellX += editorShiftX; cell.EditorCellY += editorShiftY; }
        foreach (var cell in copy.CoarseFeatureSources) { cell.EditorCellX += editorShiftX; cell.EditorCellY += editorShiftY; }
        foreach (var path in copy.Rivers) Move(path.ControlPoints, shiftX, shiftY);
        foreach (var path in copy.Roads) Move(path.ControlPoints, shiftX, shiftY);
        foreach (var crossing in copy.Crossings) { crossing.X += shiftX; crossing.Y += shiftY; }
        foreach (var item in copy.WorldObjectPlacements) { item.WorldSurfaceX += shiftX; item.WorldSurfaceY += shiftY; }
        foreach (var item in copy.WorldSiteBlueprintPlacements) { item.SurfaceCellX += shiftX; item.SurfaceCellY += shiftY; }
        foreach (var item in copy.DetailPatchPlacements) { item.SurfaceCellX += shiftX; item.SurfaceCellY += shiftY; }
        copy.SurfaceWidthCells = newWidthCells;
        copy.SurfaceHeightCells = newHeightCells;
        if (copy.RuntimeSurface != null)
        {
            copy.RuntimeSurface.OriginWorldX -= shiftX * copy.RuntimeSurface.SurfaceCellWorldSize;
            copy.RuntimeSurface.OriginWorldY -= shiftY * copy.RuntimeSurface.SurfaceCellWorldSize;
        }

        errors.AddRange(FindOutOfBounds(copy, linked));
        return errors.Count == 0 ? new(copy, errors) : new(null, errors);
    }

    private static void Move(IEnumerable<PathControlPoint> points, int dx, int dy)
    {
        foreach (var point in points) { point.X += dx; point.Y += dy; }
    }

    private static IEnumerable<string> FindOutOfBounds(WorldCompositionDocument document, LinkedSourceSet linked)
    {
        var errors = new List<string>();
        foreach (var cell in document.CoarseTerrainSources)
            if (cell.EditorCellX < 0 || cell.EditorCellY < 0 || cell.EditorCellX >= document.WorldEditorGridWidth || cell.EditorCellY >= document.WorldEditorGridHeight)
                errors.Add($"基础地形格 ({cell.EditorCellX},{cell.EditorCellY}) 超出新世界范围。");
        foreach (var cell in document.CoarseFeatureSources)
            if (cell.EditorCellX < 0 || cell.EditorCellY < 0 || cell.EditorCellX >= document.WorldEditorGridWidth || cell.EditorCellY >= document.WorldEditorGridHeight)
                errors.Add($"森林覆盖格 ({cell.EditorCellX},{cell.EditorCellY}) 超出新世界范围。");
        foreach (var path in document.Rivers)
            if (path.ControlPoints.Any(p => Outside(p.X, p.Y, document)))
                errors.Add($"河流 {path.PathId} 的部分控制点超出新世界范围。");
        foreach (var path in document.Roads)
            if (path.ControlPoints.Any(p => Outside(p.X, p.Y, document)))
                errors.Add($"道路 {path.PathId} 的部分控制点超出新世界范围。");
        foreach (var crossing in document.Crossings)
            if (Outside(crossing.X, crossing.Y, document))
                errors.Add($"道路/河流交叉口 {crossing.RoadPathId}/{crossing.RiverPathId} 超出新世界范围。");
        foreach (var item in document.WorldObjectPlacements)
            if (!Inside(item.WorldSurfaceX, item.WorldSurfaceY, item.WidthCells, item.HeightCells, document))
                errors.Add($"世界物件 {item.PlacementId} 超出新世界范围。");
        foreach (var item in document.WorldSiteBlueprintPlacements)
        {
            if (!linked.Blueprints.TryGetValue(item.PlacementId, out var blueprint))
                errors.Add($"据点蓝图 {item.PlacementId} 的链接源不可用，不能安全调整世界尺寸。");
            else
            {
                var width = item.RotationQuarterTurns % 2 == 0 ? blueprint.WidthCells : blueprint.HeightCells;
                var height = item.RotationQuarterTurns % 2 == 0 ? blueprint.HeightCells : blueprint.WidthCells;
                if (!Inside(item.SurfaceCellX, item.SurfaceCellY, width, height, document))
                    errors.Add($"据点蓝图 {item.PlacementId} 的部分内容超出新世界范围。");
            }
        }
        foreach (var item in document.DetailPatchPlacements)
        {
            if (!linked.Patches.TryGetValue(item.PlacementId, out var patch))
                errors.Add($"精修块 {item.PlacementId} 的链接源不可用，不能安全调整世界尺寸。");
            else if (!Inside(item.SurfaceCellX, item.SurfaceCellY, patch.WidthCells, patch.HeightCells, document))
                errors.Add($"精修块 {item.PlacementId} 的部分内容超出新世界范围。");
        }
        return errors;
    }

    private static bool Outside(double x, double y, WorldCompositionDocument document) =>
        x < 0 || y < 0 || x > document.SurfaceWidthCells || y > document.SurfaceHeightCells;

    private static bool Inside(int x, int y, int width, int height, WorldCompositionDocument document) =>
        width > 0 && height > 0 && x >= 0 && y >= 0 && x + width <= document.SurfaceWidthCells && y + height <= document.SurfaceHeightCells;
}
