using UnityEngine;

namespace XianXia.Unity.Actions
{
    /// <summary>
    /// 统一行动实例。木材／草药／修炼逻辑由控制器按 ActionType 调度，不写死在移动脚本里。
    /// </summary>
    public sealed class CharacterAction
    {
        public ActionType ActionType;
        public Transform Target;
        public Vector2 TargetPoint;
        public string TargetName;
        public float InteractionRange = 0.85f;
        /// <summary>一个产出周期对应的游戏小时（持续行动用）。</summary>
        public float CycleGameHours = 0.25f;
        public float Progress;
        public ActionStatus Status = ActionStatus.Idle;
        public bool CanInterrupt = true;
        public string CancelReason = string.Empty;
        public string StatusLabel = "空闲";

        public bool IsActive =>
            Status == ActionStatus.MovingToAction
            || Status == ActionStatus.Working
            || Status == ActionStatus.Cultivating;

        public void ResetToIdle()
        {
            ActionType = ActionType.None;
            Target = null;
            TargetPoint = default;
            TargetName = string.Empty;
            InteractionRange = 0.85f;
            CycleGameHours = 0.25f;
            Progress = 0f;
            Status = ActionStatus.Idle;
            CanInterrupt = true;
            CancelReason = string.Empty;
            StatusLabel = "空闲";
        }
    }
}
