using System;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Scenario 层进度 Hook（Final Closure）。
    /// Generic Domain 不硬编码剧情；Scenario 注册回调决定 WHEN 使用 War/Vassalage 等。
    /// </summary>
    public static class ScenarioProgressionHooks
    {
        /// <summary>WorldSite 全部 CaptureObjective 完成且 Owner 已更新后触发（Scenario 可选订阅）。</summary>
        public static Action<SimulationWorld, string> OnAllCaptureObjectivesCompletedForSite;

        public static void NotifyAllCaptureObjectivesCompletedForSite(SimulationWorld world, string siteId)
        {
            if (world == null || string.IsNullOrEmpty(siteId))
                return;
            OnAllCaptureObjectivesCompletedForSite?.Invoke(world, siteId);
        }
    }
}
