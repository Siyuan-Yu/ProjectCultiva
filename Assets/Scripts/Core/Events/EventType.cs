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
        WorldInitialized = 7,
        /// <summary>Cultivation Slice 0.1: Mortal → QiRefining breakthrough.</summary>
        Breakthrough = 8,
        /// <summary>VS0.2: Schedule Action cancelled because a Player Order took over.</summary>
        ScheduleInterrupted = 9,
        /// <summary>VS0.2: DailyTask quota deviation recorded (rules layer only).</summary>
        QuotaDeviationCreated = 10
    }
}
