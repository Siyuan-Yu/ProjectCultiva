using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Shared lifecycle queries for strategic movement and presentation.</summary>
    public static class CharacterLifeStateQuery
    {
        public static bool IsIncapacitated(SimulationWorld world, EntityId id)
        {
            if (world == null || id.IsNone || !world.Entities.TryGet(id, out var entity) || entity == null)
                return false;
            return entity.TryGet<LifecycleComponent>(out var life) && life.IsIncapacitated;
        }

        public static bool IsVisibleCorpse(SimulationWorld world, EntityId id)
        {
            if (world == null || id.IsNone || !world.Entities.TryGet(id, out var entity) || entity == null)
                return false;
            return CombatLifeStateService.HasVisibleCorpse(entity);
        }

        public static bool IsLivingForMacroOrder(SimulationWorld world, EntityId id)
        {
            if (world == null || id.IsNone || !world.Entities.TryGet(id, out var entity) || entity == null)
                return false;
            if (!entity.TryGet<LifecycleComponent>(out var life) || life == null)
                return true;
            return !life.IsIncapacitated && !life.IsDead && !life.IsRemoved;
        }
    }
}
