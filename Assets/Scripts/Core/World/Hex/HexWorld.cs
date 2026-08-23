using System;
using System.Collections.Generic;

namespace XianXia.Core.World.Hex
{
    /// <summary>
    /// Hex 战略世界真源：矩形网格使用紧凑数组 O(1) 索引；稀疏格仍可用字典扩展。
    /// </summary>
    public sealed class HexWorld
    {
        readonly Dictionary<HexCoord, HexCell> _sparseCells = new Dictionary<HexCoord, HexCell>();
        readonly List<HexCoord> _neighborScratch = new List<HexCoord>(6);

        HexCell[] _compactCells;
        bool _compactValid;

        public string MapId { get; set; } = string.Empty;
        public string MapName { get; set; } = string.Empty;
        public float HexSize { get; set; } = HexWorldScale.DefaultHexOuterRadius;
        public int Width { get; set; }
        public int Height { get; set; }

        public bool HasGrid => !string.IsNullOrEmpty(MapId) && CellCount > 0;
        public int CellCount => _compactValid ? _compactCells.Length : _sparseCells.Count;

        public bool UsesCompactStorage => _compactValid;

        public int ChunkCountX =>
            Width <= 0 ? 0 : (Width + HexWorldScale.RenderChunkSize - 1) / HexWorldScale.RenderChunkSize;

        public int ChunkCountY =>
            Height <= 0 ? 0 : (Height + HexWorldScale.RenderChunkSize - 1) / HexWorldScale.RenderChunkSize;

        public IReadOnlyDictionary<HexCoord, HexCell> Tiles => _sparseCells;
        public IReadOnlyDictionary<HexCoord, HexCell> Cells => _sparseCells;

        public void Clear()
        {
            MapId = string.Empty;
            MapName = string.Empty;
            Width = 0;
            Height = 0;
            _compactCells = null;
            _compactValid = false;
            _sparseCells.Clear();
        }

        public int CoordToIndex(int q, int r) => q + r * Width;

        public int CoordToIndex(HexCoord coord) => CoordToIndex(coord.Q, coord.R);

        public bool TryIndexToCoord(int index, out HexCoord coord)
        {
            coord = default;
            if (!_compactValid || index < 0 || index >= _compactCells.Length)
                return false;
            var r = index / Width;
            var q = index - r * Width;
            coord = new HexCoord(q, r);
            return true;
        }

        public bool IsInBounds(int q, int r) => q >= 0 && r >= 0 && q < Width && r < Height;

        public bool IsInBounds(HexCoord coord) => IsInBounds(coord.Q, coord.R);

        public HexCell GetOrCreate(HexCoord coord)
        {
            if (_compactValid && IsInBounds(coord))
            {
                var index = CoordToIndex(coord);
                var cell = _compactCells[index];
                if (cell == null)
                {
                    cell = new HexCell { Coord = coord };
                    _compactCells[index] = cell;
                }

                return cell;
            }

            if (!_sparseCells.TryGetValue(coord, out var sparse) || sparse == null)
            {
                sparse = new HexCell { Coord = coord };
                _sparseCells[coord] = sparse;
            }

            return sparse;
        }

        public void SetCell(HexCell cell)
        {
            if (cell == null)
                throw new ArgumentNullException(nameof(cell));

            if (_compactValid && IsInBounds(cell.Coord))
                _compactCells[CoordToIndex(cell.Coord)] = cell;
            else
                _sparseCells[cell.Coord] = cell;
        }

        public void SetTile(HexCell cell) => SetCell(cell);

        public bool TryGetCell(HexCoord coord, out HexCell cell)
        {
            cell = null;
            if (_compactValid && IsInBounds(coord))
            {
                cell = _compactCells[CoordToIndex(coord)];
                return cell != null;
            }

            return _sparseCells.TryGetValue(coord, out cell) && cell != null;
        }

        public bool TryGetTile(HexCoord coord, out HexCell cell) => TryGetCell(coord, out cell);

        public bool TryGetCell(int index, out HexCell cell)
        {
            cell = null;
            if (!_compactValid || index < 0 || index >= _compactCells.Length)
                return false;
            cell = _compactCells[index];
            return cell != null;
        }

        public bool Contains(HexCoord coord)
        {
            if (_compactValid && IsInBounds(coord))
                return _compactCells[CoordToIndex(coord)] != null;
            return _sparseCells.ContainsKey(coord);
        }

        public void ForEachCellInChunk(int chunkX, int chunkY, Action<HexCell> action)
        {
            if (!_compactValid || action == null)
                return;

            var size = HexWorldScale.RenderChunkSize;
            var q0 = chunkX * size;
            var r0 = chunkY * size;
            var q1 = Math.Min(q0 + size, Width);
            var r1 = Math.Min(r0 + size, Height);
            for (var r = r0; r < r1; r++)
            {
                for (var q = q0; q < q1; q++)
                {
                    var cell = _compactCells[CoordToIndex(q, r)];
                    if (cell != null)
                        action(cell);
                }
            }
        }

        public IEnumerable<HexCoord> GetNeighborCoords(HexCoord coord)
        {
            HexMath.CollectNeighbors(coord, _neighborScratch);
            for (var i = 0; i < _neighborScratch.Count; i++)
            {
                var neighbor = _neighborScratch[i];
                if (Contains(neighbor))
                    yield return neighbor;
            }
        }

        public IEnumerable<HexCoord> GetNeighbors(HexCoord coord) => GetNeighborCoords(coord);

        public int HexDistance(HexCoord a, HexCoord b) => HexMath.Distance(a, b);

        public void ToWorldPosition(HexCoord coord, out float worldX, out float worldY) =>
            HexMath.ToWorldPosition(coord, HexSize, out worldX, out worldY);

        public HexCoord WorldToHex(float worldX, float worldY) =>
            HexMath.WorldToHex(worldX, worldY, HexSize);

        public void FillRectangle(int width, int height, HexTerrainType terrain = HexTerrainType.Plain)
        {
            if (width < 1 || height < 1)
                throw new ArgumentOutOfRangeException("Hex map size must be positive.");

            Width = width;
            Height = height;
            _sparseCells.Clear();
            _compactCells = new HexCell[width * height];
            _compactValid = true;

            for (var r = 0; r < height; r++)
            {
                for (var q = 0; q < width; q++)
                {
                    var coord = new HexCoord(q, r);
                    _compactCells[CoordToIndex(q, r)] = new HexCell
                    {
                        Coord = coord,
                        Terrain = terrain,
                        IsPassable = HexTerrainCatalog.IsPassableByDefault(terrain),
                    };
                }
            }
        }

        /// <summary>遍历紧凑网格（无分配）。</summary>
        public void ForEachCompactCell(Action<HexCell> action)
        {
            if (!_compactValid || action == null || _compactCells == null)
                return;
            for (var i = 0; i < _compactCells.Length; i++)
            {
                var cell = _compactCells[i];
                if (cell != null)
                    action(cell);
            }
        }
    }
}
