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
        AssignWork = 8
    }
}
