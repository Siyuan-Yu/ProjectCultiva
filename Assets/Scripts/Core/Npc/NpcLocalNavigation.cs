using System;
using System.Collections.Generic;
using XianXia.Core.Construction;
using XianXia.Core.Navigation;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Surface;

namespace XianXia.Core.Npc
{
    /// <summary>Bounded cold-local navigation uses the same room-wall/door/bed geometry as the Host.</summary>
    public static class NpcLocalNavigation
    {
        const int MaxCells = 256;
        static WalkGrid Build(SimulationWorld world, SurfaceGroundNavigation ground, WorldVec2 from, WorldVec2 to)
        {
            var cell = ground.CellSize;
            var x0 = Math.Max(0, (int)Math.Floor((Math.Min(from.X, to.X) - ground.OriginX) / cell) - 12);
            var y0 = Math.Max(0, (int)Math.Floor((Math.Min(from.Y, to.Y) - ground.OriginY) / cell) - 12);
            var x1 = Math.Min(ground.Width - 1, (int)Math.Floor((Math.Max(from.X, to.X) - ground.OriginX) / cell) + 12);
            var y1 = Math.Min(ground.Height - 1, (int)Math.Floor((Math.Max(from.Y, to.Y) - ground.OriginY) / cell) + 12);
            if (x1 < x0 || y1 < y0 || x1-x0+1 > MaxCells || y1-y0+1 > MaxCells) return null;
            var grid = new WalkGrid(ground.OriginX + x0 * cell, ground.OriginY + y0 * cell, cell, x1-x0+1, y1-y0+1);
            var restrictOpening = World.Strategic.HuangcunOpeningAccess.IsMovementLocked(world);
            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
            {
                var wx = grid.OriginX + (x + .5f) * cell;
                var wy = grid.OriginY + (y + .5f) * cell;
                grid.SetBlocked(x, y, !ground.IsWalkable(wx, wy) ||
                    restrictOpening && !world.OpeningArea.Contains(ground.SurfaceId, wx, wy));
            }
            foreach (var asset in world.OutdoorConstructedAssets.QueryOverlapping(
                         ground.SurfaceId, grid.OriginX, grid.OriginY,
                         grid.Width * cell, grid.Height * cell))
            {
                if (asset.SurfaceId != ground.SurfaceId ||
                    world.OutdoorStatefulObjects.IsDestructibleDestroyed(asset.StableAssetId) ||
                    asset.WorldX > grid.OriginX + grid.Width*cell || asset.WorldX + asset.WorldWidth < grid.OriginX ||
                    asset.WorldY > grid.OriginY + grid.Height*cell || asset.WorldY + asset.WorldHeight < grid.OriginY) continue;
                if (RoomBedGeometry.IsRoom(asset))
                {
                    for (int y = 0; y < asset.CellsH; y++)
                    for (int x = 0; x < asset.CellsW; x++)
                        if (RoomBedGeometry.IsRoomWallCell(asset, x, y) &&
                            !world.OutdoorStatefulObjects.IsDestructibleDestroyed(RoomBedGeometry.RoomWallCellId(asset, x, y)))
                            for (var part = 0; part < 2; part++)
                                if (RoomBedGeometry.TryRoomWallRectangle(asset, x, y, cell, part == 1,
                                    out var wx, out var wy, out var ww, out var wh))
                                    Block(grid, wx, wy, ww, wh);
                }
                else if (RoomBedGeometry.IsBed(asset) || asset.Kind == OutdoorConstructedAssetSemantics.WallSegmentKind ||
                         asset.Kind == OutdoorConstructedAssetSemantics.StorageRoomKind)
                    Block(grid, asset.WorldX, asset.WorldY, asset.WorldWidth, asset.WorldHeight);
            }
            return grid;
        }

        static void Block(WalkGrid grid, float x, float y, float width, float height)
        {
            var x0 = Math.Max(0, (int)Math.Floor((x-grid.OriginX)/grid.CellSize + .001f));
            var y0 = Math.Max(0, (int)Math.Floor((y-grid.OriginY)/grid.CellSize + .001f));
            var x1 = Math.Min(grid.Width-1, (int)Math.Ceiling((x+width-grid.OriginX)/grid.CellSize - .001f)-1);
            var y1 = Math.Min(grid.Height-1, (int)Math.Ceiling((y+height-grid.OriginY)/grid.CellSize - .001f)-1);
            for (int cy = y0; cy <= y1; cy++) for (int cx = x0; cx <= x1; cx++) grid.SetBlocked(cx, cy, true);
        }

        public static bool HasClearContact(SimulationWorld world, SurfaceGroundNavigation ground, WorldVec2 from, WorldVec2 to)
        {
            var grid = Build(world, ground, from, to);
            return grid != null && GridPathfinder.IsWorldSegmentWalkable(grid, from.X, from.Y, to.X, to.Y);
        }

        public static bool TryLandingPoint(SimulationWorld world, SurfaceGroundNavigation ground,
            WorldVec2 from, Func<WorldVec2, bool> allowed, out WorldVec2 point)
        {
            point = from;
            var grid = Build(world, ground, from, from);
            if (grid == null || !grid.TryWorldToCell(from.X, from.Y, out var cx, out var cy)) return false;
            if (grid.IsWalkable(cx, cy) && allowed(from)) return true;
            for (var radius = 1; radius <= 12; radius++)
                for (var y = -radius; y <= radius; y++)
                    for (var x = -radius; x <= radius; x++)
                    {
                        if (Math.Abs(x) != radius && Math.Abs(y) != radius || !grid.IsWalkable(cx + x, cy + y)) continue;
                        grid.CellToWorldCenter(cx + x, cy + y, out var wx, out var wy);
                        var candidate = new WorldVec2(wx, wy);
                        if (allowed(candidate)) { point = candidate; return true; }
                    }
            return false;
        }

        public static bool TryRoute(SimulationWorld world, SurfaceGroundNavigation ground,
            WorldVec2 from, WorldVec2 to, List<WorldVec2> route)
        {
            route.Clear();
            var grid = Build(world, ground, from, to);
            if (grid == null) return false; // Never allocate a whole expanded world for a local action.
            var path = new List<float>();
            if (!new GridPathfinderWorkspace().TryFindWorldPath(grid, from.X, from.Y, to.X, to.Y, path, 0, 0)) return false;
            route.Add(from);
            for (int i = 0; i + 1 < path.Count; i += 2) route.Add(new WorldVec2(path[i], path[i+1]));
            if (!route[route.Count-1].Equals(to)) route.Add(to);
            return true;
        }
    }
}
