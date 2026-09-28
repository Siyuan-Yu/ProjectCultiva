namespace XianXia.Core.Entities
{
    public sealed class LifecycleComponent : IComponent
    {
        public LifecycleComponent(LifecycleState state = LifecycleState.Alive)
        {
            State = state;
        }

        public LifecycleState State { get; set; }

        /// <summary>
        /// 弥留到期世界 tick；到点未治疗则转阵亡。0＝未进入弥留计时。
        /// </summary>
        public ulong BleedOutAfterTick { get; set; }

        public ulong IncapacitatedAtTick { get; set; }
        public LifecycleState LastTransitionFrom { get; set; } = LifecycleState.Alive;
        public LifecycleState LastTransitionTo { get; set; } = LifecycleState.Alive;
        public string LastLifeTransitionReason { get; set; } = string.Empty;
        public DeathConfirmationReason LastDeathConfirmationReason { get; set; }

        public bool IsDead => State == LifecycleState.Dead;

        public bool IsRemoved => State == LifecycleState.Removed;

        public bool IsIncapacitated => State == LifecycleState.Incapacitated;

        public void ClearBleedOut() => BleedOutAfterTick = 0;

        public void RecordTransition(LifecycleState from, LifecycleState to, string reason)
        {
            LastTransitionFrom = from;
            LastTransitionTo = to;
            LastLifeTransitionReason = reason ?? string.Empty;
        }
    }
}
