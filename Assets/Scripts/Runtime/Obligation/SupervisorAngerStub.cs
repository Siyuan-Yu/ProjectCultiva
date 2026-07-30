using UnityEngine;

namespace XianXia.Unity.Obligation
{
    /// <summary>
    /// 主管愤怒占位实现：只保留数据与接口，暂不处罚。
    /// </summary>
    public sealed class SupervisorAngerStub : MonoBehaviour, ISupervisorAngerSink
    {
        [SerializeField] private float anger;
        [SerializeField] private bool logViolations;

        public float CurrentAnger => anger;

        public void ReportScheduleViolation(string unitId, string reason)
        {
            // 预留：后续接入主管愤怒数值与处罚。当前不累加、不惩罚。
            if (logViolations)
            {
                Debug.Log($"[SupervisorAngerStub] violation reserved: {unitId} / {reason}");
            }
        }

        public void Configure(bool enableDebugLog)
        {
            logViolations = enableDebugLog;
        }
    }
}
