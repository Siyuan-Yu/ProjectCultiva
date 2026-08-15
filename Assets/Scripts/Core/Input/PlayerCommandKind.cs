namespace XianXia.Core.Input
{
    /// <summary>
    /// RTS player command intent. Social kinds are instantaneous Core calls (not Orders).
    /// </summary>
    public enum PlayerCommandKind
    {
        Labor = 1,
        Rest = 2,
        Observe = 3,
        Cultivate = 4,
        /// <summary>VS0.6: actor Help target → RelationshipService.</summary>
        Help = 5,
        /// <summary>VS0.6: actor Slight target → RelationshipService.</summary>
        Slight = 6,
        /// <summary>VS0.6: actor Recruit target → RecruitService.</summary>
        Recruit = 7,
        /// <summary>VS0.8: assign Subject work role on primary settlement.</summary>
        AssignWork = 8,
        /// <summary>VS0.9: explore current abstract location.</summary>
        Explore = 9,
        /// <summary>VS0.9: travel to TargetLocationId (must be adjacent).</summary>
        Travel = 10,
        /// <summary>Content Ready: resolve active content event choice (ChoiceId).</summary>
        ResolveContentChoice = 11,
        /// <summary>Content Ready: start quest by QuestId if offer conditions pass.</summary>
        StartQuest = 12,
        /// <summary>Demo parity [49]/[32]: cancel active action and clear pending player orders.</summary>
        Stop = 13,
        /// <summary>Demo parity [49]: consume conceal grass to lower PersonalConcealmentRisk.</summary>
        UseConcealGrass = 14,
        /// <summary>Quest journal: claim rewards for ReadyToClaim quest.</summary>
        ClaimQuestRewards = 15,
        /// <summary>Quest journal: abandon an abandonable quest.</summary>
        AbandonQuest = 16,
        /// <summary>Enter LocalMap via entrance location (TargetLocationId=洞口地点，可空＝当前位置).</summary>
        EnterLocalMap = 17,
        /// <summary>Leave current LocalMap interior back to overworld.</summary>
        LeaveLocalMap = 18,
        /// <summary>勘查洞口（TargetLocationId＝入口地点）。</summary>
        SurveyEntrance = 19
    }
}
