using UnityEngine;
using XianXia.Unity.Obligation;
using XianXia.Unity.Presentation;
using XianXia.Unity.Time;
using XianXia.Unity.World;

namespace XianXia.Unity.Time
{
    /// <summary>
    /// 记录角色是否遵守时间表。不自动控制角色，不执行处罚。
    /// 工作时间内位于工作区 = 遵守；否则调试状态显示违反。
    /// </summary>
    public sealed class ScheduleComplianceTracker : MonoBehaviour
    {
        [SerializeField] private ScheduleService scheduleService;
        [SerializeField] private WorkZone workZone;
        [SerializeField] private DemoUnitController[] trackedUnits;
        [SerializeField] private MonoBehaviour angerSinkBehaviour;

        private ISupervisorAngerSink _angerSink;
        private readonly bool[] _wasViolating = new bool[8];

        public void Configure(
            ScheduleService service,
            WorkZone zone,
            DemoUnitController[] units,
            ISupervisorAngerSink angerSink)
        {
            scheduleService = service;
            workZone = zone;
            trackedUnits = units;
            _angerSink = angerSink;
            angerSinkBehaviour = angerSink as MonoBehaviour;
        }

        private void Awake()
        {
            if (_angerSink == null && angerSinkBehaviour is ISupervisorAngerSink sink)
            {
                _angerSink = sink;
            }
        }

        private void Update()
        {
            if (scheduleService == null || trackedUnits == null)
            {
                return;
            }

            bool requireWork = scheduleService.IsWorkPeriod;
            for (int i = 0; i < trackedUnits.Length; i++)
            {
                DemoUnitController unit = trackedUnits[i];
                if (unit == null)
                {
                    continue;
                }

                bool inWorkZone = workZone != null && workZone.Contains(unit.transform.position);
                bool compliant = !requireWork || inWorkZone;
                unit.SetScheduleCompliance(compliant, requireWork, inWorkZone);

                bool violating = requireWork && !inWorkZone;
                if (violating && (i >= _wasViolating.Length || !_wasViolating[i]))
                {
                    _angerSink?.ReportScheduleViolation(unit.name, "work_period_outside_work_zone");
                }

                if (i < _wasViolating.Length)
                {
                    _wasViolating[i] = violating;
                }
            }
        }
    }
}
