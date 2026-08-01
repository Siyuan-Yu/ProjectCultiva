namespace XianXia.Core.Schedule
{
    /// <summary>Planned activities for schedule／reference AI archetypes (not freeform BT AI).</summary>
    public enum ScheduleActivity
    {
        Labor = 1,
        Rest = 2,
        /// <summary>Mortal meal block → Rest order.</summary>
        Eat = 3,
        /// <summary>Cultivator practice → Cultivate order.</summary>
        Cultivate = 4,
        /// <summary>Cultivator／supervisor field check → Observe order.</summary>
        Explore = 5,
        /// <summary>Supervisor patrol → Observe order.</summary>
        Patrol = 6,
        /// <summary>Supervisor task inspection → Observe order.</summary>
        Inspect = 7
    }
}
