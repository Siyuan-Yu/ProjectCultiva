namespace XianXia.Core.Entities
{
    public sealed class LifecycleComponent : IComponent
    {
        public LifecycleComponent(LifecycleState state = LifecycleState.Alive)
        {
            State = state;
        }

        public LifecycleState State { get; set; }

        public bool IsDead => State == LifecycleState.Dead;

        public bool IsRemoved => State == LifecycleState.Removed;

        public bool IsIncapacitated => State == LifecycleState.Incapacitated;
    }
}
