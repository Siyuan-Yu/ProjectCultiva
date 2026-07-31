using System;
using UnityEngine;
using XianXia.Unity.Resources;

namespace XianXia.Unity.Tasks
{
    [Serializable]
    public sealed class DailyTaskDefinition
    {
        [SerializeField] private string taskId = "task";
        [SerializeField] private string displayName = "每日任务";
        [SerializeField] private ResourceType resourceType;
        [SerializeField] private int requiredAmount = 1;

        public string TaskId => taskId;
        public string DisplayName => displayName;
        public ResourceType ResourceType => resourceType;
        public int RequiredAmount => Mathf.Max(1, requiredAmount);

        public DailyTaskDefinition(
            string id,
            string name,
            ResourceType type,
            int amount)
        {
            taskId = id;
            displayName = name;
            resourceType = type;
            requiredAmount = amount;
        }
    }
}
