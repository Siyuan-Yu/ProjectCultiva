using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>FormalArmy 成员 World Presence 从 Army Location 派生（单一 Authority）。</summary>
    public static class FormalArmyMemberPresenceSync
    {
        public static void SyncAll(SimulationWorld world, FormalArmy army)
        {
            if (world?.WorldPresence == null || army == null)
                return;

            for (var i = 0; i < army.MemberCharacterIds.Count; i++)
            {
                var memberId = new EntityId(army.MemberCharacterIds[i]);
                if (memberId.IsNone)
                    continue;
                if (!ArmyService.TryGetArmyForCharacter(world, memberId, out var bound) ||
                    bound == null ||
                    !string.Equals(bound.ArmyId, army.ArmyId, System.StringComparison.Ordinal))
                    continue;

                SyncMember(world, army, memberId);
            }
        }

        public static void SyncMember(SimulationWorld world, FormalArmy army, EntityId memberId)
        {
            if (world?.WorldPresence == null || army == null || memberId.IsNone)
                return;

            var motion = army.WorldMotion;
            if (!motion.HasPosition)
                return;

            if (motion.LocationKind == FormalArmyLocationKind.AtWorldSite &&
                !string.IsNullOrEmpty(motion.SiteId))
            {
                world.WorldPresence.SetAtSite(memberId, motion.SiteId);
                return;
            }

            world.WorldPresence.SetAtWorldPosition(memberId, motion.WorldPosition, motion.CurrentHex);
        }

        public static void DetachMemberAtArmyLocation(
            SimulationWorld world,
            FormalArmy army,
            EntityId memberId)
        {
            if (world?.WorldPresence == null || army == null || memberId.IsNone)
                return;

            var motion = army.WorldMotion;
            if (motion.LocationKind == FormalArmyLocationKind.AtWorldSite &&
                !string.IsNullOrEmpty(motion.SiteId))
            {
                world.WorldPresence.SetAtSite(memberId, motion.SiteId);
                return;
            }

            if (motion.HasPosition)
                world.WorldPresence.SetAtWorldPosition(memberId, motion.WorldPosition, motion.CurrentHex);
        }
    }
}
