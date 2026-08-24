using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>日界：Pure Hex 模式下无 legacy Route 派兵。</summary>
    public sealed class StrategicDayHandler : IDayBoundaryHandler
    {
        public void OnDayStarted(SimulationWorld world, ulong startedDayIndex)
        {
        }

        public void OnDayEnded(SimulationWorld world, ulong endedDayIndex)
        {
        }
    }
}
