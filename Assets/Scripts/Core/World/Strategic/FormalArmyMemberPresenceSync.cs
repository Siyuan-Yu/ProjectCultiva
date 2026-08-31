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

        /// <summary>
        /// 成员退出 Army 时的 Presence 收口。
        /// 新 authority：被 detach 的角色若处于 Residual life state（Incapacitated / VisibleCorpse），
        /// 直接钉到 army.WorldMotion.CurrentHex（Manual WORLD_COMBAT 已在入场时 exact commit 到
        /// BattleAnchorHex），绝不再被 SetAtWorldPosition / SetAtSite 覆盖成无 ResidualHex 的
        /// AtWorldPosition —— 否则 WorldMap 无 residual marker 且离开再回来无法 rematerialize。
        /// </summary>
        public static void DetachMemberAtArmyLocation(
            SimulationWorld world,
            FormalArmy army,
            EntityId memberId)
        {
            if (world?.WorldPresence == null || army == null || memberId.IsNone)
                return;

            var motion = army.WorldMotion;

            if (StrategicResidualPresenceService.IsResidualLifeCandidate(world, memberId))
            {
                // Residual member：以 army 当前 Hex 为 ResidualHex；无 position 时保留已有合法
                // AtHex presence（不拿 default (0,0) 覆盖），缺失由 Resolve final assert 暴露。
                if (motion.HasPosition)
                    StrategicResidualPresenceService.PlaceCharacterAtResidualHex(
                        world,
                        memberId,
                        motion.CurrentHex);
                return;
            }

            // Living member 普通退出 Army：保持当前旧行为。
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
