using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Development-only：20k Hex 压力测试地图（无完整游戏内容）。</summary>
    public static class HexWorldStressMapBuilder
    {
        public const string MapId = "dev:hex_stress_20k";

        public static void Build(SimulationWorld world)
        {
            if (world == null)
                return;

            var grid = world.HexWorld;
            grid.Clear();
            grid.MapId = MapId;
            grid.MapName = "Hex Stress 200x100";
            grid.HexSize = HexWorldScale.DefaultHexOuterRadius;
            grid.FillRectangle(HexWorldScale.StressTestWidth, HexWorldScale.StressTestHeight, HexTerrainType.Plain);

            for (var r = 0; r < grid.Height; r++)
            {
                for (var q = 0; q < grid.Width; q++)
                {
                    if (!grid.TryGetCell(new HexCoord(q, r), out var cell) || cell == null)
                        continue;
                    var h = (q * 17 + r * 31) % 100;
                    if (h < 8)
                        cell.Terrain = HexTerrainType.Forest;
                    else if (h > 96)
                        cell.Terrain = HexTerrainType.Mountain;
                }
            }
        }
    }
}
