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

        public bool IsDead => State == LifecycleState.Dead;

        public bool IsRemoved => State == LifecycleState.Removed;

        public bool IsIncapacitated => State == LifecycleState.Incapacitated;

        public void ClearBleedOut() => BleedOutAfterTick = 0;
    }
}
