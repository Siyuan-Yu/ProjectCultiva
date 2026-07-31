namespace XianXia.Core.Events
{
    public enum EventType
    {
        EntityCreated = 1,
        ModifierAdded = 2,
        ModifierRemoved = 3,
        ActionCompleted = 4,
        ActionFailed = 5,
        OrderRejected = 6,
        /// <summary>Emitted once after VS0.1 / new-game world bootstrap completes.</summary>
        WorldInitialized = 7
    }
}
