using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.Social;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// 现代 CharacterEncounter / Continuous Manual Combat 敌对判定。
    /// </summary>
    public static class CharacterEncounterHostilityService
    {
        public static bool IsInEncounterCombatContext(SimulationWorld world)
        {
            return world?.Strategic?.CharacterEncounter != null ||
                   world?.Strategic?.ContinuousManualCombat?.IsActive == true;
        }

        public static bool IsEncounterCombatParticipant(SimulationWorld world, EntityId npcId)
        {
            if (world == null || npcId.IsNone)
                return false;

            var continuous = world.Strategic?.ContinuousManualCombat;
            if (continuous != null && continuous.IsActive)
                return continuous.Contains(npcId);

            var encounter = world.Strategic?.CharacterEncounter;
            if (encounter?.Find(npcId.Value) != null)
                return true;
            var snap = world.Strategic?.Participants;
            return snap != null && snap.FindByEntity(npcId) != null;
        }

        public static bool IsHostileEncounterParticipant(SimulationWorld world, Entity entity)
        {
            if (world == null || entity == null)
                return false;

            var continuous = world.Strategic?.ContinuousManualCombat;
            if (continuous != null && continuous.IsActive)
                return continuous.IsEnemy(entity.Id);

            if (!IsInEncounterCombatContext(world))
                return false;

            if (!IsEncounterCombatParticipant(world, entity.Id))
                return false;

            if (!entity.TryGet<FactionMembershipComponent>(out var mem) || mem == null)
                return false;

            var playerFaction = world.Strategic?.PlayerFactionId ?? StrategicFactionCatalog.PlayerFactionId;
            return WarGateService.CanAttack(world, playerFaction, mem.FactionId);
        }
    }
}
