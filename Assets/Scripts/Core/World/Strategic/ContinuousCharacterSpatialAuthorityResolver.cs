using System;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    public enum ContinuousSpatialOwnerKind { Personal = 0, PlayerParty = 1, FormalArmy = 2, Squad = 3 }

    /// <summary>Read-only normal Surface position, with group motion ahead of personal presence.</summary>
    public static class ContinuousCharacterSpatialAuthorityResolver
    {
        public static bool TryResolveWorldPosition(SimulationWorld world, EntityId id,
            string surfaceId, out WorldVec2 position, out ContinuousSpatialOwnerKind owner,
            out string ownerId, out string failure)
        {
            position = default;
            owner = ContinuousSpatialOwnerKind.Personal;
            ownerId = string.Empty;
            failure = string.Empty;
            if (world == null || id.IsNone || string.IsNullOrEmpty(surfaceId))
            { failure = "MissingContinuousSpatialContext"; return false; }

            var party = world.Strategic.PlayerPartyContext;
            if (party != null && party.IsMember(id) &&
                PlayerPartyTransitionMembership.ShouldMemberTransitionWithParty(world, party, id))
            {
                owner = ContinuousSpatialOwnerKind.PlayerParty;
                var motion = world.PlayerPartyTravel;
                if (motion == null || !motion.HasPosition || motion.SurfaceId != surfaceId ||
                    !Finite(motion.WorldPosition.X) || !Finite(motion.WorldPosition.Y) ||
                    !world.SurfaceGround.TryGet(surfaceId, out var nav) ||
                    !nav.Contains(motion.WorldPosition.X, motion.WorldPosition.Y))
                { failure = "PlayerPartyContinuousSpatialInvariant"; return false; }
                if (!PlayerPartyContinuousFormationResolver.TryResolve(world, party, id, nav,
                        out position))
                { failure = "PlayerPartyContinuousSpatialInvariant:MemberNotTraveling"; return false; }
                return true;
            }

            if (SquadWorldMotionService.OwnsCharacter(world, id) &&
                world.Strategic.Squads.TryGetForCharacter(id, out var squad) &&
                world.Strategic.SquadWorldMotions.TryGet(squad.SquadId, out var squadMotion) &&
                SquadWorldMotionService.IsActiveNpcSquadAuthority(world, squad, squadMotion))
            {
                owner = ContinuousSpatialOwnerKind.Squad;
                ownerId = squad.SquadId;
                if (!squadMotion.HasPosition || squadMotion.SurfaceId != surfaceId ||
                    !Finite(squadMotion.WorldPosition.X) || !Finite(squadMotion.WorldPosition.Y) ||
                    !world.SurfaceGround.TryGet(surfaceId, out var nav) ||
                    !nav.Contains(squadMotion.WorldPosition.X, squadMotion.WorldPosition.Y) ||
                    !nav.IsWalkable(squadMotion.WorldPosition.X, squadMotion.WorldPosition.Y))
                { failure = "SquadContinuousSpatialInvariant"; return false; }
                var slot = SquadContinuousFormationResolver.StableSlot(squad, id);
                if (slot < 0) { failure = "SquadContinuousSpatialInvariant:MemberMissing"; return false; }
                position = SquadContinuousFormationResolver.Resolve(squadMotion, slot, nav);
                return true;
            }

            return CharacterPersonalSpaceQuery.TryResolveContinuous(world, id, surfaceId,
                out position, out failure);
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
