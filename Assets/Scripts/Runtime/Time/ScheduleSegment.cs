using System;
using UnityEngine;

namespace XianXia.Unity.Time
{
    [Serializable]
    public sealed class ScheduleSegment
    {
        [SerializeField] private string displayName = "时段";
        [SerializeField] private ScheduleActivity activity = ScheduleActivity.Free;
        [SerializeField] private int startHour;
        [SerializeField] private int startMinute;
        [SerializeField] private int endHour;
        [SerializeField] private int endMinute;

        public string DisplayName => displayName;
        public ScheduleActivity Activity => activity;
        public int StartHour => startHour;
        public int StartMinute => startMinute;
        public int EndHour => endHour;
        public int EndMinute => endMinute;

        public int StartMinutes => startHour * 60 + startMinute;
        public int EndMinutes => endHour * 60 + endMinute;

        public ScheduleSegment()
        {
        }

        public ScheduleSegment(
            string name,
            ScheduleActivity activityType,
            int startH,
            int startM,
            int endH,
            int endM)
        {
            displayName = name;
            activity = activityType;
            startHour = startH;
            startMinute = startM;
            endHour = endH;
            endMinute = endM;
        }

        public bool Contains(float gameMinutesOfDay)
        {
            int start = StartMinutes;
            int end = EndMinutes;
            int current = Mathf.FloorToInt(gameMinutesOfDay) % GameClock.MinutesPerDay;

            if (start == end)
            {
                return true;
            }

            if (start < end)
            {
                return current >= start && current < end;
            }

            // 跨午夜，例如 23:00–06:00
            return current >= start || current < end;
        }

        public float MinutesUntilEnd(float gameMinutesOfDay)
        {
            int end = EndMinutes;
            int current = Mathf.FloorToInt(gameMinutesOfDay) % GameClock.MinutesPerDay;
            if (current < end)
            {
                return end - current;
            }

            return GameClock.MinutesPerDay - current + end;
        }

        public string FormatRange()
        {
            return $"{startHour:00}:{startMinute:00}–{endHour:00}:{endMinute:00}";
        }
    }
}
