using System;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// 玩家 FormalArmy formation／roster management 的地点规则。只读取 Resolver 已写入的
    /// Effective Controller，不关心 Control Asset 类型，也不自行重算 Territory。
    /// </summary>
    public static class FormalArmyManagementTerritoryPolicy
    {
        public static bool TryValidateFactionControlsHex(
            SimulationWorld world,
            string factionId,
            HexCoord hex,
            out GameError error)
        {
            error = default;
            if (world?.HexWorld == null || string.IsNullOrEmpty(factionId))
            {
                error = new GameError(ErrorCode.InvalidArgument, "World and faction are required.");
                return false;
            }

            var controller = TerritoryControlService.GetController(world, hex);
            if (string.Equals(controller, factionId, StringComparison.Ordinal))
                return true;

            error = new GameError(
                ErrorCode.InvalidOperation,
                "Army formation and roster operations require faction-controlled territory.",
                hex + ";expected=" + factionId + ";actual=" +
                (string.IsNullOrEmpty(controller) ? "neutral" : controller));
            return false;
        }
    }
}
