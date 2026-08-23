namespace XianXia.Core.World.Strategic
{
    /// <summary>Formal Army 战略状态。Idle/Moving 为 Hex 模型；AtNode/OnRoute 为 legacy Route 模型（迁移中）。</summary>
    public enum FormalArmyState
    {
        AtNode = 0,
        Garrisoned = 1,
        OnRoute = 2,
        Idle = 3,
        Moving = 4,
    }
}
