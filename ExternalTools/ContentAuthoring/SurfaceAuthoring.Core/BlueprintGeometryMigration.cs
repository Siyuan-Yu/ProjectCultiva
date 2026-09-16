using System.Globalization;

namespace SurfaceAuthoring.Core;

public sealed record BlueprintGeometryMigrationResult(int RestoredCount, IReadOnlyList<string> Warnings);

/// <summary>Restores exact legacy object rectangles only while their generated integer envelope remains untouched.</summary>
public static class BlueprintGeometryMigration
{
    public static BlueprintGeometryMigrationResult RestoreLegacyExactGeometry(
        WorldCompositionDocument composition, WorldSiteBlueprintPlacement placement, WorldSiteBlueprintDocument blueprint)
    {
        var warnings = new List<string>();
        if (composition.RuntimeSurface is not { } binding) return new(0, warnings);
        var restored = 0;
        foreach (var item in blueprint.ObjectPlacements)
        {
            if (!TryLegacyRect(item, out var legacy) || item.Metadata.TryGetValue("legacyGeometryState", out var state) && state == "edited") continue;
            if (state == "exact") continue;

            var envelope = ImportedEnvelope(legacy, placement, binding);
            if (!Matches(item, envelope))
            {
                warnings.Add($"迁移对象 {item.PlacementId} 已被人工编辑，保留当前几何，未恢复旧精确矩形。");
                item.Metadata["legacyGeometryState"] = "edited";
                continue;
            }

            var originX = binding.OriginWorldX + placement.SurfaceCellX * binding.SurfaceCellWorldSize;
            var originY = binding.OriginWorldY + placement.SurfaceCellY * binding.SurfaceCellWorldSize;
            item.LocalSurfaceX = (legacy.X - originX) / binding.SurfaceCellWorldSize;
            item.LocalSurfaceY = (legacy.Y - originY) / binding.SurfaceCellWorldSize;
            item.WidthCells = legacy.Width / binding.SurfaceCellWorldSize;
            item.HeightCells = legacy.Height / binding.SurfaceCellWorldSize;
            item.Metadata["legacyGeometryState"] = "exact";
            restored++;
        }
        if (restored > 0) blueprint.SchemaVersion = WorldSiteBlueprintDocument.CurrentSchemaVersion;
        return new(restored, warnings);
    }

    public static bool TryLegacyRect(BlueprintObjectPlacement item, out (double X, double Y, double Width, double Height) rect)
    {
        rect = default;
        return TryNumber(item.Metadata, "legacyWorldX", out rect.X) && TryNumber(item.Metadata, "legacyWorldY", out rect.Y) &&
               TryNumber(item.Metadata, "legacyWorldWidth", out rect.Width) && TryNumber(item.Metadata, "legacyWorldHeight", out rect.Height) &&
               rect.Width > 0 && rect.Height > 0;
    }

    private static (double X, double Y, double Width, double Height) ImportedEnvelope(
        (double X, double Y, double Width, double Height) legacy, WorldSiteBlueprintPlacement placement, RuntimeSurfaceBinding binding)
    {
        var x0 = FloorCell(legacy.X, binding.OriginWorldX, binding.SurfaceCellWorldSize);
        var y0 = FloorCell(legacy.Y, binding.OriginWorldY, binding.SurfaceCellWorldSize);
        var x1 = CeilingCell(legacy.X + legacy.Width, binding.OriginWorldX, binding.SurfaceCellWorldSize);
        var y1 = CeilingCell(legacy.Y + legacy.Height, binding.OriginWorldY, binding.SurfaceCellWorldSize);
        return (x0 - placement.SurfaceCellX, y0 - placement.SurfaceCellY, Math.Max(1, x1 - x0), Math.Max(1, y1 - y0));
    }

    private static bool Matches(BlueprintObjectPlacement item, (double X, double Y, double Width, double Height) expected) =>
        Near(item.LocalSurfaceX, expected.X) && Near(item.LocalSurfaceY, expected.Y) && Near(item.WidthCells, expected.Width) && Near(item.HeightCells, expected.Height);
    private static bool Near(double a, double b) => Math.Abs(a - b) <= 1e-6;
    private static int FloorCell(double value, double origin, double cell) => (int)Math.Floor((value - origin) / cell + 1e-5);
    private static int CeilingCell(double value, double origin, double cell) => (int)Math.Ceiling((value - origin) / cell - 1e-5);
    private static bool TryNumber(IReadOnlyDictionary<string, string> metadata, string key, out double value)
    {
        value = 0;
        return metadata.TryGetValue(key, out var text) && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
