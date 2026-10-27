using System;
using System.Collections.Generic;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Npc
{
    /// <summary>ADR-0071. One persistent route per Site in the existing content-progress namespace.</summary>
    public static class SettlementPatrolRoutes
    {
        public const int MaxPoints = 64;
        public static string Key(string siteId) => "base:patrol-route:" + siteId;
        public static int WaitTicks(int count) => Math.Max(2, Math.Min(24, 48 / Math.Max(1, count)));
        public static int Revision(SimulationWorld world, string siteId) => world.ContentCounters.Get(Key(siteId) + ":revision");

        public static bool CanEdit(SimulationWorld world, string siteId) =>
            world != null && !string.IsNullOrEmpty(world.Strategic.PlayerFactionId) &&
            world.Strategic.Sites.TryGet(siteId ?? string.Empty, out var site) && site.IsCoreActive &&
            site.HasContinuousCore && site.OwnerFactionId == world.Strategic.PlayerFactionId;

        public static List<WorldVec2> Get(SimulationWorld world, string siteId)
        {
            EnsureDefault(world, siteId);
            var points = new List<WorldVec2>();
            var key = Key(siteId);
            var count = Math.Min(MaxPoints, world.ContentCounters.Get(key + ":count"));
            for (var i = 0; i < count; i++)
            {
                var p = key + ":" + i;
                var sign = world.ContentCounters.Get(p + ":sign");
                points.Add(new WorldVec2(world.ContentCounters.Get(p + ":x") / 1000000f * ((sign & 1) == 0 ? 1 : -1),
                    world.ContentCounters.Get(p + ":y") / 1000000f * ((sign & 2) == 0 ? 1 : -1)));
            }
            return points;
        }

        public static bool ValidatePoint(SimulationWorld world, string siteId, WorldVec2 point, out string reason)
        {
            reason = "巡视点须位于本城可通行的地面。";
            if (float.IsNaN(point.X) || float.IsNaN(point.Y) || float.IsInfinity(point.X) || float.IsInfinity(point.Y) ||
                Math.Abs((double)point.X * 1000000) > int.MaxValue || Math.Abs((double)point.Y * 1000000) > int.MaxValue ||
                !world.Strategic.Sites.TryGet(siteId, out var site) || !site.HasContinuousCore ||
                !WorldSiteAdministrativeControlResolver.TryResolve(world, site.CoreSurfaceId, point.X, point.Y, out var actual, out _) ||
                actual?.SiteId != siteId || !world.SurfaceGround.TryGet(site.CoreSurfaceId, out var ground) ||
                !ground.IsWalkable(point.X, point.Y) || !NpcLocalNavigation.HasClearContact(world, ground, point, point)) return false;
            reason = string.Empty;
            return true;
        }

        public static Result Save(SimulationWorld world, string siteId, IReadOnlyList<WorldVec2> points)
        {
            if (!CanEdit(world, siteId)) return Result.Failure(ErrorCode.InvalidOperation, "仅可修改我方已占领城池的巡视点。");
            if (points == null || points.Count > MaxPoints) return Result.Failure(ErrorCode.InvalidArgument, "巡视点最多64个。");
            foreach (var point in points)
                if (!ValidatePoint(world, siteId, point, out var reason)) return Result.Failure(ErrorCode.InvalidArgument, reason);
            Write(world, siteId, points);
            return Result.Success();
        }

        static void Write(SimulationWorld world, string siteId, IReadOnlyList<WorldVec2> points)
        {
            var key = Key(siteId);
            world.ContentCounters.Set(key + ":configured", 1);
            world.ContentCounters.Set(key + ":count", points.Count);
            world.ContentCounters.Add(key + ":revision", 1);
            for (var i = 0; i < points.Count; i++)
            {
                var p = key + ":" + i;
                world.ContentCounters.Set(p + ":x", (int)Math.Round(Math.Abs((double)points[i].X) * 1000000));
                world.ContentCounters.Set(p + ":y", (int)Math.Round(Math.Abs((double)points[i].Y) * 1000000));
                world.ContentCounters.Set(p + ":sign", (points[i].X < 0 ? 1 : 0) | (points[i].Y < 0 ? 2 : 0));
            }
        }

        static void EnsureDefault(SimulationWorld world, string siteId)
        {
            if (siteId != "base:site_huangcun" || world.ContentCounters.Get(Key(siteId) + ":configured") != 0 ||
                !world.Strategic.Sites.TryGet(siteId, out var site) || !site.HasContinuousCore ||
                !world.SurfaceGround.TryGet(site.CoreSurfaceId, out var ground)) return;
            var dx = site.CoreRangeWidth * .5f - ground.CellSize * 2.5f;
            var dy = site.CoreRangeHeight * .5f - ground.CellSize * 2.5f;
            var offsets = new[] { new WorldVec2(-dx,-dy), new WorldVec2(0,-dy), new WorldVec2(dx,-dy),
                new WorldVec2(dx,0), new WorldVec2(dx,dy), new WorldVec2(0,dy), new WorldVec2(-dx,dy), new WorldVec2(-dx,0) };
            var points = new List<WorldVec2>();
            foreach (var offset in offsets)
            {
                var preferred = new WorldVec2(site.CoreWorldX + offset.X, site.CoreWorldY + offset.Y);
                if (!NpcLocalNavigation.TryLandingPoint(world, ground, preferred,
                    p => WorldSitePhysicalRegionQuery.ResolveSiteIdOrEmpty(world, p) == siteId, out var point)) return;
                points.Add(point);
            }
            Write(world, siteId, points);
        }
    }
}
