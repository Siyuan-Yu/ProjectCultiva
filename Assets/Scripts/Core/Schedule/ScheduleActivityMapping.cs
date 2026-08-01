using XianXia.Core.Orders;

namespace XianXia.Core.Schedule
{
    public static class ScheduleActivityMapping
    {
        public static OrderType ToOrderType(ScheduleActivity activity)
        {
            switch (activity)
            {
                case ScheduleActivity.Labor:
                    return OrderType.Labor;
                case ScheduleActivity.Rest:
                case ScheduleActivity.Eat:
                    return OrderType.Rest;
                case ScheduleActivity.Cultivate:
                    return OrderType.Cultivate;
                case ScheduleActivity.Explore:
                case ScheduleActivity.Patrol:
                case ScheduleActivity.Inspect:
                    return OrderType.Observe;
                default:
                    return OrderType.Rest;
            }
        }

        public static bool TryParse(string text, out ScheduleActivity activity)
        {
            activity = ScheduleActivity.Rest;
            if (string.IsNullOrWhiteSpace(text))
                return false;
            return System.Enum.TryParse(text, true, out activity);
        }
    }
}
