namespace XianXia.Core.Cultivation
{
    /// <summary>
    /// 坐下修炼基础节奏：1 tick = 5 游戏分钟 → +<see cref="BaseProgressPerTick"/> 修为。
    /// 地点／天气修正后续叠在此基数上；倍速靠 Host 加快 Tick，不改本常数。
    /// </summary>
    public static class CultivationProgressRules
    {
        public const int BaseProgressPerTick = 5;
    }
}
