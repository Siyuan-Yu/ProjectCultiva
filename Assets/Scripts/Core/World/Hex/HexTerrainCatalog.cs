using System;

namespace XianXia.Core.World.Hex
{
    public static class HexTerrainCatalog
    {
        public static float DefaultMovementCost(HexTerrainType terrain, bool isRoad)
        {
            var baseCost = terrain switch
            {
                HexTerrainType.Plain => 1.0f,
                HexTerrainType.Forest => 1.5f,
                HexTerrainType.Mountain => 2.0f,
                HexTerrainType.Water => float.PositiveInfinity,
                HexTerrainType.Road => 0.7f,
                _ => 1.0f
            };

            if (isRoad && terrain != HexTerrainType.Road && float.IsFinite(baseCost))
                baseCost *= 0.7f;

            return baseCost;
        }

        public static bool IsPassableByDefault(HexTerrainType terrain) =>
            terrain != HexTerrainType.Water;
    }
}
