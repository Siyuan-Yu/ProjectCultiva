using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>FormalArmy 成员 World Presence 从 Army Location 派生（单一 Authority）。</summary>
    public static class FormalArmyMemberPresenceSync
    {
        public enum Reason { OrdinaryTick, GroupRelocation }

        public static bool IsArmyEngaged(SimulationWorld world, FormalArmy army)
        {
            if (world?.Strategic?.Participants == null || army == null)
                return false;
            var participants = world.Strategic.Participants;
            if (string.Equals(participants.AttackerArmyId, army.ArmyId, System.StringComparison.Ordinal) ||
                string.Equals(participants.DefenderArmyId, army.ArmyId, System.StringComparison.Ordinal))
                return true;
            for (var i = 0; i < army.MemberCharacterIds.Count; i++)
                if (participants.FindByEntity(new EntityId(army.MemberCharacterIds[i])) != null)
                    return true;
            return false;
        }

        public static bool IsArmyControlledMember(SimulationWorld world, EntityId memberId)
        {
            if (world == null || memberId.IsNone ||
                !ArmyService.TryGetArmyForCharacter(world, memberId, out var army) || army == null)
                return false;
            if (!LingeringBattlefieldPartyService.IsLivingForMacroOrder(world, memberId))
                return false;
            return army.State != FormalArmyState.Garrisoned &&
                   !IsArmyEngaged(world, army);
        }

        public static void SyncAll(SimulationWorld world, FormalArmy army, bool preservePersonalPositions = false)
            => SyncAll(world, army, Reason.OrdinaryTick, preservePersonalPositions);

        public static void SyncAllFromArmyAuthority(SimulationWorld world, FormalArmy army,
            Reason reason)
            => SyncAll(world, army, reason, false);

        static void SyncAll(SimulationWorld world, FormalArmy army, Reason reason,
            bool preservePersonalPositions)
        {
            if (world?.WorldPresence == null || army == null)
                return;

            SquadCommandService.SetExecution(world, army.SquadId,
                army.WorldMotion.IsMoving ? SquadCommandKind.FormalArmyWorldMotion : SquadCommandKind.None,
                army.WorldMotion.IsMoving ? army.LeaderCharacterId : EntityId.None);

            for (var i = 0; i < army.MemberCharacterIds.Count; i++)
            {
                var memberId = new EntityId(army.MemberCharacterIds[i]);
                if (memberId.IsNone)
                    continue;
                if (!ArmyService.TryGetArmyForCharacter(world, memberId, out var bound) ||
                    bound == null ||
                    !string.Equals(bound.ArmyId, army.ArmyId, System.StringComparison.Ordinal))
                    continue;

                if (reason == Reason.GroupRelocation && !IsArmyControlledMember(world, memberId))
                    continue;
                if (reason == Reason.OrdinaryTick && preservePersonalPositions &&
                    world.WorldPresence.TryGet(memberId, out var personal) &&
                    personal != null)
                    continue;
                SyncMember(world, army, memberId, reason);
            }
        }

        public static void SyncMember(SimulationWorld world, FormalArmy army, EntityId memberId)
            => SyncMember(world, army, memberId, Reason.OrdinaryTick);

        static void SyncMember(SimulationWorld world, FormalArmy army, EntityId memberId, Reason reason)
        {
            if (world?.WorldPresence == null || army == null || memberId.IsNone)
                return;

            // Membership survives incapacity; movement authority does not.
            if (!LingeringBattlefieldPartyService.IsLivingForMacroOrder(world, memberId) ||
                IsArmyEngaged(world, army)) return;

            var motion = army.WorldMotion;
            if (!motion.HasPosition)
                return;

            // Near-field movement owns precise personal positions. The legacy group projection
            // must not overwrite them on idle ticks, finalization or during an encounter.
            if (reason == Reason.OrdinaryTick &&
                world.WorldPresence.TryGet(memberId, out var personal) &&
                !string.IsNullOrEmpty(personal.PersonalSurfaceId) &&
                (!motion.IsMoving || world.ContinuousOutdoorMaterialization.IsMaterialized(memberId)) &&
                IsNearArmyAnchor(world, personal, motion))
                return;

            if (motion.LocationKind == FormalArmyLocationKind.AtWorldSite &&
                !string.IsNullOrEmpty(motion.SiteId))
            {
                var siteSurface = motion.SurfaceId;
                if (string.IsNullOrEmpty(siteSurface) &&
                    world.SurfaceGround.TryResolveSiteArrival(motion.SiteId, out var resolvedSurface, out _))
                    siteSurface = resolvedSurface;
                world.WorldPresence.SetAtSiteWithAnchor(memberId, motion.SiteId,
                    motion.WorldPosition, siteSurface);
                return;
            }

            var worldSurface = motion.SurfaceId;
            if (string.IsNullOrEmpty(worldSurface) &&
                world.SurfaceGround.TryResolveContaining(motion.WorldPosition, out var navigation))
                worldSurface = navigation.SurfaceId;
            world.WorldPresence.SetAtWorldPosition(memberId, motion.WorldPosition,
                motion.CurrentHex, worldSurface);
        }

        static bool IsNearArmyAnchor(SimulationWorld world, WorldAgentPresence personal,
            FormalArmyWorldMotion motion)
        {
            if (string.IsNullOrEmpty(motion.SurfaceId)) return true;
            if (motion.LocationKind == FormalArmyLocationKind.AtWorldSite)
            {
                if (personal.Mode != PartyWorldPresenceMode.AtSite ||
                    !string.Equals(personal.SiteId, motion.SiteId,
                        System.StringComparison.Ordinal))
                    return false;
            }
            else if (personal.Mode != PartyWorldPresenceMode.AtWorldPosition)
                return false;
            if (!personal.HasContinuousWorldPosition ||
                !string.Equals(personal.PersonalSurfaceId, motion.SurfaceId,
                    System.StringComparison.Ordinal) ||
                !world.SurfaceGround.TryGet(motion.SurfaceId, out var navigation))
                return false;
            var dx = personal.WorldPosX - motion.WorldPosition.X;
            var dy = personal.WorldPosY - motion.WorldPosition.Y;
            var limit = navigation.CellSize * 16f;
            return dx * dx + dy * dy <= limit * limit;
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
                // A personal residual position outranks the compatibility adapter. In particular,
                // AtSite + precise must remain AtSite; converting it to AtHex loses Site semantics
                // and used to make delayed death relocate the corpse.
                if (ResidualSpatialAuthorityService.TryResolveStableResidualSpatialAuthority(
                        world, memberId, out _))
                    return;

                // Genuine legacy repair has no trustworthy personal point. Use only this army's
                // own motion (never PlayerParty/focus position) and preserve Surface provenance.
                if (!motion.HasPosition)
                    return;
                if (motion.LocationKind == FormalArmyLocationKind.AtWorldSite &&
                    !string.IsNullOrEmpty(motion.SiteId))
                {
                    world.WorldPresence.SetAtSite(memberId, motion.SiteId);
                    return;
                }
                world.WorldPresence.SetAtWorldPosition(
                    memberId, motion.WorldPosition, motion.CurrentHex, motion.SurfaceId);
                return;
            }

            // Living member 普通退出 Army：保持当前旧行为。
            if (motion.LocationKind == FormalArmyLocationKind.AtWorldSite &&
                !string.IsNullOrEmpty(motion.SiteId))
            {
                var siteSurface = motion.SurfaceId;
                if (string.IsNullOrEmpty(siteSurface) &&
                    world.SurfaceGround.TryResolveSiteArrival(motion.SiteId,
                        out var resolvedSurface, out _))
                    siteSurface = resolvedSurface;
                if (!string.IsNullOrEmpty(siteSurface) && motion.HasPosition)
                    world.WorldPresence.SetAtSiteWithAnchor(memberId, motion.SiteId,
                        motion.WorldPosition, siteSurface);
                else
                    world.WorldPresence.SetAtSite(memberId, motion.SiteId);
                return;
            }

            if (motion.HasPosition)
                world.WorldPresence.SetAtWorldPosition(memberId, motion.WorldPosition,
                    motion.CurrentHex, motion.SurfaceId);
        }
    }
}
