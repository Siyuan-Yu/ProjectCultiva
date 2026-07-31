using System.Collections.Generic;
using UnityEngine;
using XianXia.Unity.Resources;

namespace XianXia.Unity.Tasks
{
    [CreateAssetMenu(
        menuName = "XianXia/Tasks/Daily Task Config",
        fileName = "DailyTasks_Supervisor")]
    public sealed class DailyTaskConfig : ScriptableObject
    {
        [SerializeField] private int generationHour = 6;
        [SerializeField] private List<DailyTaskDefinition> tasks = new();

        public int GenerationHour => Mathf.Clamp(generationHour, 0, 23);
        public IReadOnlyList<DailyTaskDefinition> Tasks => tasks;

        public static DailyTaskConfig CreateDefaultSupervisorTasks()
        {
            DailyTaskConfig config = CreateInstance<DailyTaskConfig>();
            config.generationHour = 6;
            config.tasks = new List<DailyTaskDefinition>
            {
                new("collect_wood", "收集木材", ResourceType.Wood, 20),
                new("collect_herb", "收集草药", ResourceType.Herb, 5)
            };
            return config;
        }
    }
}
