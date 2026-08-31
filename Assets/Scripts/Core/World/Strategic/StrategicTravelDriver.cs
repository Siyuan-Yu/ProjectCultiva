using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// SimulationLoop 之后的战略层 Travel 真推进入口。
    /// PlayerParty：AfterTravelTick → PlayerPartyHexTravelService.AdvanceAll（及距离预算）。
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
