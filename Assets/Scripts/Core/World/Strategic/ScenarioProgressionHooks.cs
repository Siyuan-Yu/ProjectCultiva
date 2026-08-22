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
        /// <summary>Node 全部 CaptureObjective 完成且 Owner 已更新后触发（Scenario 可选订阅）。</summary>
        public static Action<SimulationWorld, string> OnAllCaptureObjectivesCompletedForNode;

        public static void NotifyAllCaptureObjectivesCompletedForNode(SimulationWorld world, string nodeId)
        {
            if (world == null || string.IsNullOrEmpty(nodeId))
                return;
            OnAllCaptureObjectivesCompletedForNode?.Invoke(world, nodeId);
        }
    }
}
