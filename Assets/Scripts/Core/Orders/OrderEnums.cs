namespace XianXia.Core.Orders
{
    public enum OrderSource
    {
        Player = 0,
        Ai = 1,
        Schedule = 2,
        Event = 3
    }

    public enum OrderType
    {
        Wait = 0,
        ApplyModifier = 1,
        Cultivate = 2,
        Labor = 3,
        Rest = 4,
        Observe = 5,
        /// <summary>NPC Simulation: travel to WorkArea／Location.</summary>
        Move = 6,
        /// <summary>NPC Simulation: on-site schedule work after Move.</summary>
        Work = 7
    }
}
