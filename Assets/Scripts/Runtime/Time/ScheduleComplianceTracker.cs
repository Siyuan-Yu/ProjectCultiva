using System.Collections.Generic;
using UnityEngine;
using XianXia.Unity.Obligation;
using XianXia.Unity.Presentation;
using XianXia.Unity.World;

namespace XianXia.Unity.Time
{
    /// <summary>
    /// 按全村劳役表记录玩家小队遵守情况；愤怒仅对「被发现」的偷懒累计。
    /// </summary>
    public sealed class ScheduleComplianceTracker : MonoBehaviour
    {
        [SerializeField] private ScheduleService scheduleService;
        [SerializeField] private WorkSystem workSystem;
        [SerializeField] private SupervisorAngerConfig angerConfig;
        [SerializeField] private DemoUnitController[] trackedUnits;
        [SerializeField] private float detectionRadius = 10f;

        private readonly List<Transform> _authorityAnchors = new();

        public int ViolationCount { get; private set; }

        public void Configure(
            ScheduleService service,
            WorkSystem work,
            DemoUnitController[] units,
            SupervisorAngerConfig config = null,
            float detectRadius = 10f)
        {
            scheduleService = service;
            workSystem = work;
            trackedUnits = units;
            angerConfig = config;
            detectionRadius = detectRadius;
            RefreshAuthorityAnchors();
        }

        private void Start()
        {
            RefreshAuthorityAnchors();
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

            bool requireWork = scheduleService.IsVillageWorkPeriod;
            int violationCount = 0;
            for (int i = 0; i < trackedUnits.Length; i++)
            {
                DemoUnitController unit = trackedUnits[i];
                if (unit == null)
                {
                    continue;
                }

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

        /// <summary>该小时工时内、未工作、且处于主管/守卫视野内的违规人数。</summary>
        public int CountDetectedWorkViolationsAtHour(int hour)
        {
            if (scheduleService == null || trackedUnits == null || !scheduleService.IsVillageWorkPeriodAtHour(hour))
            {
                return 0;
            }

            RefreshAuthorityAnchors();
            float radius = angerConfig != null ? Mathf.Max(detectionRadius, 8f) : detectionRadius;
            int count = 0;
            for (int i = 0; i < trackedUnits.Length; i++)
            {
                DemoUnitController unit = trackedUnits[i];
                if (unit == null)
                {
                    continue;
                }

                bool isWorking = workSystem != null && workSystem.IsUnitWorking(unit);
                if (isWorking || !IsDetected(unit.transform.position, radius))
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        public int CountWorkViolationsAtHour(int hour)
        {
            return CountDetectedWorkViolationsAtHour(hour);
        }

        private bool IsDetected(Vector3 position, float radius)
        {
            if (_authorityAnchors.Count == 0)
            {
                RefreshAuthorityAnchors();
            }

            float radiusSq = radius * radius;
            for (int i = 0; i < _authorityAnchors.Count; i++)
            {
                Transform anchor = _authorityAnchors[i];
                if (anchor == null)
                {
                    continue;
                }

                Vector2 delta = (Vector2)anchor.position - (Vector2)position;
                if (delta.sqrMagnitude <= radiusSq)
                {
                    return true;
                }
            }

            return false;
        }

        private void RefreshAuthorityAnchors()
        {
            _authorityAnchors.Clear();
            TryAddAnchor("Supervisor");
            TryAddAnchor("Guard_01");
            TryAddAnchor("Guard_02");
        }

        private void TryAddAnchor(string objectName)
        {
            GameObject go = GameObject.Find(objectName);
            if (go != null)
            {
                _authorityAnchors.Add(go.transform);
            }
        }
    }
}
