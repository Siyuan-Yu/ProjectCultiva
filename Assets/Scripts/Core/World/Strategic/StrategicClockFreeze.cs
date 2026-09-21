using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>战略 WorldTick 冻结原因（ADR-0023）。</summary>
    public enum StrategicClockFreezeReason
    {
        None = 0,
        // Retained numeric compatibility; retired BattleOffer runtime no longer assigns this value.
        BattleOffer = 1,
        ManualEncounter = 2,
        PostBattle = 3,
        // Retained numeric compatibility; retired interrupt queue no longer assigns this value.
        InterruptQueue = 4
    }

    /// <summary>战略时钟冻结态；与 Host 战术 IsPaused 分离。</summary>
    public sealed class StrategicClockFreezeState
    {
        public StrategicClockFreezeReason Reason { get; set; }

        public bool IsWorldTickFrozen => Reason != StrategicClockFreezeReason.None;

        /// <summary>手动战／战后：锁 ActiveMap、禁战略令。</summary>
        public bool IsModalEncounter =>
            Reason == StrategicClockFreezeReason.ManualEncounter ||
            Reason == StrategicClockFreezeReason.PostBattle;

        public void Clear()
        {
            Reason = StrategicClockFreezeReason.None;
        }
    }

    /// <summary>Modern CharacterEncounter manual／post-battle state freezes WorldTick.</summary>
    public static class StrategicClockFreezeService
    {
        public static bool IsWorldTickFrozen(SimulationWorld world) =>
            world?.Strategic?.ClockFreeze != null && world.Strategic.ClockFreeze.IsWorldTickFrozen;

        public static bool IsModalEncounter(SimulationWorld world) =>
            world?.Strategic?.ClockFreeze != null && world.Strategic.ClockFreeze.IsModalEncounter;

    }
}
