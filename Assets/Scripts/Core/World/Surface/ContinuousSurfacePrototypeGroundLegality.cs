using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Surface
{
    /// <summary>W1C migration/prototype safety guard, NOT future Surface physics authority.
    /// Roads affect travel cost only; ordinary passable ground does not require a road.</summary>
    public static class ContinuousSurfacePrototypeGroundLegality
    {
        public const string BlockedDiagnostic = "ContinuousStrategicLegalityBlocked";

        public static bool CanCross(HexWorld world, HexCoord committed, HexCoord candidate)
        {
            if (committed.Equals(candidate)) return true;
            return world != null && world.TryGetTile(candidate, out var tile) &&
                   tile.Terrain != HexTerrainType.Water && tile.IsPassable;
        }

        public static bool CanMoveTo(HexWorld world, HexCoord committed, WorldVec2 position, float size)
        {
            var derived = HexMath.WorldToHex(position.X, position.Y, size);
            var next = ContinuousSurfaceHexCommitResolver.Resolve(committed, position, size);
            // Do not let the commit hysteresis band accumulate a canonical position in water.
            return CanCross(world, committed, derived) && CanCross(world, committed, next);
        }
    }
}
