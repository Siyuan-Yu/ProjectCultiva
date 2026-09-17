using System;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    public enum ContinuousSpatialOwnerKind { Personal = 0, PlayerParty = 1, FormalArmy = 2 }

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

            if (FormalArmyMemberPresenceSync.IsArmyControlledMember(world, id) &&
                ArmyService.TryGetArmyForCharacter(world, id, out var army) && army != null)
            {
                owner = ContinuousSpatialOwnerKind.FormalArmy;
                ownerId = army.ArmyId;
                var motion = army.WorldMotion;
                if (!motion.HasPosition || motion.SurfaceId != surfaceId ||
                    !Finite(motion.WorldPosition.X) || !Finite(motion.WorldPosition.Y) ||
                    !world.SurfaceGround.TryGet(surfaceId, out var nav) ||
                    !nav.Contains(motion.WorldPosition.X, motion.WorldPosition.Y) ||
                    !nav.IsWalkable(motion.WorldPosition.X, motion.WorldPosition.Y))
                { failure = "FormalArmyContinuousSpatialInvariant"; return false; }
                var slot = -1;
                for (var i = 0; i < army.MemberCharacterIds.Count; i++)
                    if (army.MemberCharacterIds[i] == id.Value) { slot = i; break; }
                if (slot < 0) { failure = "FormalArmyContinuousSpatialInvariant:MemberMissing"; return false; }
                position = FormalArmyContinuousFormationResolver.Resolve(motion, slot, nav);
                return true;
            }

            var party = world.Strategic.PlayerPartyContext;
            if (party != null && party.IsMember(id))
            {
                owner = ContinuousSpatialOwnerKind.PlayerParty;
                var motion = world.PlayerPartyTravel;
                if (motion == null || !motion.HasPosition ||
                    !Finite(motion.WorldPosition.X) || !Finite(motion.WorldPosition.Y) ||
                    !world.SurfaceGround.TryGet(surfaceId, out var nav) ||
                    !nav.Contains(motion.WorldPosition.X, motion.WorldPosition.Y))
                { failure = "PlayerPartyContinuousSpatialInvariant"; return false; }
                if (!PlayerPartyContinuousFormationResolver.TryResolve(world, party, id, nav,
                        out position))
                { failure = "PlayerPartyContinuousSpatialInvariant:MemberNotTraveling"; return false; }
                return true;
            }

            return CharacterPersonalSpaceQuery.TryResolveContinuous(world, id, surfaceId,
                out position, out failure);
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
