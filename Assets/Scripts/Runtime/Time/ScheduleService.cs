using System;
using UnityEngine;
using XianXia.Unity.Presentation;

namespace XianXia.Unity.Time
{
    /// <summary>
    /// 全村统一劳役时间表（主管规定）。不直接控制玩家三人或 NPC 行为；
    /// 玩家仍 RTS 下指令；NPC 劳工按表移动；愤怒系统据此判定是否该工作。
    /// </summary>
    public sealed class ScheduleService : MonoBehaviour
    {
        [SerializeField] private GameClock clock;
        [SerializeField] private DayScheduleConfig template;
        [SerializeField] private DemoUnitController[] partyUnits;
        [SerializeField] private HourlySchedule villageSchedule = new();
        [SerializeField]
        [Tooltip("正式版应为 false。当前单机测试允许改全村劳役表。")]
        private bool allowEditForTesting = true;

        public DayScheduleConfig Schedule => template;
        public HourlySchedule VillageSchedule => villageSchedule;
        public bool AllowEditForTesting => allowEditForTesting;
        public bool CanModify => allowEditForTesting;
        public DemoUnitController[] PartyUnits => partyUnits;

        public ScheduleSegment CurrentSegment =>
            template == null || clock == null ? null : template.GetCurrent(clock.GameMinutesOfDay);

        public ScheduleSegment NextSegment =>
            template == null || clock == null ? null : template.GetNext(clock.GameMinutesOfDay);

        public bool IsVillageWorkPeriod =>
            clock != null && GetVillageActivity(clock.Hour) == ScheduleActivity.Work;

        public void Configure(
            GameClock gameClock,
            DayScheduleConfig config,
            DemoUnitController[] units,
            bool enableTestingEdit)
        {
            clock = gameClock;
            template = config;
            partyUnits = units;
            allowEditForTesting = enableTestingEdit;
            if (villageSchedule == null)
            {
                villageSchedule = new HourlySchedule();
            }

            ResetVillageToDefaultLaborer();
        }

        public ScheduleActivity GetVillageActivity(int hour)
        {
            EnsureVillageSchedule();
            return villageSchedule.Get(hour);
        }

        public ScheduleActivity GetVillageActivity()
        {
            return clock == null ? ScheduleActivity.Free : GetVillageActivity(clock.Hour);
        }

        /// <summary>兼容旧调用：全村同一表，unit 参数仅用于 UI 上下文。</summary>
        public ScheduleActivity GetActivity(DemoUnitController unit)
        {
            return GetVillageActivity();
        }

        public ScheduleActivity GetActivity(DemoUnitController unit, int hour)
        {
            return GetVillageActivity(hour);
        }

        public bool IsWorkPeriodFor(DemoUnitController unit)
        {
            return IsVillageWorkPeriod;
        }

        public bool IsWorkPeriodFor(DemoUnitController unit, int hour)
        {
            return GetVillageActivity(hour) == ScheduleActivity.Work;
        }

        public bool IsVillageWorkPeriodAtHour(int hour)
        {
            return GetVillageActivity(hour) == ScheduleActivity.Work;
        }

        public bool TrySetVillageActivity(int hour, ScheduleActivity activity)
        {
            if (!allowEditForTesting)
            {
                return false;
            }

            EnsureVillageSchedule();
            villageSchedule.Set(hour, activity);
            return true;
        }

        public bool TryCycleVillageActivity(int hour)
        {
            if (!allowEditForTesting)
            {
                return false;
            }

            EnsureVillageSchedule();
            villageSchedule.Cycle(hour);
            return true;
        }

        public bool TryCycleActivity(DemoUnitController unit, int hour)
        {
            return TryCycleVillageActivity(hour);
        }

        public void ResetVillageToDefaultLaborer()
        {
            EnsureVillageSchedule();
            villageSchedule.ApplyDefaultLaborer();
        }

        public void ResetAllToDefaultLaborer()
        {
            ResetVillageToDefaultLaborer();
        }

        /// <summary>工时内未工作的可控角色人数（不论是否被发现）。</summary>
        public int CountPartyWorkViolationsAtHour(int hour, Func<DemoUnitController, bool> isWorking)
        {
            if (partyUnits == null || isWorking == null || !IsVillageWorkPeriodAtHour(hour))
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < partyUnits.Length; i++)
            {
                DemoUnitController unit = partyUnits[i];
                if (unit != null && !isWorking(unit))
                {
                    count++;
                }
            }

            return count;
        }

        private void EnsureVillageSchedule()
        {
            if (villageSchedule == null)
            {
                villageSchedule = new HourlySchedule();
                villageSchedule.ApplyDefaultLaborer();
            }
        }
    }
}
