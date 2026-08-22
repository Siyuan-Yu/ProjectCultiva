using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Formal Army 追击 tick 同步。
    /// · 已上路／多跳队列中：禁止每 tick MoveArmyToStackAnchor（会 Prepare 清零 ticks）
    /// · 同路追击：仅 Clamp 敌人当前进度（从我方当前进度续跑）
    /// · 停下且无队列：再开拔一次
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
            {
                ArmyTravelCommandService.ClampArmyPursuitToStackAnchor(world, army, stack);
                ArmyPresenceAdapter.SyncFromArmy(world, army);
                return;
            }

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
