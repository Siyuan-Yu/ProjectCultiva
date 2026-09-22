using System.Collections.Generic;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>PlayerParty membership and exact Surface presence synchronization.</summary>
    public static class PlayerPartyTransitionMembership
    {
        static readonly List<EntityId> Scratch = new List<EntityId>(8);

        public static bool ShouldMemberTransitionWithParty(
            SimulationWorld world,
            PlayerPartyRuntime party,
            EntityId characterId)
        {
            if (world == null || party == null || characterId.IsNone ||
                !party.IsMember(characterId))
                return false;
            if (CharacterStrategicQuery.TryGetSquad(world, characterId, out var squad) &&
                !string.Equals(
                    squad.SquadId, party.ControlledSquadId,
                    System.StringComparison.Ordinal) &&
                !string.Equals(
                    squad.SquadId, SquadMembershipService.PlayerSquadId,
                    System.StringComparison.Ordinal))
                return false;
            return world.Entities.TryGet(characterId, out var entity) &&
                   entity != null &&
                   CombatLifeStateService.CanFight(entity);
        }

        public static void CaptureTravelingMembersForPartyTransition(
            SimulationWorld world,
            PlayerPartyRuntime party)
        {
            if (world?.PlayerPartyTravel == null || party == null)
                return;
            Scratch.Clear();
            for (var i = 0; i < party.Members.Count; i++)
                if (ShouldMemberTransitionWithParty(world, party, party.Members[i]))
                    Scratch.Add(party.Members[i]);
            world.PlayerPartyTravel.CaptureTravelingMembers(Scratch);
        }

        public static void SyncMemberPresenceFromMotion(
            SimulationWorld world,
            EntityId id)
        {
            var motion = world?.PlayerPartyTravel;
            if (motion == null || !motion.HasPosition || id.IsNone ||
                motion.LocationKind != PlayerPartyLocationKind.AtWorldPosition ||
                string.IsNullOrEmpty(motion.SurfaceId))
                return;
            world.WorldPresence.SetAtWorldPosition(
                id, motion.WorldPosition, motion.SurfaceId);
        }

        public static void SyncIndependentCharacterPresenceFromPosition(
            SimulationWorld world,
            EntityId id,
            WorldVec2 preciseWorldPosition,
            string surfaceId = "")
        {
            if (world == null || id.IsNone)
                return;
            if (string.IsNullOrEmpty(surfaceId) &&
                world.SurfaceGround.TryResolveContaining(preciseWorldPosition, out var surface))
                surfaceId = surface.SurfaceId;
            if (string.IsNullOrEmpty(surfaceId))
                return;
            if (WorldSiteAdministrativeControlResolver.TryResolve(
                    world, surfaceId, preciseWorldPosition.X, preciseWorldPosition.Y,
                    out var site, out _) &&
                site != null)
                world.WorldPresence.SetAtSiteWithAnchor(
                    id, site.SiteId, preciseWorldPosition, surfaceId);
            else
                world.WorldPresence.SetAtWorldPosition(
                    id, preciseWorldPosition, surfaceId);
        }

        public static void ReconcilePlayerPartyMemberWorldPresenceFromMotion(
            SimulationWorld world,
            PlayerPartyRuntime party)
        {
            var motion = world?.PlayerPartyTravel;
            if (motion == null || party == null || !motion.HasPosition ||
                motion.LocationKind != PlayerPartyLocationKind.AtWorldPosition ||
                string.IsNullOrEmpty(motion.SurfaceId))
                return;
            for (var i = 0; i < party.Members.Count; i++)
            {
                var id = party.Members[i];
                if (ShouldMemberTransitionWithParty(world, party, id))
                    world.WorldPresence.SetAtWorldPosition(
                        id, motion.WorldPosition, motion.SurfaceId);
            }
        }
    }
}
