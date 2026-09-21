using XianXia.Core.World;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Surface
{
    /// <summary>Compatibility-only Hex legality for retired routes without a baked Surface path.
    /// Normal Continuous Outdoor movement uses SurfaceGroundNavigation.</summary>
    public static class ContinuousSurfacePrototypeGroundLegality
    {
        public const string BlockedDiagnostic = "ContinuousStrategicLegalityBlocked";

        public static bool CanCross(HexWorld world, HexCoord committed, HexCoord candidate)
        {
            if (committed.Equals(candidate)) return true;
            return world != null && world.TryGetTile(candidate, out var tile) &&
                   tile.Terrain != HexTerrainType.Water && tile.IsPassable;
        }

    }
}
