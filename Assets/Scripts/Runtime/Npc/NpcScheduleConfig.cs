using UnityEngine;

namespace XianXia.Unity.Npc
{
    /// <summary>
    /// 可配置 NPC 日程：24 小时相位（工作／休息／巡逻）。
    /// </summary>
    [CreateAssetMenu(
        menuName = "XianXia/Npc/Npc Schedule Config",
        fileName = "NpcSchedule_Default")]
    public sealed class NpcScheduleConfig : ScriptableObject
    {
        public const int HoursPerDay = 24;

        [SerializeField] private string scheduleId = "npc_default";
        [SerializeField] private string displayName = "NPC日程";
        [SerializeField] private NpcDutyPhase[] hours = CreateFilled(NpcDutyPhase.Rest);

        public string ScheduleId => scheduleId;
        public string DisplayName => displayName;

        public NpcDutyPhase GetDuty(int hour)
        {
            EnsureHours();
            hour = Mathf.Clamp(hour, 0, HoursPerDay - 1);
            return hours[hour];
        }

        public void SetDuty(int hour, NpcDutyPhase phase)
        {
            EnsureHours();
            hours[Mathf.Clamp(hour, 0, HoursPerDay - 1)] = phase;
        }

        public static NpcScheduleConfig CreateDefaultGuard()
        {
            NpcScheduleConfig config = CreateInstance<NpcScheduleConfig>();
            config.scheduleId = "guard_patrol";
            config.displayName = "守卫巡逻日程";
            config.hours = CreateFilled(NpcDutyPhase.Rest);
            // 06–21 巡逻，22–05 休息
            for (int hour = 6; hour <= 21; hour++)
            {
                config.hours[hour] = NpcDutyPhase.Patrol;
            }

            return config;
        }

        public static NpcScheduleConfig CreateDefaultSupervisor()
        {
            NpcScheduleConfig config = CreateInstance<NpcScheduleConfig>();
            config.scheduleId = "supervisor_inspect";
            config.displayName = "主管巡视日程";
            config.hours = CreateFilled(NpcDutyPhase.Rest);
            // 白天 07–18 巡视／检查，晚上回住所
            for (int hour = 7; hour <= 18; hour++)
            {
                config.hours[hour] = NpcDutyPhase.Patrol;
            }

            return config;
        }

        public static NpcScheduleConfig CreateDefaultVillagerGroup()
        {
            NpcScheduleConfig config = CreateInstance<NpcScheduleConfig>();
            config.scheduleId = "villager_group";
            config.displayName = "村民群体日程";
            config.hours = CreateFilled(NpcDutyPhase.Rest);
            for (int hour = 7; hour < 12; hour++)
            {
                config.hours[hour] = NpcDutyPhase.Work;
            }

            for (int hour = 13; hour < 18; hour++)
            {
                config.hours[hour] = NpcDutyPhase.Work;
            }

            return config;
        }

        private void EnsureHours()
        {
            if (hours == null || hours.Length != HoursPerDay)
            {
                hours = CreateFilled(NpcDutyPhase.Rest);
            }
        }

        private static NpcDutyPhase[] CreateFilled(NpcDutyPhase phase)
        {
            var result = new NpcDutyPhase[HoursPerDay];
            for (int i = 0; i < HoursPerDay; i++)
            {
                result[i] = phase;
            }

            return result;
        }
    }
}
