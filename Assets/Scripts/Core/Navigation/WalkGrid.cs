using System;

namespace XianXia.Core.Navigation
{
    /// <summary>
    /// Axis-aligned walkability grid in presentation XY (no Unity).
    /// Cell (0,0) covers [OriginX, OriginX+CellSize) × [OriginY, OriginY+CellSize).
    /// </summary>
    public sealed class WalkGrid
    {
        readonly bool[] _blocked;

        public WalkGrid(float originX, float originY, float cellSize, int width, int height)
        {
            if (cellSize <= 0f)
                throw new ArgumentOutOfRangeException(nameof(cellSize));
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException(nameof(width));

            OriginX = originX;
            OriginY = originY;
            CellSize = cellSize;
            Width = width;
            Height = height;
            _blocked = new bool[width * height];
        }

        public float OriginX { get; }
        public float OriginY { get; }
        public float CellSize { get; }
        public int Width { get; }
        public int Height { get; }
        public int Revision { get; private set; }

        public int BlockedCount
        {
            get
            {
                var n = 0;
                for (var i = 0; i < _blocked.Length; i++)
                {
                    if (_blocked[i])
                        n++;
                }

                return n;
            }
        }

        public bool InBounds(int cx, int cy) =>
            cx >= 0 && cy >= 0 && cx < Width && cy < Height;

        public bool IsWalkable(int cx, int cy) =>
            InBounds(cx, cy) && !_blocked[Index(cx, cy)];

        public void SetBlocked(int cx, int cy, bool blocked)
        {
            if (!InBounds(cx, cy))
                return;
            if (_blocked[Index(cx, cy)] == blocked)
                return;
            _blocked[Index(cx, cy)] = blocked;
            Revision++;
        }

        public void SetBlockedRect(int minCx, int minCy, int maxCxInclusive, int maxCyInclusive, bool blocked)
        {
            for (var y = minCy; y <= maxCyInclusive; y++)
            for (var x = minCx; x <= maxCxInclusive; x++)
                SetBlocked(x, y, blocked);
        }

        public bool TryWorldToCell(float worldX, float worldY, out int cx, out int cy)
        {
            cx = (int)Math.Floor((worldX - OriginX) / CellSize);
            cy = (int)Math.Floor((worldY - OriginY) / CellSize);
            return InBounds(cx, cy);
        }

        public void CellToWorldCenter(int cx, int cy, out float worldX, out float worldY)
        {
            worldX = OriginX + (cx + 0.5f) * CellSize;
            worldY = OriginY + (cy + 0.5f) * CellSize;
        }

        /// <summary>Nearest walkable cell by BFS ring; false if none within maxRadius cells.</summary>
        public bool TryFindNearestWalkable(int cx, int cy, int maxRadius, out int outX, out int outY)
        {
            outX = cx;
            outY = cy;
            if (IsWalkable(cx, cy))
                return true;

            for (var r = 1; r <= maxRadius; r++)
            {
                for (var dy = -r; dy <= r; dy++)
                for (var dx = -r; dx <= r; dx++)
                {
                    if (Math.Abs(dx) != r && Math.Abs(dy) != r)
                        continue;
                    var x = cx + dx;
                    var y = cy + dy;
                    if (!IsWalkable(x, y))
                        continue;
                    outX = x;
                    outY = y;
                    return true;
                }
            }

            return false;
        }

        int Index(int cx, int cy) => cy * Width + cx;
    }
}
