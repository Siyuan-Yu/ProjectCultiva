using System.Collections.Generic;
using UnityEngine;

namespace XianXia.Unity.Time
{
    [CreateAssetMenu(menuName = "XianXia/Schedule/Day Schedule Config", fileName = "DaySchedule_Laborer")]
    public sealed class DayScheduleConfig : ScriptableObject
    {
        [SerializeField] private string scheduleId = "laborer_default";
        [SerializeField] private bool canModify;
        [SerializeField] private List<ScheduleSegment> segments = new();

        public string ScheduleId => scheduleId;
        public bool CanModify => canModify;
        public IReadOnlyList<ScheduleSegment> Segments => segments;

        public ScheduleSegment GetCurrent(float gameMinutesOfDay)
        {
            for (int i = 0; i < segments.Count; i++)
            {
                if (segments[i] != null && segments[i].Contains(gameMinutesOfDay))
                {
                    return segments[i];
                }
            }

            return null;
        }

        public ScheduleSegment GetNext(float gameMinutesOfDay)
        {
            ScheduleSegment current = GetCurrent(gameMinutesOfDay);
            if (current == null || segments.Count == 0)
            {
                return null;
            }

            int index = segments.IndexOf(current);
            if (index < 0)
            {
                return segments[0];
            }

            return segments[(index + 1) % segments.Count];
        }

        public static DayScheduleConfig CreateDefaultLaborer()
        {
            DayScheduleConfig config = CreateInstance<DayScheduleConfig>();
            config.scheduleId = "laborer_default";
            config.canModify = false;
            config.segments = new List<ScheduleSegment>
            {
                new("起床/准备", ScheduleActivity.WakePrepare, 6, 0, 7, 0),
                new("工作", ScheduleActivity.Work, 7, 0, 12, 0),
                new("吃饭", ScheduleActivity.Meal, 12, 0, 13, 0),
                new("工作", ScheduleActivity.Work, 13, 0, 18, 0),
                new("吃饭", ScheduleActivity.Meal, 18, 0, 19, 0),
                new("自由时间", ScheduleActivity.Free, 19, 0, 23, 0),
                new("睡觉", ScheduleActivity.Sleep, 23, 0, 6, 0)
            };
            return config;
        }
    }
}
