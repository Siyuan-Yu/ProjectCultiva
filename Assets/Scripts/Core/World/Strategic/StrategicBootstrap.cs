using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Generic 战略 Bootstrap 入口（Final Closure）。
    /// 不决定 Ch01 剧情外交；Opening Scenario 由 <see cref="Ch01ScenarioStrategicSetup"/> 负责。
    /// </summary>
    public static class StrategicBootstrap
    {
        /// <summary>Ch01 Opening 场景初始化（测试/fixture，无 Content 包）。</summary>
        public static Result ApplyCh01Defaults(SimulationWorld world)
        {
            Ch01ScenarioStrategicSetup.Apply(world);
            var hex = HexStrategicMapBootstrap.TryApplyFromFixture(world);
            if (hex.IsFailure)
                return hex;
            Ch01ScenarioStrategicSetup.PositionPrototypeBanditPatrolArmy(world);
            return Result.Success();
        }
    }
}
