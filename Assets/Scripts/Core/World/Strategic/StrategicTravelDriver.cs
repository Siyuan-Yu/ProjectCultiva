using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// SimulationLoop 之后的战略层 Travel 真推进入口。
    /// PlayerParty：AfterTravelTick → PlayerPartyHexTravelService.AdvanceAll（及距离预算）。
    /// PlayerParty pursuit（Phase 5S-B2-3.5）：因 Core 无 PlayerPartyRuntime 引用，pursuit tick
    /// 由 Host PlayableHostBootstrap.StepTick 在 TickOnce 之后驱动
    /// （PlayerPartyHexPursuitService.AfterTravelTick(world, party)）—— 其顺序等价于本文件内
    /// ArmyHexPursuitService.AfterTravelTick：PlayerParty travel 与 FormalArmy target travel 均已
    /// Advance 后，先检查 SupportArea contact、未接触则按 target 当前 Hex retarget。
    /// 接战弹窗优先；抵达后不再自动进入残留战场 —— Residual 只是 world population，
    /// 普通 MoveToHex 到站后自然停在 Hex（Player 加载 LocalMap 时可见弥留／尸体）。
    /// </summary>
    public static class StrategicTravelDriver
    {
        public static void AfterTravelTick(SimulationWorld world, int ticks = 1)
        {
            if (world?.Strategic == null || ticks < 1)
                return;

            if (!world.HexWorld.HasGrid)
                return;

            ArmyHexTravelService.AdvanceAll(world, ticks);
            PlayerPartyHexTravelService.AdvanceAll(world, ticks);
            BackgroundSimulationScheduler.AfterSimulationTick(world, ticks);
            ArmyStackAdapter.SyncAllLinkedStacksFromFormalArmies(world);
            ArmyHexPursuitService.AfterTravelTick(world);
        }
    }
}
