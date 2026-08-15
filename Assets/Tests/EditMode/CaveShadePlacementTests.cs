using System.IO;
using NUnit.Framework;
using XianXia.Core.Combat;
using XianXia.Core.Cultivation;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Social;
using XianXia.Data.Bootstrap;

namespace XianXia.Tests
{
    public sealed class CaveShadePlacementTests
    {
        static string BaseGamePath =>
            Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));

        [Test]
        public void LevelTester_SpawnZone_Places_Cave_Shade_In_Chamber()
        {
            var started = new PlayableDayBootstrap().Start(
                BaseGamePath,
                new PlayableDayOptions
                {
                    OpeningScenarioId = "base:scenario_ch01_reference",
                    CharacterRosterId = "base:roster_level_tester"
                });
            Assert.IsTrue(started.IsSuccess, started.IsFailure ? started.Error.ToString() : "");

            var world = started.Value.World;
            Entity shade = null;
            foreach (var e in world.Entities.All)
            {
                if (e != null &&
                    e.DefinitionId.ToString() == "base:character_cave_shade")
                {
                    shade = e;
                    break;
                }
            }

            Assert.IsNotNull(shade, "洞府残影应由 spawnZone＋spawnTable 生成");
            Assert.IsTrue(shade.TryGet<EntityLocationComponent>(out var loc));
            Assert.AreEqual("base:loc_cave_chamber", loc.LocationId);
            Assert.IsTrue(loc.HasPresentationOverride, "刷怪区应写入表现落点");
            Assert.IsTrue(shade.TryGet<EncounterLinkComponent>(out var link));
            Assert.AreEqual("cave_ch01_shade", link.EncounterId);
            Assert.IsTrue(shade.TryGet<PersonalityProfileComponent>(out var profile));
            Assert.IsTrue(profile.HasTag("hostile"));
            Assert.IsTrue(shade.TryGet<CultivationComponent>(out var cult));
            Assert.AreEqual(RealmStage.QiRefining, cult.Realm);

            // 地表不应看见洞内残影
            Assert.IsFalse(
                XianXia.Unity.Host.LocalMapVisibility.IsEntityVisible(world, shade.Id),
                "残影在地表图应不可见");

            Assert.IsTrue(world.WorldRegion.TryGet("base:loc_cave_chamber", out var chamber));
            Assert.IsTrue(
                XianXia.Unity.Host.LocalMapVisibility.IsInteriorOnlyLocation(chamber));
            Assert.IsTrue(world.Flags.Has(
                SpawnZoneApplier.FlagPrefix + "base:map_ch01_cave:cave_spawn_chamber"));
        }
    }
}
