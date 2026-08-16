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
        public void WorkBand_ForestResolves_FarmHerbBandsRemoved()
        {
            var started = new PlayableDayBootstrap().Start(
                BaseGamePath,
                new PlayableDayOptions { OpeningScenarioId = "base:scenario_ch01_reference" });
            Assert.IsTrue(started.IsSuccess, started.IsFailure ? started.Error.ToString() : "");
            var world = started.Value.World;

            var forest = HostZoneQuery.FindWorkLocation(world, new Vector3(-34f, 0f, 0f));
            Assert.AreEqual("base:loc_ref_forest", forest);

            // 旧大片农田／药田色带已删：点绿草不得再命中田区工区
            Assert.IsTrue(string.IsNullOrEmpty(
                HostZoneQuery.FindWorkLocation(world, new Vector3(20f, -12f, 0f))));
            Assert.IsTrue(string.IsNullOrEmpty(
                HostZoneQuery.FindWorkLocation(world, new Vector3(-3f, -15f, 0f))));
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
        public void InteractSpots_MultiplePerForest_ResolveWork()
        {
            var started = new PlayableDayBootstrap().Start(
                BaseGamePath,
                new PlayableDayOptions { OpeningScenarioId = "base:scenario_ch01_reference" });
            Assert.IsTrue(started.IsSuccess, started.IsFailure ? started.Error.ToString() : "");
            var world = started.Value.World;

            Assert.IsTrue(HostInteractSpots.TryFindNearest(
                HostPresentationSpace.FromPresentation(-34f, 0f),
                HostInteractSpotKind.Work,
                out var a,
                3.5f,
                world));
            Assert.AreEqual("base:loc_ref_forest", a.LocationId);

            Assert.IsTrue(HostInteractSpots.TryFindNearest(
                HostPresentationSpace.FromPresentation(-32f, -3f),
                HostInteractSpotKind.Work,
                out var b,
                3.5f,
                world));
            Assert.AreEqual("base:loc_ref_forest", b.LocationId);
            Assert.AreNotEqual(a.Label, b.Label);
        }

        [Test]
        public void WorkHotspot_IgnoresOldFarmBandClick()
        {
            var started = new PlayableDayBootstrap().Start(
                BaseGamePath,
                new PlayableDayOptions { OpeningScenarioId = "base:scenario_ch01_reference" });
            Assert.IsTrue(started.IsSuccess, started.IsFailure ? started.Error.ToString() : "");
            var world = started.Value.World;

            var edge = HostPresentationSpace.FromPresentation(30f, -6f);
            Assert.IsTrue(string.IsNullOrEmpty(HostZoneQuery.FindWorkLocation(world, edge)));
            Assert.IsTrue(string.IsNullOrEmpty(HostZoneQuery.FindWorkHotspot(world, edge)));
        }
    }
}
