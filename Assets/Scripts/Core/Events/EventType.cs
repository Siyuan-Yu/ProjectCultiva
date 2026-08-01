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
        QuotaDeviationCreated = 10,
        /// <summary>VS0.3: WorldTick crossed a day boundary; previous day ended.</summary>
        DayEnded = 11,
        /// <summary>VS0.3: New day began immediately after DayEnded.</summary>
        DayStarted = 12,
        /// <summary>VS0.3: ObserveAction finished (hit／miss／none).</summary>
        ObservationResolved = 13,
        /// <summary>VS0.3: Entity discovered an OpportunitySite.</summary>
        OpportunitySiteDiscovered = 14,
        /// <summary>VS0.3: DayEnded consumed quota shortfall／Deviation into thin consequence.</summary>
        QuotaConsequenceApplied = 15
    }
}
