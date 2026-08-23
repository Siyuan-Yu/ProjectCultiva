using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Hex 战略 Runtime 开关；玩家正式 Runtime 在启用后不再走 Route movement。</summary>
    public static class HexStrategicRuntime
    {
        public static bool IsActive(SimulationWorld world) =>
            HexStrategicMapBootstrap.UseHexStrategicMap &&
            world?.HexWorld != null &&
            world.HexWorld.HasGrid;
    }
}
