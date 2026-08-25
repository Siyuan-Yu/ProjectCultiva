namespace XianXia.Core.World.Hex
{
    /// <summary>
    /// Hex 战略旅行模式。V1 仅 Ground；Flight 预留，避免寻路 API 只能绑 FormalArmy。
    /// </summary>
    public enum HexTravelMode
    {
        Ground = 0,
        // Flight = 1, // Phase 后续
    }
}
