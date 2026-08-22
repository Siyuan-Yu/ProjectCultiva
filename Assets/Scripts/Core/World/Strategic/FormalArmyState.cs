namespace XianXia.Core.World.Strategic
{
    /// <summary>Formal Army 战略状态（Phase A：仅 AtNode 语义；不含 OnRoute／Travel）。</summary>
    public enum FormalArmyState
    {
        AtNode = 0,
        Garrisoned = 1,
        OnRoute = 2
    }
}
