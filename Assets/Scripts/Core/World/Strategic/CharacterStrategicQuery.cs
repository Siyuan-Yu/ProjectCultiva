using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.Social;

namespace XianXia.Core.World.Strategic
{
    public static class CharacterStrategicQuery
    {
        public static bool TryGetSquad(SimulationWorld world, EntityId id, out SquadState squad)
        {
            squad = null;
            return world?.Strategic?.Squads != null && !id.IsNone &&
                   world.Strategic.Squads.TryGetForCharacter(id, out squad);
        }

        public static string ResolveFactionId(SimulationWorld world, EntityId id)
        {
            if (world != null && world.Entities.TryGet(id, out var entity) && entity != null &&
                entity.TryGet<FactionMembershipComponent>(out var membership) && membership != null &&
                !string.IsNullOrEmpty(membership.FactionId)) return membership.FactionId;
            return TryGetSquad(world, id, out var squad) ? squad.FactionId ?? string.Empty : string.Empty;
        }
    }
}
