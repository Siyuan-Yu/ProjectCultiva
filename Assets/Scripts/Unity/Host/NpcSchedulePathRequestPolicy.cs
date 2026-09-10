namespace XianXia.Unity.Host
{
    /// <summary>
    /// NPC 日程寻路的 repath 触发策略（纯函数，便于测试）。
    ///
    /// 铁律：**正常沿路径移动中的 NPC 不允许仅因为 timer 到期就重新 A***。
    /// timer 只用来限制「没在移动 / 卡住 / WalkGrid 变了」这三类受控 repath 的频率，
    /// 且必须是 real-time（unscaled）窗口，不能除以 game speed —— 否则 20x 会把冷却
    /// 压到 0.15s，形成 A* storm。
    /// </summary>
    public static class NpcSchedulePathRequestPolicy
    {
        public static bool ShouldRequestPath(
            bool targetChanged,
            bool stuck,
            bool isMoving,
            bool repathDue,
            bool gridRevisionChanged)
        {
            // 目标变了：立即请求（不等待 timer）。
            if (targetChanged)
                return true;
            // 没在移动（含首次寻路、被挡停）：受控请求。
            if (!isMoving)
                return repathDue;
            // 卡住：受控 repath。
            if (stuck)
                return repathDue;
            // WalkGrid revision 变了、旧路径可能已失效：受控 repath。
            if (gridRevisionChanged)
                return repathDue;
            // 正常移动中：保持当前路径（timer 到期也不重新 A*）。
            return false;
        }
    }
}
