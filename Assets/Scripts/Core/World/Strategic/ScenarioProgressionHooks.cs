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
        /// <summary>真实 WorldSite 易主事务成功后触发；不表示 Objective 永久完成。</summary>
        public static Action<SimulationWorld, string, string, string, string> OnWorldSiteCaptured;

        public static void NotifyAllCaptureObjectivesCompletedForSite(SimulationWorld world, string siteId)
        {
            if (world == null || string.IsNullOrEmpty(siteId))
                return;
            OnAllCaptureObjectivesCompletedForSite?.Invoke(world, siteId);
        }

        public static void NotifyWorldSiteCaptured(
            SimulationWorld world, string siteId, string oldOwnerFactionId, string newOwnerFactionId, string workAreaId)
        {
            if (world == null || string.IsNullOrEmpty(siteId))
                return;
            OnWorldSiteCaptured?.Invoke(world, siteId, oldOwnerFactionId ?? string.Empty,
                newOwnerFactionId ?? string.Empty, workAreaId ?? string.Empty);
        }
    }
}
