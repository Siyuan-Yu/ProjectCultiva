using System;
using XianXia.Core.Construction;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.Npc
{
    /// <summary>Actual walls obstruct sight; furniture and occupants do not.</summary>
    public static class WorldObservation
    {
        public static bool HasLineOfSight(SimulationWorld world, string surface, WorldVec2 from, WorldVec2 to)
        {
            if (!world.SurfaceGround.TryGet(surface, out var ground)) return false;
            var cell = ground.CellSize;
            var steps = Math.Max(1, (int)Math.Ceiling(WorldVec2.Distance(from, to) / (cell * .5f)));
            for (var i = 1; i < steps; i++)
            {
                var t = (float)i / steps;
                if (!ground.IsWalkable(from.X + (to.X-from.X)*t, from.Y + (to.Y-from.Y)*t)) return false;
            }
            foreach (var asset in world.OutdoorConstructedAssets.QueryOverlapping(surface,
                Math.Min(from.X,to.X)-cell*.01f, Math.Min(from.Y,to.Y)-cell*.01f,
                Math.Abs(to.X-from.X)+cell*.02f, Math.Abs(to.Y-from.Y)+cell*.02f))
            {
                if (world.OutdoorStatefulObjects.IsDestructibleDestroyed(asset.StableAssetId)) continue;
                if (RoomBedGeometry.IsRoom(asset))
                {
                    for (var y=0; y<asset.CellsH; y++) for(var x=0; x<asset.CellsW; x++)
                    {
                        if (!RoomBedGeometry.IsRoomWallCell(asset,x,y) ||
                            world.OutdoorStatefulObjects.IsDestructibleDestroyed(RoomBedGeometry.RoomWallCellId(asset,x,y))) continue;
                        for(var part=0;part<2;part++)
                            if(RoomBedGeometry.TryRoomWallRectangle(asset,x,y,cell,part==1,out var wx,out var wy,out var ww,out var wh) &&
                                Intersects(from,to,wx,wy,ww,wh)) return false;
                    }
                }
                else if ((asset.Kind == OutdoorConstructedAssetSemantics.WallSegmentKind ||
                          asset.Kind == OutdoorConstructedAssetSemantics.StorageRoomKind) &&
                         Intersects(from,to,asset.WorldX,asset.WorldY,asset.WorldWidth,asset.WorldHeight)) return false;
            }
            return true;
        }
        static bool Intersects(WorldVec2 a, WorldVec2 b, float x,float y,float w,float h)
        {
            var low=0f; var high=1f;
            return Clip(a.X,b.X-a.X,x,x+w,ref low,ref high) && Clip(a.Y,b.Y-a.Y,y,y+h,ref low,ref high);
        }
        static bool Clip(float origin,float delta,float min,float max,ref float low,ref float high)
        {
            if(Math.Abs(delta)<.0000001f) return origin>=min && origin<=max;
            var a=(min-origin)/delta; var b=(max-origin)/delta;
            if(a>b){var swap=a;a=b;b=swap;}
            low=Math.Max(low,a); high=Math.Min(high,b); return low<=high;
        }
    }
}
