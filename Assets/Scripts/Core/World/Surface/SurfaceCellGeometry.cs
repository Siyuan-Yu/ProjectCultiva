using System;

namespace XianXia.Core.World.Surface
{
    /// <summary>Logical cell geometry, independent from art scale and canonical world units.</summary>
    public static class SurfaceCellGeometry
    {
        public const int CharacterWidthCells = 1;
        public const int CharacterHeightCells = 1;
        // Canonical floats at the far edge of a large Surface lose a few 1/1000 cells.
        public const double CellTolerance = .002;

        public static bool IsInteger(double cells) => !double.IsNaN(cells) && !double.IsInfinity(cells) &&
            Math.Abs(cells - Math.Round(cells)) <= CellTolerance;

        public static bool IsAlignedRectangle(double x, double y, double width, double height,
            double originX, double originY, double cellSize) => cellSize > 0 && width > 0 && height > 0 &&
            IsInteger((x - originX) / cellSize) && IsInteger((y - originY) / cellSize) &&
            IsInteger(width / cellSize) && IsInteger(height / cellSize) &&
            Math.Round(width / cellSize) >= 1 && Math.Round(height / cellSize) >= 1;

        public static WorldVec2 SnapCenter(WorldVec2 position, double originX, double originY,
            double cellSize, int widthCells = 1, int heightCells = 1)
        {
            var halfW = widthCells * .5;
            var halfH = heightCells * .5;
            return new WorldVec2(
                (float)(originX + (Math.Round((position.X-originX)/cellSize-halfW) + halfW) * cellSize),
                (float)(originY + (Math.Round((position.Y-originY)/cellSize-halfH) + halfH) * cellSize));
        }
    }
}
