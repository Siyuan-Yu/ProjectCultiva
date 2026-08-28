using System;
using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>拒绝远程 FormalArmy 手动进战场（Phase 4 Authority Gate）。</summary>
    public static class BattleManualEntryPolicy
    {
        public static bool CanEnterManual(SimulationWorld world) =>
            BattleDecisionPolicy.CanPlayerManuallyParticipate(world);

        public static Result ValidateManualEntry(SimulationWorld world)
        {
            if (world?.Strategic == null)
                return Result.Failure(ErrorCode.InvalidOperation, "No strategic board.");

            if (CanEnterManual(world))
                return Result.Success();

            return Result.Failure(
                ErrorCode.InvalidOperation,
                "Manual battle requires PlayerParty within engagement range.",
                "RemoteFormalArmyManualBlocked");
        }
    }
}
