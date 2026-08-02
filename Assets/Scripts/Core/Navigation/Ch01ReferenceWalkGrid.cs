namespace XianXia.Core.Navigation
{
    /// <summary>
    /// Demo／Ch01 参考关可行走网格：对齐 HostDemoTileMap 范围，并标记房屋／岩块等障碍。
    /// </summary>
    public static class Ch01ReferenceWalkGrid
    {
        public const float OriginX = -40f;
        public const float OriginY = -25f;
        public const float CellSize = 1f;
        public const int Width = 80;
        public const int Height = 50;

        public static WalkGrid Create()
        {
            var grid = new WalkGrid(OriginX, OriginY, CellSize, Width, Height);

            // 房屋区主体（杂役屋）
            BlockWorldRect(grid, -12f, 7f, -4f, 14f);
            // 枢纽旁小棚／岩
            BlockWorldRect(grid, -2f, -2f, 2f, 2f);
            // 矿洞岩壁（入口留缝）
            BlockWorldRect(grid, -36f, 6f, -31f, 12f);
            // 洞府岩壁（洞口可走）
            BlockWorldRect(grid, 22f, -18f, 30f, -16f);
            // 地图外缘加厚（防贴边穿出感）— 已由 InBounds 限制；此处留空

            // 确保关键地点圆心可走（清障）
            EnsureWalkableWorld(grid, 20f, -12f);   // 农田
            EnsureWalkableWorld(grid, -8f, 10f);    // 房屋前
            EnsureWalkableWorld(grid, -34f, 0f);    // 树林
            EnsureWalkableWorld(grid, -30f, 8f);    // 矿口
            EnsureWalkableWorld(grid, -3f, -15f);   // 药田
            EnsureWalkableWorld(grid, 28f, -12f);   // 灵泉
            EnsureWalkableWorld(grid, 24f, -14f);   // 洞府
            EnsureWalkableWorld(grid, 0f, 0f);      // 枢纽

            return grid;
        }

        static void BlockWorldRect(WalkGrid grid, float minX, float minY, float maxX, float maxY)
        {
            if (!grid.TryWorldToCell(minX, minY, out var x0, out var y0))
            {
                x0 = 0;
                y0 = 0;
            }

            if (!grid.TryWorldToCell(maxX, maxY, out var x1, out var y1))
            {
                x1 = grid.Width - 1;
                y1 = grid.Height - 1;
            }

            if (x0 > x1)
            {
                var t = x0;
                x0 = x1;
                x1 = t;
            }

            if (y0 > y1)
            {
                var t = y0;
                y0 = y1;
                y1 = t;
            }

            grid.SetBlockedRect(x0, y0, x1, y1, true);
        }

        static void EnsureWalkableWorld(WalkGrid grid, float x, float y)
        {
            if (!grid.TryWorldToCell(x, y, out var cx, out var cy))
                return;
            grid.SetBlocked(cx, cy, false);
            // 十字清障，避免地点被整块封死
            grid.SetBlocked(cx + 1, cy, false);
            grid.SetBlocked(cx - 1, cy, false);
            grid.SetBlocked(cx, cy + 1, false);
            grid.SetBlocked(cx, cy - 1, false);
        }
    }
}
