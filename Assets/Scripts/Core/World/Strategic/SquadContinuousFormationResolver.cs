using System;
using XianXia.Core.Exploration;
using XianXia.Core.Domain.Ids;
using XianXia.Core.World;
using XianXia.Core.World.Surface;

namespace XianXia.Core.World.Strategic
{
    public static class SquadContinuousFormationResolver
    {
        public static int StableSlot(SquadState squad, EntityId member)
        {
            if (squad == null || member.IsNone || !squad.Contains(member)) return -1;
            var slot = 0;
            for (var i = 0; i < squad.MemberCharacterIds.Count; i++)
                if (squad.MemberCharacterIds[i] < member.Value) slot++;
            return slot;
        }

        public static WorldVec2 ResolveConnectedSlot(WorldVec2 anchor, int stableSlot,
            SurfaceGroundNavigation navigation)
        {
            if (navigation == null || stableSlot <= 0) return anchor;
            var spacing = Math.Max(.01f, navigation.CellSize * 3f);
            var row = (stableSlot + 1) / 2;
            var side = stableSlot % 2 == 1 ? -1f : 1f;
            var candidate = new WorldVec2(anchor.X + side * row * spacing, anchor.Y - row * spacing);
            return navigation.Contains(candidate.X, candidate.Y) && navigation.IsWalkable(candidate.X, candidate.Y) &&
                   navigation.IsSegmentWalkable(anchor.X, anchor.Y, candidate.X, candidate.Y)
                ? candidate : anchor;
        }

        public static WorldVec2 Resolve(SquadWorldMotionState motion, int stableSlot,
            SurfaceGroundNavigation navigation)
        {
            if (motion == null || navigation == null || stableSlot <= 0) return motion?.WorldPosition ?? default;
            var spacing = Math.Max(.01f, navigation.CellSize * 3f);
            var row = (stableSlot + 1) / 2;
            var side = stableSlot % 2 == 1 ? -1f : 1f;
            var candidate = new WorldVec2(motion.WorldPosition.X + side * row * spacing,
                motion.WorldPosition.Y - row * spacing);
            return navigation.Contains(candidate.X, candidate.Y) && navigation.IsWalkable(candidate.X, candidate.Y) &&
                   navigation.IsSegmentWalkable(motion.WorldPosition.X, motion.WorldPosition.Y,
                       candidate.X, candidate.Y)
                ? candidate : motion.WorldPosition;
        }
    }
}
