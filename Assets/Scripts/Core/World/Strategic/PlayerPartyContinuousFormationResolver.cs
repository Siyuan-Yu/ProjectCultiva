using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Surface;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Stable player-party presentation slots; PlayerPartyWorldMotion remains authority.</summary>
    public static class PlayerPartyContinuousFormationResolver
    {
        public static bool TryResolve(SimulationWorld world, PlayerPartyRuntime party, EntityId id,
            SurfaceGroundNavigation navigation, out WorldVec2 point)
        {
            point = default;
            var motion = world?.PlayerPartyTravel;
            if (party == null || navigation == null || motion == null || !motion.HasPosition ||
                !PlayerPartyTransitionMembership.ShouldMemberTransitionWithParty(world, party, id))
                return false;

            var anchor = motion.WorldPosition;
            if (party.IsActive(id))
            {
                point = anchor;
                return true;
            }

            var slot = 1;
            var members = party.Members;
            for (var i = 0; i < members.Count; i++)
            {
                var member = members[i];
                if (party.IsActive(member) ||
                    !PlayerPartyTransitionMembership.ShouldMemberTransitionWithParty(world, party, member))
                    continue;
                if (member == id)
                {
                    point = SquadContinuousFormationResolver.ResolveConnectedSlot(
                        anchor, slot, navigation);
                    return true;
                }
                slot++;
            }
            return false;
        }
    }
}
