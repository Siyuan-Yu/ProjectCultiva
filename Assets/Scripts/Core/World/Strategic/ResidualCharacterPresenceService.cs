using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Residual lifecycle eligibility for exact Surface spatial authority.</summary>
    public static class ResidualCharacterPresenceService
    {
        /// <summary>LifeState 候选：Incapacitated 或 Dead+VisibleCorpse；排除 Removed / Captured。</summary>
        public static bool IsResidualLifeCandidate(SimulationWorld world, EntityId characterId)
        {
            if (world == null || characterId.IsNone)
                return false;
            if (!world.Entities.TryGet(characterId, out var ent) || ent == null)
                return false;
            if (!ent.TryGet<LifecycleComponent>(out var life) || life == null || life.IsRemoved)
                return false;
            if (life.State == LifecycleState.Captured)
                return false;
            if (life.IsIncapacitated)
                return true;
            return Combat.CombatLifeStateService.HasVisibleCorpse(ent);
        }

    }
}


