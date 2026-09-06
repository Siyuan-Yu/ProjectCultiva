using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Host 进入放置模式前的轻量授权 seam；V1 不接 Inventory/Crafting。</summary>
    public static class FactionFlagPlacementAuthorization
    {
        public const bool AlwaysHasPlacementTool = true;

        public static Result CanBeginPlacement(
            SimulationWorld world,
            string factionId,
            HexCoord anchor,
            out int neutralHexGain) =>
            FactionFlagService.ValidatePlacement(world, factionId, anchor, out neutralHexGain);
    }
}
