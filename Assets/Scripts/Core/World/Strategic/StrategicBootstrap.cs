using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Generic 战略 Bootstrap 入口（Final Closure）。
    /// 不决定 Ch01 剧情外交；Opening Scenario 由 <see cref="Ch01ScenarioStrategicSetup"/> 负责。
    /// </summary>
    public static class StrategicBootstrap
    {
        /// <summary>Ch01 Opening 场景初始化（委托 Scenario Setup）。</summary>
        public static void ApplyCh01Defaults(SimulationWorld world) =>
            Ch01ScenarioStrategicSetup.Apply(world);
    }
}
