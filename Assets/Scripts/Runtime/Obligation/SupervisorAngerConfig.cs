using UnityEngine;

namespace XianXia.Unity.Obligation
{
    [CreateAssetMenu(
        menuName = "XianXia/Obligation/Supervisor Anger Config",
        fileName = "SupervisorAnger_Default")]
    public sealed class SupervisorAngerConfig : ScriptableObject
    {
        [SerializeField] private float incompleteTaskIncrease = 10f;
        [SerializeField] private float idleWorkHourIncreasePerUnit = 1f;
        [SerializeField] private float completedTaskDecrease = 5f;

        public float IncompleteTaskIncrease => Mathf.Max(0f, incompleteTaskIncrease);
        public float IdleWorkHourIncreasePerUnit => Mathf.Max(0f, idleWorkHourIncreasePerUnit);
        public float CompletedTaskDecrease => Mathf.Max(0f, completedTaskDecrease);

        public static SupervisorAngerConfig CreateDefault()
        {
            return CreateInstance<SupervisorAngerConfig>();
        }
    }
}
