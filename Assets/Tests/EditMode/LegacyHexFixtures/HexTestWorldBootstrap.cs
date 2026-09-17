using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>EditMode：为依赖 Hex 战略的测试提供最小地图。</summary>
    public static class HexTestWorldBootstrap
    {
        public static void EnsureMinimalHexMap(SimulationWorld world)
        {
            if (world == null)
                return;
            if (world.HexWorld.HasGrid)
                return;
            Ch01HexPrototypeMapBuilder.BuildMinimalTwoSitePrototype(world);
        }

        public static void EnsureCh01HexMap(SimulationWorld world)
        {
            if (world == null)
                return;
            if (world.HexWorld.HasGrid)
                return;
            Ch01HexPrototypeMapBuilder.Build(world);
        }
    }
}
