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
        // Placement coordinates are floats. Farther SurfaceChunk coordinates can accumulate about
        // 0.00012 cell of error while still representing the same authored integer lattice.
        // Keep this cell-relative and far below any meaningful placement offset.
        const double CellAlignmentTolerance = 0.001;

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
            var job = new Job(inputs);
            while (!job.IsComplete)
                job.Step(int.MaxValue);
            return job.Result;
        }

        /// <summary>
        /// Incremental pure-data composer.  Hosts may time-slice <see cref="Step"/> without
        /// touching Unity APIs; the synchronous API above simply consumes the same job at once.
        /// </summary>
        public sealed class Job
        {
            readonly IReadOnlyList<Input> _inputs;
            readonly bool[] _covered;
            readonly bool[] _blocked;
            readonly int[] _offsetX;
            readonly int[] _offsetY;
            readonly float _minX;
            readonly float _minY;
            readonly float _cellSize;
            readonly int _width;
            readonly int _height;
            int _inputIndex;
            int _cellIndex;
            int _outputIndex;

            public Job(IReadOnlyList<Input> inputs)
            {
                if (inputs == null || inputs.Count == 0)
                    throw new ArgumentException("At least one WalkGrid input is required.", nameof(inputs));
                _inputs = inputs;
                var first = inputs[0].Grid ?? throw new ArgumentException("WalkGrid input is null.", nameof(inputs));
                _cellSize = first.CellSize;
                var minX = float.PositiveInfinity;
                var minY = float.PositiveInfinity;
                var maxX = float.NegativeInfinity;
                var maxY = float.NegativeInfinity;
                for (var i = 0; i < inputs.Count; i++)
                {
                    var input = inputs[i];
                    var grid = input.Grid ?? throw new ArgumentException("WalkGrid input is null.", nameof(inputs));
                    if (Math.Abs(grid.CellSize - _cellSize) > 0.0001f)
                        throw new InvalidOperationException("WalkGrid cell sizes must be compatible.");
                    minX = Math.Min(minX, grid.OriginX + input.PlacementX);
                    minY = Math.Min(minY, grid.OriginY + input.PlacementY);
                    maxX = Math.Max(maxX, grid.OriginX + input.PlacementX + grid.Width * _cellSize);
                    maxY = Math.Max(maxY, grid.OriginY + input.PlacementY + grid.Height * _cellSize);
                }
                _minX = minX; _minY = minY;
                _width = ToCellCount(maxX - minX, _cellSize);
                _height = ToCellCount(maxY - minY, _cellSize);
                _offsetX = new int[inputs.Count]; _offsetY = new int[inputs.Count];
                for (var i = 0; i < inputs.Count; i++)
                {
                    _offsetX[i] = ToAlignedCellOffset(inputs[i].Grid.OriginX + inputs[i].PlacementX - minX, _cellSize);
                    _offsetY[i] = ToAlignedCellOffset(inputs[i].Grid.OriginY + inputs[i].PlacementY - minY, _cellSize);
                }
                _covered = new bool[_width * _height];
                _blocked = new bool[_width * _height];
                Result = new WalkGrid(_minX, _minY, _cellSize, _width, _height);
            }

            public WalkGrid Result { get; }
            public int Width => _width;
            public int Height => _height;
            public int InputCount => _inputs.Count;
            public bool IsComplete { get; private set; }

            public bool Step(int cellBudget)
            {
                if (IsComplete) return true;
                var remaining = Math.Max(1, cellBudget);
                while (remaining > 0 && _inputIndex < _inputs.Count)
                {
                    var input = _inputs[_inputIndex];
                    var grid = input.Grid;
                    var count = grid.Width * grid.Height;
                    while (remaining > 0 && _cellIndex < count)
                    {
                        var x = _cellIndex % grid.Width;
                        var y = _cellIndex / grid.Width;
                        var index = (_offsetY[_inputIndex] + y) * _width + _offsetX[_inputIndex] + x;
                        _covered[index] = true;
                        if (!grid.IsWalkable(x, y)) _blocked[index] = true;
                        _cellIndex++; remaining--;
                    }
                    if (_cellIndex == count) { _inputIndex++; _cellIndex = 0; }
                }
                while (remaining > 0 && _inputIndex == _inputs.Count && _outputIndex < _covered.Length)
                {
                    if (!_covered[_outputIndex] || _blocked[_outputIndex])
                        Result.SetBlocked(_outputIndex % _width, _outputIndex / _width, true);
                    _outputIndex++; remaining--;
                }
                IsComplete = _inputIndex == _inputs.Count && _outputIndex == _covered.Length;
                return IsComplete;
            }
        }

        static int ToAlignedCellOffset(float offset, float cellSize)
        {
            RequireAligned(offset, cellSize);
            return (int)Math.Round((double)offset / cellSize, MidpointRounding.AwayFromZero);
        }

        static int ToCellCount(float extent, float cellSize)
        {
            var cells = (double)extent / cellSize;
            var count = (int)Math.Round(cells, MidpointRounding.AwayFromZero);
            if (count < 1 || Math.Abs(cells - count) > CellAlignmentTolerance)
                throw new InvalidOperationException("WalkGrid placement must align to cell boundaries.");
            return count;
        }

        static void RequireAligned(float offset, float cellSize)
        {
            var cells = (double)offset / cellSize;
            if (Math.Abs(cells - Math.Round(cells, MidpointRounding.AwayFromZero)) >
                CellAlignmentTolerance)
                throw new InvalidOperationException("WalkGrid placement must align to the shared cell lattice.");
        }
    }
}
