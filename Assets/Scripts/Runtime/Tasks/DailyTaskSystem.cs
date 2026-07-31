using System.Collections.Generic;
using UnityEngine;
using XianXia.Unity.Obligation;
using XianXia.Unity.Resources;
using XianXia.Unity.Time;

namespace XianXia.Unity.Tasks
{
    public sealed class DailyTaskSystem : MonoBehaviour
    {
        [SerializeField] private GameClock clock;
        [SerializeField] private ResourceInventory inventory;
        [SerializeField] private SupervisorAngerSystem supervisorAnger;
        [SerializeField] private DailyTaskConfig taskConfig;
        [SerializeField] private SupervisorAngerConfig angerConfig;

        private readonly List<DailyTaskState> _currentTasks = new();
        private int _generatedDay = -1;

        public IReadOnlyList<DailyTaskState> CurrentTasks => _currentTasks;
        public int GeneratedDay => _generatedDay;

        public bool AllComplete
        {
            get
            {
                if (_currentTasks.Count == 0)
                {
                    return false;
                }

                foreach (DailyTaskState state in _currentTasks)
                {
                    if (!state.IsComplete)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public float RemainingGameMinutes
        {
            get
            {
                if (clock == null || taskConfig == null || _generatedDay < 0)
                {
                    return 0f;
                }

                float currentAbsoluteMinutes =
                    (clock.DayNumber - 1) * GameClock.MinutesPerDay + clock.GameMinutesOfDay;
                float deadlineAbsoluteMinutes =
                    _generatedDay * GameClock.MinutesPerDay + taskConfig.GenerationHour * 60f;
                return Mathf.Max(0f, deadlineAbsoluteMinutes - currentAbsoluteMinutes);
            }
        }

        public void Configure(
            GameClock gameClock,
            ResourceInventory resourceInventory,
            SupervisorAngerSystem angerSystem,
            DailyTaskConfig dailyTaskConfig,
            SupervisorAngerConfig supervisorAngerConfig)
        {
            clock = gameClock;
            inventory = resourceInventory;
            supervisorAnger = angerSystem;
            taskConfig = dailyTaskConfig;
            angerConfig = supervisorAngerConfig;
        }

        private void OnEnable()
        {
            if (inventory != null)
            {
                inventory.ResourceChanged += OnResourceChanged;
            }
        }

        private void Start()
        {
            TryGenerateForCurrentDay();
        }

        private void OnDisable()
        {
            if (inventory != null)
            {
                inventory.ResourceChanged -= OnResourceChanged;
            }
        }

        private void Update()
        {
            TryGenerateForCurrentDay();
        }

        private void TryGenerateForCurrentDay()
        {
            if (clock == null || taskConfig == null)
            {
                return;
            }

            bool reachedGenerationTime = clock.Hour >= taskConfig.GenerationHour;
            if (!reachedGenerationTime || _generatedDay >= clock.DayNumber)
            {
                return;
            }

            if (_generatedDay >= 0)
            {
                SettlePreviousTasks();
            }

            GenerateTasks(clock.DayNumber);
        }

        private void GenerateTasks(int day)
        {
            _currentTasks.Clear();
            foreach (DailyTaskDefinition definition in taskConfig.Tasks)
            {
                if (definition != null)
                {
                    _currentTasks.Add(new DailyTaskState(definition));
                }
            }

            _generatedDay = day;
        }

        private void SettlePreviousTasks()
        {
            if (supervisorAnger == null || angerConfig == null)
            {
                return;
            }

            foreach (DailyTaskState state in _currentTasks)
            {
                if (!state.IsComplete)
                {
                    supervisorAnger.AdjustAnger(
                        angerConfig.IncompleteTaskIncrease,
                        $"daily_task_incomplete:{state.Definition.TaskId}");
                }
            }
        }

        private void OnResourceChanged(ResourceType type, int delta, int total)
        {
            if (delta <= 0 || supervisorAnger == null || angerConfig == null)
            {
                return;
            }

            foreach (DailyTaskState state in _currentTasks)
            {
                if (state.Definition.ResourceType != type || state.IsComplete)
                {
                    continue;
                }

                bool justCompleted = state.AddProgress(delta);
                if (justCompleted && !state.CompletionRewardApplied)
                {
                    state.MarkCompletionRewardApplied();
                    supervisorAnger.AdjustAnger(
                        -angerConfig.CompletedTaskDecrease,
                        $"daily_task_complete:{state.Definition.TaskId}");
                }
            }
        }
    }
}
