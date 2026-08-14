namespace XianXia.Core.Domain.Time
{
    /// <summary>
    /// Host 自动步进与 Core 劳动换算共用的 1x 节奏。
    /// 1 tick = <see cref="WorldTick.GameMinutesPerTick"/> 游戏分钟；
    /// 1x 下每 1 现实秒 +1 tick → 1 现实秒 = 5 游戏分钟；5x → 25 游戏分钟。
    /// </summary>
    public static class SimulationTickPacing
    {
        public const float SecondsPerTickAt1x = 1f;

        /// <summary>1x：每现实秒推进的游戏分钟数。</summary>
        public const int GameMinutesPerRealSecondAt1x = 5;

        public static int GameMinutesPerRealSecondAtSpeed(int speedMultiplier)
        {
            if (speedMultiplier < 1)
                speedMultiplier = 1;
            return GameMinutesPerRealSecondAt1x * speedMultiplier;
        }

        /// <summary>指定 Host 倍速下跑完一整游戏日所需的现实秒数（1x=288s，5x=57.6s）。</summary>
        public static float RealSecondsPerGameDayAtSpeed(int speedMultiplier)
        {
            if (speedMultiplier < 1)
                speedMultiplier = 1;
            return WorldTick.TicksPerDay * SecondsPerTickAt1x / speedMultiplier;
        }
    }
}
