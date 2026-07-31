using System;
using UnityEngine;

namespace XianXia.Unity.Time
{
    /// <summary>
    /// 单角色 24 小时时间表。索引 = 游戏小时 0～23。
    /// </summary>
    [Serializable]
    public sealed class HourlySchedule
    {
        public const int HoursPerDay = 24;

        [SerializeField] private ScheduleActivity[] hours = CreateDefaultLaborerHours();

        public ScheduleActivity Get(int hour)
        {
            EnsureArray();
            hour = ((hour % HoursPerDay) + HoursPerDay) % HoursPerDay;
            return hours[hour];
        }

        public void Set(int hour, ScheduleActivity activity)
        {
            EnsureArray();
            hour = ((hour % HoursPerDay) + HoursPerDay) % HoursPerDay;
            hours[hour] = activity;
        }

        public ScheduleActivity Cycle(int hour)
        {
            ScheduleActivity current = Get(hour);
            ScheduleActivity next = current switch
            {
                ScheduleActivity.Sleep => ScheduleActivity.WakePrepare,
                ScheduleActivity.WakePrepare => ScheduleActivity.Work,
                ScheduleActivity.Work => ScheduleActivity.Meal,
                ScheduleActivity.Meal => ScheduleActivity.Free,
                _ => ScheduleActivity.Sleep
            };
            Set(hour, next);
            return next;
        }

        public void CopyFrom(HourlySchedule other)
        {
            EnsureArray();
            if (other == null)
            {
                return;
            }

            for (int i = 0; i < HoursPerDay; i++)
            {
                hours[i] = other.Get(i);
            }
        }

        public void ApplyDefaultLaborer()
        {
            hours = CreateDefaultLaborerHours();
        }

        public static ScheduleActivity[] CreateDefaultLaborerHours()
        {
            var result = new ScheduleActivity[HoursPerDay];
            for (int hour = 0; hour < HoursPerDay; hour++)
            {
                if (hour >= 6 && hour < 7)
                {
                    result[hour] = ScheduleActivity.WakePrepare;
                }
                else if (hour >= 7 && hour < 12)
                {
                    result[hour] = ScheduleActivity.Work;
                }
                else if (hour >= 12 && hour < 13)
                {
                    result[hour] = ScheduleActivity.Meal;
                }
                else if (hour >= 13 && hour < 18)
                {
                    result[hour] = ScheduleActivity.Work;
                }
                else if (hour >= 18 && hour < 19)
                {
                    result[hour] = ScheduleActivity.Meal;
                }
                else if (hour >= 19 && hour < 23)
                {
                    result[hour] = ScheduleActivity.Free;
                }
                else
                {
                    result[hour] = ScheduleActivity.Sleep;
                }
            }

            return result;
        }

        private void EnsureArray()
        {
            if (hours == null || hours.Length != HoursPerDay)
            {
                hours = CreateDefaultLaborerHours();
            }
        }
    }
}
