using System;
using System.Collections.Generic;

namespace XianXia.Core.Navigation
{
    /// <summary>
    /// Pure Core composition for multiple presentation-placed WalkGrids.
    /// It does not select loaded surfaces or alter movement authority.
    /// </summary>
    public static class WalkGridComposer
    {
        public readonly struct Input
        {
            public Input(WalkGrid grid, float placementX, float placementY)
            {
                Grid = grid ?? throw new ArgumentNullException(nameof(grid));
                PlacementX = placementX;
                PlacementY = placementY;
            }

            public WalkGrid Grid { get; }
            public float PlacementX { get; }
            public float PlacementY { get; }
        }

        /// <summary>
        /// Creates the union grid. Cells outside every input are blocked; overlapping inputs
        /// use the conservative rule that any blocked source blocks the composite cell.
        /// </summary>
        public static WalkGrid Compose(IReadOnlyList<Input> inputs)
        {
            if (inputs == null || inputs.Count == 0)
                throw new ArgumentException("At least one WalkGrid input is required.", nameof(inputs));

            var first = inputs[0].Grid ?? throw new ArgumentException("WalkGrid input is null.", nameof(inputs));
            var cellSize = first.CellSize;
            var minX = float.PositiveInfinity;
            var minY = float.PositiveInfinity;
            var maxX = float.NegativeInfinity;
            var maxY = float.NegativeInfinity;

            for (var i = 0; i < inputs.Count; i++)
            {
                var input = inputs[i];
                var grid = input.Grid ?? throw new ArgumentException("WalkGrid input is null.", nameof(inputs));
                if (Math.Abs(grid.CellSize - cellSize) > 0.0001f)
                    throw new InvalidOperationException("WalkGrid cell sizes must be compatible.");

                minX = Math.Min(minX, grid.OriginX + input.PlacementX);
                minY = Math.Min(minY, grid.OriginY + input.PlacementY);
                maxX = Math.Max(maxX, grid.OriginX + input.PlacementX + grid.Width * cellSize);
                maxY = Math.Max(maxY, grid.OriginY + input.PlacementY + grid.Height * cellSize);
            }

            var width = ToCellCount(maxX - minX, cellSize);
            var height = ToCellCount(maxY - minY, cellSize);
            for (var i = 0; i < inputs.Count; i++)
            {
                var grid = inputs[i].Grid;
                RequireAligned(grid.OriginX + inputs[i].PlacementX - minX, cellSize);
                RequireAligned(grid.OriginY + inputs[i].PlacementY - minY, cellSize);
            }
            var result = new WalkGrid(minX, minY, cellSize, width, height);
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                result.CellToWorldCenter(x, y, out var worldX, out var worldY);
                var covered = false;
                var anyBlocker = false;
                for (var i = 0; i < inputs.Count; i++)
                {
                    var input = inputs[i];
                    if (!input.Grid.TryWorldToCell(
                            worldX - input.PlacementX,
                            worldY - input.PlacementY,
                            out var sourceX,
                            out var sourceY))
                        continue;
                    covered = true;
                    if (!input.Grid.IsWalkable(sourceX, sourceY))
                        anyBlocker = true;
                }

                result.SetBlocked(x, y, !covered || anyBlocker);
            }
            return result;
        }

        static int ToCellCount(float extent, float cellSize)
        {
            var count = (int)Math.Round(extent / cellSize, MidpointRounding.AwayFromZero);
            if (count < 1 || Math.Abs(count * cellSize - extent) > 0.0001f)
                throw new InvalidOperationException("WalkGrid placement must align to cell boundaries.");
            return count;
        }

        static void RequireAligned(float offset, float cellSize)
        {
            var cells = offset / cellSize;
            if (Math.Abs(cells - Math.Round(cells, MidpointRounding.AwayFromZero)) > 0.0001f)
                throw new InvalidOperationException("WalkGrid placement must align to the shared cell lattice.");
        }
    }
}
