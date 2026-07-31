using System;
using UnityEngine;
using XianXia.Unity.Time;

namespace XianXia.Unity.Obligation
{
    public sealed class SupervisorAngerSystem : MonoBehaviour, ISupervisorAngerSink
    {
        [SerializeField] private float anger;
        [SerializeField] private GameClock clock;
        [SerializeField] private ScheduleService scheduleService;
        [SerializeField] private ScheduleComplianceTracker complianceTracker;
        [SerializeField] private SupervisorAngerConfig config;

        private int _lastCheckedAbsoluteHour = -1;

        public event Action<float, float, string> AngerChanged;

        public float CurrentAnger => anger;
        public SupervisorAngerConfig Config => config;

        public void Configure(
            GameClock gameClock,
            ScheduleService schedule,
            ScheduleComplianceTracker tracker,
            SupervisorAngerConfig angerConfig)
        {
            clock = gameClock;
            scheduleService = schedule;
            complianceTracker = tracker;
            config = angerConfig;
            anger = 0f;
            _lastCheckedAbsoluteHour = -1;
        }

        private void Update()
        {
            if (clock == null || scheduleService == null || complianceTracker == null || config == null)
            {
                return;
            }

            int absoluteHour = (clock.DayNumber - 1) * 24 + clock.Hour;
            if (_lastCheckedAbsoluteHour < 0)
            {
                _lastCheckedAbsoluteHour = absoluteHour;
                return;
            }

            if (absoluteHour == _lastCheckedAbsoluteHour)
            {
                return;
            }

            int completedHour = _lastCheckedAbsoluteHour % 24;
            _lastCheckedAbsoluteHour = absoluteHour;
            int violationCount = complianceTracker.CountWorkViolationsAtHour(completedHour);
            if (violationCount > 0)
            {
                AdjustAnger(
                    config.IdleWorkHourIncreasePerUnit * violationCount,
                    $"work_time_idle:{violationCount}");
            }

            complianceTracker.RefreshCompliance();
        }

        public void AdjustAnger(float delta, string reason)
        {
            if (Mathf.Approximately(delta, 0f))
            {
                return;
            }

            float previous = anger;
            anger = Mathf.Clamp(anger + delta, 0f, 100f);
            if (!Mathf.Approximately(previous, anger))
            {
                AngerChanged?.Invoke(previous, anger, reason);
            }
        }
    }
}
