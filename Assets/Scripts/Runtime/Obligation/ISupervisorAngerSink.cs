namespace XianXia.Unity.Obligation
{
    /// <summary>
    /// 主管愤怒预留接口。Demo 本阶段只记录，不执行处罚。
    /// </summary>
    public interface ISupervisorAngerSink
    {
        float CurrentAnger { get; }
        void AdjustAnger(float delta, string reason);
    }
}
