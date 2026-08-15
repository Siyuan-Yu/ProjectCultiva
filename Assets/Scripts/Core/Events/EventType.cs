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
        QuotaConsequenceApplied = 15,
        /// <summary>VS0.5: RelationshipLedger recorded a directed score change.</summary>
        RelationshipChanged = 16,
        /// <summary>VS0.5: Faction membership joined／left (thin Alpha mark).</summary>
        FactionMembershipChanged = 17,
        /// <summary>VS0.8: Settlement stock changed (session; not Snapshot).</summary>
        SettlementStockChanged = 18,
        /// <summary>VS0.8: Work role assignment changed.</summary>
        WorkAssignmentChanged = 19,
        /// <summary>VS0.8: Day-end settlement production resolved.</summary>
        SettlementProductionResolved = 20,
        /// <summary>VS0.9: Entity moved to another abstract location.</summary>
        LocationChanged = 21,
        /// <summary>VS0.9: Explore resolved at current location.</summary>
        LocationExplored = 22,
        /// <summary>Content Ready: quest offered／started (session).</summary>
        QuestStarted = 23,
        /// <summary>Content Ready: quest completed (session).</summary>
        QuestCompleted = 24,
        /// <summary>Content Ready: quest failed (session).</summary>
        QuestFailed = 25,
        /// <summary>Quest journal: rewards claimed after ReadyToClaim.</summary>
        QuestRewardsClaimed = 31,
        /// <summary>Quest journal: player abandoned an abandonable quest.</summary>
        QuestAbandoned = 32,
        /// <summary>Content Ready: content event presented to player (session).</summary>
        ContentEventPresented = 26,
        /// <summary>Content Ready: content event choice resolved (session).</summary>
        ContentEventResolved = 27,
        /// <summary>Chapter Production: story／content flag changed (session).</summary>
        StoryFlagChanged = 28,
        /// <summary>Chapter Production: chapter activated (session).</summary>
        ChapterActivated = 29,
        /// <summary>Chapter Production: chapter day beat applied (session).</summary>
        ChapterDayBeatApplied = 30,
        /// <summary>Territory: control core (主管府) took damage.</summary>
        ControlCoreDamaged = 33,
        /// <summary>Territory: player captured a control core.</summary>
        ControlCoreCaptured = 34,
        /// <summary>LocalMap 进出：ActiveMapLayoutId 已切换（session）。</summary>
        LocalMapChanged = 35,
        /// <summary>战斗 Alpha：单位被击倒（NPC Dead／己方重伤）。</summary>
        CombatantDefeated = 36
    }
}

