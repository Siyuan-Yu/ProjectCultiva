using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    public enum EncounterSpatialOwnerKind { Personal = 0, PlayerParty = 1, FormalArmy = 2 }

    /// <summary>Read-only encounter origin resolution; organization authority is never rewritten.</summary>
    public static class CharacterEncounterSpatialAuthorityResolver
    {
        public static bool TryResolveEncounterWorldPosition(SimulationWorld world, EntityId id,
            string sourceSurfaceId, out WorldVec2 position, out EncounterSpatialOwnerKind owner,
            out string formalArmyId, out string failure)
        {
            var resolved = ContinuousCharacterSpatialAuthorityResolver.TryResolveWorldPosition(
                world, id, sourceSurfaceId, out position, out var spatialOwner,
                out formalArmyId, out failure);
            owner = (EncounterSpatialOwnerKind)spatialOwner;
            return resolved;
        }

        public static string DescribeFailure(SimulationWorld world, EntityId id, string squadId,
            string requestedSurface, EncounterSpatialOwnerKind owner, string failure)
        {
            WorldAgentPresence personal = null;
            if (world != null) world.WorldPresence.TryGet(id, out personal);
            FormalArmy army = null;
            if (world != null) ArmyService.TryGetArmyForCharacter(world, id, out army);
            var motion = army?.WorldMotion;
            var party = world?.Strategic?.PlayerPartyContext;
            var partyMotion = world?.PlayerPartyTravel;
            var partySurface = "";
            if (partyMotion?.HasPosition == true &&
                world.SurfaceGround.TryResolveContaining(partyMotion.WorldPosition,
                    out var partyNavigation))
                partySurface = partyNavigation.SurfaceId;
            return failure + " CharacterId=" + id.Value + " SquadId=" + (squadId ?? "") +
                   " SpatialOwnerKind=" + owner + " FormalArmyId=" + (army?.ArmyId ?? "") +
                   " HasPersonalPosition=" + (personal?.HasContinuousWorldPosition == true) +
                   " PersonalSurfaceId=" + (personal?.PersonalSurfaceId ?? "") +
                   " ArmySurfaceId=" + (motion?.SurfaceId ?? "") +
                   " ArmyWorldPosition=" + (motion?.HasPosition == true
                       ? motion.WorldPosition.ToString() : "none") +
                   " IsPlayerPartyMember=" + (party?.IsMember(id) == true) +
                   " IsActiveCharacter=" + (party?.IsActive(id) == true) +
                   " PartyHasPosition=" + (partyMotion?.HasPosition == true) +
                   " PartyLocationKind=" + (partyMotion?.LocationKind.ToString() ?? "none") +
                   " PartySiteId=" + (partyMotion?.SiteId ?? "") +
                   " PartyWorldPosition=" + (partyMotion?.HasPosition == true
                       ? partyMotion.WorldPosition.ToString() : "none") +
                   " PartySurfaceId=" + partySurface +
                   " RequestedSurface=" + (requestedSurface ?? "");
        }
    }
}
