using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Hex Runtime 激活时禁止正式 Route movement 入口（H8）。</summary>
    public static class HexStrategicLegacyGuard
    {
        public static bool RejectRouteMovement(SimulationWorld world, out GameError error)
        {
            error = default;
            if (!ArmyHexCommandService.IsHexStrategicActive(world))
                return false;
            error = new GameError(
                ErrorCode.InvalidOperation,
                "Route movement is disabled while hex strategic map is active.");
            return true;
        }
    }
}
