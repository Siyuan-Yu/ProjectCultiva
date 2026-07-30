namespace XianXia.Unity.Obligation
{
    /// <summary>
    /// 主管愤怒预留接口。Demo 本阶段只记录，不执行处罚。
    /// </summary>
    public interface ISupervisorAngerSink
    {
        void ReportScheduleViolation(string unitId, string reason);
        float CurrentAnger { get; }
    }
}
