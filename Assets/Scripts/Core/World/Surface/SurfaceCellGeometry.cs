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
        public const float FurnitureScale = 2f;

        // Widen the serialized float operands before subtracting/dividing. This
        // keeps chunk boundaries identical across Mono and offline .NET tools.
        public static int ChunkCoordinate(float worldPosition, float origin, float chunkSize) =>
            (int)Math.Floor(((double)worldPosition - origin) / chunkSize);

        public static bool IsFurnitureKind(string kind) => kind == "room" || kind == "bed" ||
            kind == "storageRoom" || kind == "recoverySpot" || kind == "prisonDropOff" ||
            kind == "cushion" || kind == "chessBoard" || kind == "alchemyFurnace" ||
            kind == "smithingFurnace" || kind == "controlCore";

        static bool MatchesFurnitureScale(double width, double height, int cellsW, int cellsH,
            double cellSize, double scale) => cellSize > 0 && cellsW > 0 && cellsH > 0 &&
            Math.Abs(width / cellSize - cellsW * scale) <= CellTolerance &&
            Math.Abs(height / cellSize - cellsH * scale) <= CellTolerance;

        public static bool IsScaledFurniture(string kind, double width, double height,
            int cellsW, int cellsH, double cellSize) => IsFurnitureKind(kind) &&
            (MatchesFurnitureScale(width, height, cellsW, cellsH, cellSize, FurnitureScale) ||
             MatchesFurnitureScale(width, height, cellsW, cellsH, cellSize, 1.5));

        public static bool IsAlignedFurnitureRectangle(string kind, double x, double y,
            double width, double height, int cellsW, int cellsH,
            double originX, double originY, double cellSize)
        {
            var scaled = IsScaledFurniture(kind, width, height, cellsW, cellsH, cellSize);
            if (!scaled || !MatchesFurnitureScale(width, height, cellsW, cellsH, cellSize, 1.5))
                return IsAlignedRectangle(x, y, width, height, originX, originY, cellSize);
            // Keep the tolerance in Surface cells; using a smaller local grid must not
            // double float error at the far edge of a large world.
            var gridX = (x-originX)/cellSize;
            var gridY = (y-originY)/cellSize;
            return Math.Abs(gridX-Math.Round(gridX*2)*.5) <= CellTolerance &&
                Math.Abs(gridY-Math.Round(gridY*2)*.5) <= CellTolerance;
        }

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
