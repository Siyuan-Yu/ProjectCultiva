using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Formal Army 追击 tick 同步：TargetArmyId → FormalArmy.StrategicPosition 真源；
    /// TRACK EVERY TICK，仅拓扑变化时 Repath。
    /// </summary>
    public static class ArmyPursuitCommandService
    {
        public static void SyncFormalArmyPursuersToStack(
            SimulationWorld world,
            FormalArmy army,
            ArmyStack stack,
            IReadOnlyList<EntityId> pursue)
        {
            if (world == null || army == null || stack == null)
                return;

            if (!ArmyStackAdapter.TryGetFormalArmy(world, stack, out var targetArmy) || targetArmy == null)
            {
                SyncFormalArmyPursuersToStackLegacy(world, army, stack, pursue);
                return;
            }

            SyncFormalArmyPursuersToTargetArmy(world, army, targetArmy, stack, pursue);
        }

        public static void SyncFormalArmyPursuersToTargetArmy(
            SimulationWorld world,
            FormalArmy army,
            FormalArmy targetArmy,
            ArmyStack targetStack,
            IReadOnlyList<EntityId> pursue)
        {
            if (world == null || army == null || targetArmy == null)
                return;

            var leaderId = army.LeaderCharacterId;
            if (leaderId.IsNone ||
                !world.WorldPresence.TryGet(leaderId, out var leaderPresence) ||
                leaderPresence == null)
                return;

            if (leaderPresence.Mode == PartyWorldPresenceMode.InEncounter &&
                !StrategicEncounterSpawner.IsFieldCleared(world))
                return;

            if (!WorldTravelService.CanReceiveTravelOrder(world, leaderId))
                return;

            ArmyStackAdapter.SyncStackTravelFromFormalArmy(world, targetStack);

            if (targetStack != null &&
                StrategicEngageRules.IsAgentColocatedWithStack(world, leaderPresence, targetStack))
                return;

            ArmyPursuitTargetService.TryEnsurePursuitTravel(world, army, targetArmy);
            ArmyPresenceAdapter.SyncFromArmy(world, army);
        }

        static void SyncFormalArmyPursuersToStackLegacy(
            SimulationWorld world,
            FormalArmy army,
            ArmyStack stack,
            IReadOnlyList<EntityId> pursue)
        {
            var leaderId = army.LeaderCharacterId;
            if (leaderId.IsNone ||
                !world.WorldPresence.TryGet(leaderId, out var leaderPresence) ||
                leaderPresence == null)
                return;

            if (leaderPresence.Mode == PartyWorldPresenceMode.InEncounter &&
                !StrategicEncounterSpawner.IsFieldCleared(world))
                return;

            if (!WorldTravelService.CanReceiveTravelOrder(world, leaderId))
                return;

            if (StrategicEngageRules.IsAgentColocatedWithStack(world, leaderPresence, stack))
                return;

            if (army.IsTraveling)
                return;

            if (ArmyTravelCommandService.HasPendingLegs(army.ArmyId))
            {
                ArmyTravelCommandService.TryContinueQueuedTravel(world, army.ArmyId);
                ArmyPresenceAdapter.SyncFromArmy(world, army);
                return;
            }

            ArmyTravelCommandService.MoveArmyToStackAnchor(world, army.ArmyId, stack);
        }
    }
}
