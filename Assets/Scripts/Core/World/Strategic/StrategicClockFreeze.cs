using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>战略 WorldTick 冻结原因（ADR-0023）。</summary>
    public enum StrategicClockFreezeReason
    {
        None = 0,
        BattleOffer = 1,
        ManualEncounter = 2,
        PostBattle = 3,
        InterruptQueue = 4
    }

    /// <summary>战略时钟冻结态；与 Host 战术 IsPaused 分离。</summary>
    public sealed class StrategicClockFreezeState
    {
        public StrategicClockFreezeReason Reason { get; set; }

        /// <summary>Host 已写入开战前 pause／倍速。</summary>
        public bool HasSavedHostPresentation { get; set; }

        public bool SavedHostPaused { get; set; }

        public int SavedSpeedMultiplier { get; set; } = 1;

        public bool IsWorldTickFrozen => Reason != StrategicClockFreezeReason.None;

        /// <summary>手动战／战后：锁 ActiveMap、禁战略令。</summary>
        public bool IsModalEncounter =>
            Reason == StrategicClockFreezeReason.ManualEncounter ||
            Reason == StrategicClockFreezeReason.PostBattle;

        public void Clear()
        {
            Reason = StrategicClockFreezeReason.None;
            HasSavedHostPresentation = false;
            SavedHostPaused = false;
            SavedSpeedMultiplier = 1;
        }
    }

    /// <summary>ADR-0023：BattleOffer／Manual／PostBattle 冻结 WorldTick。</summary>
    public static class StrategicClockFreezeService
    {
        public static bool IsWorldTickFrozen(SimulationWorld world) =>
            world?.Strategic?.ClockFreeze != null && world.Strategic.ClockFreeze.IsWorldTickFrozen;

        public static bool IsModalEncounter(SimulationWorld world) =>
            world?.Strategic?.ClockFreeze != null && world.Strategic.ClockFreeze.IsModalEncounter;

        public static void BeginOrPromote(SimulationWorld world, StrategicClockFreezeReason reason)
        {
            if (world?.Strategic == null || reason == StrategicClockFreezeReason.None)
                return;

            var freeze = world.Strategic.ClockFreeze;
            if (!freeze.IsWorldTickFrozen)
            {
                freeze.Reason = reason;
                return;
            }

            // 只允许提升：Offer → Manual → PostBattle（或保持）
            if ((int)reason >= (int)freeze.Reason)
                freeze.Reason = reason;
        }

        public static void EndFreeze(SimulationWorld world)
        {
            world?.Strategic?.ClockFreeze?.Clear();
        }

        public static void CaptureHostPresentationIfNeeded(
            SimulationWorld world,
            bool hostPaused,
            int speedMultiplier)
        {
            if (world?.Strategic?.ClockFreeze == null)
                return;
            var freeze = world.Strategic.ClockFreeze;
            if (!freeze.IsWorldTickFrozen || freeze.HasSavedHostPresentation)
                return;

            freeze.SavedHostPaused = hostPaused;
            freeze.SavedSpeedMultiplier = speedMultiplier < 1 ? 1 : speedMultiplier;
            freeze.HasSavedHostPresentation = true;
        }
    }
}
