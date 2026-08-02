using NUnit.Framework;
using UnityEngine;
using XianXia.Data.Bootstrap;
using XianXia.Unity.Host;
using System.IO;

namespace XianXia.Tests
{
    public sealed class HostZoneQueryTests
    {
        static string BaseGamePath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Content", "BaseGame"));

        [Test]
        public void WorkBand_FarmAndForest_ResolveToResourceLocations()
        {
            var started = new PlayableDayBootstrap().Start(
                BaseGamePath,
                new PlayableDayOptions { OpeningScenarioId = "base:scenario_ch01_reference" });
            Assert.IsTrue(started.IsSuccess, started.IsFailure ? started.Error.ToString() : "");
            var world = started.Value.World;

            var farm = HostZoneQuery.FindWorkLocation(world, new Vector3(20f, -12f, 0f));
            Assert.AreEqual("base:loc_ref_labor_yard", farm);

            var forest = HostZoneQuery.FindWorkLocation(world, new Vector3(-34f, 0f, 0f));
            Assert.AreEqual("base:loc_ref_forest", forest);

            var herb = HostZoneQuery.FindWorkLocation(world, new Vector3(-3f, -15f, 0f));
            Assert.AreEqual("base:loc_ref_herb_field", herb);
        }

        [Test]
        public void SpiritBand_ResolvesCultivateLocations()
        {
            var started = new PlayableDayBootstrap().Start(
                BaseGamePath,
                new PlayableDayOptions { OpeningScenarioId = "base:scenario_ch01_reference" });
            Assert.IsTrue(started.IsSuccess, started.IsFailure ? started.Error.ToString() : "");
            var world = started.Value.World;

            var spring = HostZoneQuery.FindCultivateLocation(world, new Vector3(28f, -12f, 0f));
            Assert.AreEqual("base:loc_ref_spring", spring);

            var cave = HostZoneQuery.FindCultivateLocation(world, new Vector3(27f, -14f, 0f));
            Assert.AreEqual("base:loc_ref_cave", cave);
        }

        [Test]
        public void WorkHotspot_IgnoresFarBandClick_UnlikeWorkLocation()
        {
            var started = new PlayableDayBootstrap().Start(
                BaseGamePath,
                new PlayableDayOptions { OpeningScenarioId = "base:scenario_ch01_reference" });
            Assert.IsTrue(started.IsSuccess, started.IsFailure ? started.Error.ToString() : "");
            var world = started.Value.World;

            var edge = HostPresentationSpace.FromPresentation(30f, -6f);
            var band = HostZoneQuery.FindWorkLocation(world, edge);
            var hot = HostZoneQuery.FindWorkHotspot(world, edge);
            Assert.AreEqual("base:loc_ref_labor_yard", band);
            Assert.IsTrue(string.IsNullOrEmpty(hot), "far band click must not force labor hotspot");
        }
    }
}
