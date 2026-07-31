using UnityEngine;
using XianXia.Unity.Presentation;
using XianXia.Unity.World;

namespace XianXia.Unity.Time
{
    /// <summary>
    /// 按每角色当前小时时间表记录遵守情况。不自动控制角色，不执行处罚。
    /// </summary>
    public sealed class ScheduleComplianceTracker : MonoBehaviour
    {
        [SerializeField] private ScheduleService scheduleService;
        [SerializeField] private WorkSystem workSystem;
        [SerializeField] private DemoUnitController[] trackedUnits;

        public int ViolationCount { get; private set; }

        public void Configure(
            ScheduleService service,
            WorkSystem work,
            DemoUnitController[] units)
        {
            scheduleService = service;
            workSystem = work;
            trackedUnits = units;
        }

        private void Update()
        {
            RefreshCompliance();
        }

        public void RefreshCompliance()
        {
            if (scheduleService == null || trackedUnits == null)
            {
                ViolationCount = 0;
                return;
            }

            int violationCount = 0;
            for (int i = 0; i < trackedUnits.Length; i++)
            {
                DemoUnitController unit = trackedUnits[i];
                if (unit == null)
                {
                    continue;
                }

                bool requireWork = scheduleService.IsWorkPeriodFor(unit);
                bool isWorking = workSystem != null && workSystem.IsUnitWorking(unit);
                bool compliant = !requireWork || isWorking;
                unit.SetScheduleCompliance(compliant, requireWork, isWorking);
                if (requireWork && !isWorking)
                {
                    violationCount++;
                }
            }

            ViolationCount = violationCount;
        }

        public int CountWorkViolationsAtHour(int hour)
        {
            if (scheduleService == null)
            {
                return 0;
            }

            return scheduleService.CountWorkViolationsAtHour(
                hour,
                unit => workSystem != null && workSystem.IsUnitWorking(unit));
        }
    }
}
