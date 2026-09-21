using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Modern residual = lifecycle state plus precise Surface spatial authority. Hex helpers in
    /// this type exist only for one-way old snapshot and non-Continuous compatibility.
    /// </summary>
    public static class ResidualCharacterPresenceService
    {
        /// <summary>
        /// Legacy-only Hex placement for old snapshot migration and non-Continuous LocalMap
        /// compatibility. Modern Continuous gameplay must preserve an exact Surface position.
        /// </summary>
        public static void PlaceLegacyCharacterAtResidualHex(
            SimulationWorld world,
            EntityId characterId,
            HexCoord encounterHex)
        {
            if (world == null || characterId.IsNone)
                return;
            if (!world.Entities.TryGet(characterId, out var ent) || ent == null)
                return;
            if (!IsResidualLifeCandidate(world, characterId))
                return;


            world.WorldPresence.SetLegacyAtHex(characterId, encounterHex);

        }

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


