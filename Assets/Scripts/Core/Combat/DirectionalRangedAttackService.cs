using XianXia.Core.Domain.Ids;
using XianXia.Core.Cultivation;
using XianXia.Core.Entities;
using XianXia.Core.Npc;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Combat
{
    /// <summary>Domain permission for free-aim shots. Presentation supplies actual swept contact, never damage.</summary>
    public static class DirectionalRangedAttackService
    {
        public static bool CanFire(SimulationWorld world, EntityId actor)
        {
            return world != null && world.Entities.TryGet(actor, out var entity) &&
                CombatLifeStateService.CanFight(entity) && SpiritVeilService.IsActive(entity) &&
                entity.TryGet<CultivationComponent>(out var cultivation) && SpiritVeilRules.CanUseRealm(cultivation.Realm) &&
                entity.TryGet<CombatVitalsComponent>(out var vitals) && vitals.CurrentSpiritPower > 0 &&
                !PartyRestService.BlocksMovement(entity) &&
                MortalTransportConstraintService.ValidateAttack(world, actor).IsSuccess;
        }

        public static bool CanHit(SimulationWorld world, EntityId actor, EntityId target)
        {
            if (!CanFire(world, actor) || actor == target ||
                !world.Entities.TryGet(target, out var victim) || !CombatLifeStateService.CanFight(victim) ||
                CharacterEncounterService.RequiresEntry(world, actor, target)) return false;
            var encounter = world.Strategic?.CharacterEncounter;
            if (encounter != null)
                return encounter.Phase == CharacterEncounterPhase.Active && encounter.Opposing(actor.Value, target.Value);
            return NpcHostilityService.CanAutoAttack(world, actor, target);
        }

        public static bool ResolveHit(SimulationWorld world, EntityId actor, EntityId target,
            out bool defeated)
        {
            defeated = false;
            return CanHit(world, actor, target) && new MeleeCombatService().ApplyStrike(
                world, actor, target, out _, out defeated).IsSuccess;
        }
    }
}
