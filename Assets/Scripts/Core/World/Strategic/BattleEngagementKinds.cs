namespace XianXia.Core.World.Strategic
{
    public enum BattleInitiatorKind
    {
        None = 0,
        FormalArmy = 1,
        PlayerParty = 2,
    }

    public enum BattleDecisionSubjectKind
    {
        None = 0,
        FormalArmy = 1,
        PlayerParty = 2,
    }

    /// <summary>Domain 决定弹窗选项；UI 只读此结构绘制按钮。</summary>
    public sealed class BattleDecisionOptions
    {
        public bool Manual { get; set; }
        public bool Auto { get; set; } = true;
        public bool Retreat { get; set; }
        public BattleDecisionSubjectKind PlayerDecisionSubjectKind { get; set; }
        public string PlayerDecisionSubjectFormalArmyId { get; set; } = string.Empty;
    }
}
