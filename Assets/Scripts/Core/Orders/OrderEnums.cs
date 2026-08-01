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
        Observe = 5
    }
}
