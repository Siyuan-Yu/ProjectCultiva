using UnityEngine;

namespace XianXia.Unity.Time
{
    /// <summary>
    /// 只读时间表查询服务。初期玩家不可修改。
    /// </summary>
    public sealed class ScheduleService : MonoBehaviour
    {
        [SerializeField] private DayScheduleConfig schedule;
        [SerializeField] private GameClock clock;

        public DayScheduleConfig Schedule => schedule;
        public bool CanModify => schedule != null && schedule.CanModify;

        public ScheduleSegment CurrentSegment =>
            schedule == null || clock == null ? null : schedule.GetCurrent(clock.GameMinutesOfDay);

        public ScheduleSegment NextSegment =>
            schedule == null || clock == null ? null : schedule.GetNext(clock.GameMinutesOfDay);

        public float MinutesUntilNextSegment
        {
            get
            {
                ScheduleSegment current = CurrentSegment;
                if (current == null || clock == null)
                {
                    return 0f;
                }

                return current.MinutesUntilEnd(clock.GameMinutesOfDay);
            }
        }

        public bool IsWorkPeriod => CurrentSegment != null && CurrentSegment.Activity == ScheduleActivity.Work;

        public void Configure(GameClock gameClock, DayScheduleConfig config)
        {
            clock = gameClock;
            schedule = config;
        }
    }
}
