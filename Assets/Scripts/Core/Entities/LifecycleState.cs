namespace XianXia.Core.Entities
{
    /// <summary>Character lifecycle (ADR-0019). Removed is independent of Dead.</summary>
    public enum LifecycleState
    {
        Alive = 0,
        Incapacitated = 1,
        Captured = 2,
        Missing = 3,
        Dead = 4,
        Removed = 5
    }
}
