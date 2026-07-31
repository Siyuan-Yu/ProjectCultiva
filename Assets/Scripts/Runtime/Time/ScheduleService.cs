using System;
using UnityEngine;
using XianXia.Unity.Presentation;

namespace XianXia.Unity.Time
{
    /// <summary>
    /// 每角色小时时间表。正式版应锁定；Demo 单机测试允许修改。
    /// </summary>
    public sealed class ScheduleService : MonoBehaviour
    {
        [SerializeField] private GameClock clock;
        [SerializeField] private DayScheduleConfig template;
        [SerializeField] private DemoUnitController[] units;
        [SerializeField] private HourlySchedule[] unitSchedules = Array.Empty<HourlySchedule>();
        [SerializeField]
        [Tooltip("正式版应为 false。当前单机测试先允许修改。")]
        private bool allowEditForTesting = true;

        public DayScheduleConfig Schedule => template;
        public bool AllowEditForTesting => allowEditForTesting;
        public bool CanModify => allowEditForTesting;
        public DemoUnitController[] Units => units;
        public int UnitCount => units == null ? 0 : units.Length;

        public ScheduleSegment CurrentSegment =>
            template == null || clock == null ? null : template.GetCurrent(clock.GameMinutesOfDay);

        public ScheduleSegment NextSegment =>
            template == null || clock == null ? null : template.GetNext(clock.GameMinutesOfDay);

        public float MinutesUntilNextSegment
        {
            get
            {
                if (clock == null)
                {
                    return 0f;
                }

                float minute = clock.GameMinutesOfDay;
                float endOfHour = (Mathf.Floor(minute / 60f) + 1f) * 60f;
                return Mathf.Max(0f, endOfHour - minute);
            }
        }

        public bool IsWorkPeriod => clock != null && AnyUnitScheduled(ScheduleActivity.Work, clock.Hour);

        public void Configure(
            GameClock gameClock,
            DayScheduleConfig config,
            DemoUnitController[] partyUnits,
            bool enableTestingEdit)
        {
            clock = gameClock;
            template = config;
            units = partyUnits;
            allowEditForTesting = enableTestingEdit;
            EnsureSchedules();
            ResetAllToDefaultLaborer();
        }

        public ScheduleActivity GetActivity(DemoUnitController unit)
        {
            return clock == null ? ScheduleActivity.Free : GetActivity(unit, clock.Hour);
        }

        public ScheduleActivity GetActivity(DemoUnitController unit, int hour)
        {
            int index = IndexOf(unit);
            if (index < 0)
            {
                return ScheduleActivity.Free;
            }

            EnsureSchedules();
            return unitSchedules[index].Get(hour);
        }

        public bool IsWorkPeriodFor(DemoUnitController unit)
        {
            return GetActivity(unit) == ScheduleActivity.Work;
        }

        public bool IsWorkPeriodFor(DemoUnitController unit, int hour)
        {
            return GetActivity(unit, hour) == ScheduleActivity.Work;
        }

        public bool TrySetActivity(DemoUnitController unit, int hour, ScheduleActivity activity)
        {
            if (!allowEditForTesting)
            {
                return false;
            }

            int index = IndexOf(unit);
            if (index < 0)
            {
                return false;
            }

            EnsureSchedules();
            unitSchedules[index].Set(hour, activity);
            return true;
        }

        public bool TryCycleActivity(DemoUnitController unit, int hour)
        {
            if (!allowEditForTesting)
            {
                return false;
            }

            int index = IndexOf(unit);
            if (index < 0)
            {
                return false;
            }

            EnsureSchedules();
            unitSchedules[index].Cycle(hour);
            return true;
        }

        public void ResetAllToDefaultLaborer()
        {
            EnsureSchedules();
            for (int i = 0; i < unitSchedules.Length; i++)
            {
                unitSchedules[i].ApplyDefaultLaborer();
            }
        }

        public int CountWorkViolationsAtHour(int hour, Func<DemoUnitController, bool> isWorking)
        {
            if (units == null || isWorking == null)
            {
                return 0;
            }

            EnsureSchedules();
            int count = 0;
            for (int i = 0; i < units.Length; i++)
            {
                DemoUnitController unit = units[i];
                if (unit == null || GetActivity(unit, hour) != ScheduleActivity.Work)
                {
                    continue;
                }

                if (!isWorking(unit))
                {
                    count++;
                }
            }

            return count;
        }

        private bool AnyUnitScheduled(ScheduleActivity activity, int hour)
        {
            if (units == null)
            {
                return false;
            }

            for (int i = 0; i < units.Length; i++)
            {
                if (units[i] != null && GetActivity(units[i], hour) == activity)
                {
                    return true;
                }
            }

            return false;
        }

        private int IndexOf(DemoUnitController unit)
        {
            if (unit == null || units == null)
            {
                return -1;
            }

            for (int i = 0; i < units.Length; i++)
            {
                if (units[i] == unit)
                {
                    return i;
                }
            }

            return -1;
        }

        private void EnsureSchedules()
        {
            int count = units == null ? 0 : units.Length;
            if (unitSchedules != null && unitSchedules.Length == count)
            {
                for (int i = 0; i < count; i++)
                {
                    if (unitSchedules[i] == null)
                    {
                        unitSchedules[i] = new HourlySchedule();
                        unitSchedules[i].ApplyDefaultLaborer();
                    }
                }

                return;
            }

            unitSchedules = new HourlySchedule[count];
            for (int i = 0; i < count; i++)
            {
                unitSchedules[i] = new HourlySchedule();
                unitSchedules[i].ApplyDefaultLaborer();
            }
        }
    }
}
