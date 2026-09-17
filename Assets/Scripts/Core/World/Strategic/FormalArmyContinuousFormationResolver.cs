using System;
using System.Collections.Generic;
using XianXia.Core.World;
using XianXia.Core.World.Surface;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Transient, deterministic formation positions derived from Army.WorldMotion.</summary>
    public static class FormalArmyContinuousFormationResolver
    {
        public static WorldVec2 Resolve(FormalArmyWorldMotion motion, int stableSlot,
            SurfaceGroundNavigation navigation, float spacingCells = 3f,
            IReadOnlyList<WorldVec2> trail = null)
        {
            var anchor = motion.WorldPosition;
            if (stableSlot <= 0 || navigation == null) return anchor;
            var spacing = Math.Max(.001f, navigation.CellSize * spacingCells);
            var distance = stableSlot * spacing;
            var current = anchor;
            if (trail != null)
                for (var i = trail.Count - 2; i >= 0; i--)
                    if (TryFollow(previous: trail[i], ref current, ref distance,
                            navigation, anchor, out var point))
                        return point;

            var path = motion.SurfacePath;
            for (var i = Math.Min(motion.SurfaceWaypointIndex - 1, path.Count - 1); i >= 0; i--)
                if (TryFollow(path[i], ref current, ref distance,
                        navigation, anchor, out var point))
                    return point;

            if (WorldVec2.Distance(current, anchor) >= .01f)
                return anchor;
            return ResolveConnectedSlot(anchor, stableSlot, navigation, spacingCells);
        }

        /// <summary>Shared stationary group slot for Army and PlayerParty presentation.</summary>
        public static WorldVec2 ResolveConnectedSlot(WorldVec2 anchor, int stableSlot,
            SurfaceGroundNavigation navigation, float spacingCells = 3f)
        {
            if (stableSlot <= 0 || navigation == null) return anchor;
            var spacing = Math.Max(.001f, navigation.CellSize * spacingCells);
            // Bounded local search; candidate and direct anchor connection must be walkable.
            for (var ring = 1; ring <= 4; ring++)
                for (var offset = 0; offset < 8; offset++)
                {
                    var direction = (stableSlot + offset) & 7;
                    var angle = direction * Math.PI * .25;
                    var radius = spacing * ring;
                    var candidate = new WorldVec2(
                        anchor.X + (float)Math.Cos(angle) * radius,
                        anchor.Y + (float)Math.Sin(angle) * radius);
                    if (navigation.IsWalkable(candidate.X, candidate.Y) &&
                        navigation.IsSegmentWalkable(anchor.X, anchor.Y,
                            candidate.X, candidate.Y))
                        return candidate;
                }
            return anchor;
        }

        static bool TryFollow(WorldVec2 previous, ref WorldVec2 current, ref float distance,
            SurfaceGroundNavigation navigation, WorldVec2 anchor, out WorldVec2 point)
        {
            point = default;
            var length = WorldVec2.Distance(current, previous);
            if (length >= distance && length > .0001f)
            {
                var t = distance / length;
                point = new WorldVec2(current.X + (previous.X - current.X) * t,
                    current.Y + (previous.Y - current.Y) * t);
                return navigation.IsWalkable(point.X, point.Y) &&
                       navigation.IsSegmentWalkable(anchor.X, anchor.Y, point.X, point.Y);
            }
            distance -= length;
            current = previous;
            return false;
        }
    }
}
