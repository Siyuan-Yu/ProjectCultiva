using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>纯 FactionFlag domain placement 查询；不授予免费建造入口。</summary>
    public static class FactionFlagPlacementAuthorization
    {
        public static Result CanBeginPlacement(
            SimulationWorld world,
            string factionId,
            HexCoord anchor,
            out int neutralHexGain) =>
            FactionFlagService.ValidatePlacement(world, factionId, anchor, out neutralHexGain);
    }
}
