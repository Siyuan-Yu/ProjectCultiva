using NUnit.Framework;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Tests
{
    public sealed class Ch01HexPrototypeMapTests
    {
        [Test]
        public void Ch01HexMap_HuangcunAndQingyunLu_HaveRealHexDistance()
        {
            var world = new SimulationWorld();
            Ch01HexPrototypeMapBuilder.Build(world);

            Assert.IsTrue(world.HexWorld.HasGrid);
            Assert.IsTrue(world.Strategic.Sites.TryGet(Ch01HexPrototypeMapBuilder.SiteHuangcun, out var huangcun));
            Assert.IsTrue(world.Strategic.Sites.TryGet(Ch01HexPrototypeMapBuilder.SiteQingyunLu, out var qingyun));

            var distance = HexMath.Distance(huangcun.HexCoord, qingyun.HexCoord);
            Assert.Greater(distance, 3, "Sites should not be adjacent; strategic marching distance required.");
        }
    }
}
