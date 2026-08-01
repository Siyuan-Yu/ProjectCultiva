using NUnit.Framework;
using XianXia.Core.Bootstrap;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Data.Bootstrap;

namespace XianXia.Tests
{
    public sealed class PersonalityPhaseATests
    {
        [Test]
        public void GameStart_AppliesPersonalityTagsFromSpawn()
        {
            var bootstrap = new GameStartBootstrap();
            var worldData = new WorldInitData
            {
                Regions = { new RegionData { Id = new RegionId(1), Name = "t" } }
            };
            var spawns = new[]
            {
                new CharacterSpawnRequest
                {
                    DefinitionId = new DefinitionId("base", "a"),
                    Name = "甲",
                    PersonalityTags = { "personality_bold", "personality_curious" }
                },
                new CharacterSpawnRequest
                {
                    DefinitionId = new DefinitionId("base", "b"),
                    Name = "乙",
                    PersonalityTags = { "personality_cautious" }
                }
            };

            var started = bootstrap.Start(worldData, spawns);
            Assert.IsTrue(started.IsSuccess, started.IsFailure ? started.Error.ToString() : "");

            var world = started.Value.World;
            Assert.IsTrue(world.Entities.TryGet(started.Value.CharacterIds[0], out var a));
            Assert.IsTrue(world.Entities.TryGet(started.Value.CharacterIds[1], out var b));
            Assert.IsTrue(a.Get<PersonalityProfileComponent>().HasTag("personality_bold"));
            Assert.IsTrue(a.Get<PersonalityProfileComponent>().HasTag("personality_curious"));
            Assert.IsFalse(a.Get<PersonalityProfileComponent>().HasTag("personality_cautious"));
            Assert.IsTrue(b.Get<PersonalityProfileComponent>().HasTag("personality_cautious"));
            Assert.AreEqual(1, b.Get<PersonalityProfileComponent>().Count);
        }

        [Test]
        public void PlayableDay_ThreeCharacters_HaveDistinctPersonalityTags()
        {
            Assert.IsTrue(
                PlayableHostBootstrapPath(out var packageDir),
                "Content/BaseGame required");
            var started = new PlayableDayBootstrap().Start(packageDir);
            Assert.IsTrue(started.IsSuccess, started.IsFailure ? started.Error.ToString() : "");

            var ids = started.Value.CharacterIds;
            Assert.AreEqual(3, ids.Count);
            var tags = new System.Collections.Generic.List<string>();
            foreach (var id in ids)
            {
                Assert.IsTrue(started.Value.World.Entities.TryGet(id, out var e));
                var profile = e.Get<PersonalityProfileComponent>();
                Assert.Greater(profile.Count, 0, e.DisplayName);
                tags.Add(string.Join(",", profile.Tags));
            }

            Assert.AreNotEqual(tags[0], tags[1]);
            Assert.AreNotEqual(tags[0], tags[2]);
            Assert.AreNotEqual(tags[1], tags[2]);
        }

        static bool PlayableHostBootstrapPath(out string path)
        {
            path = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));
            return System.IO.Directory.Exists(path) &&
                   System.IO.File.Exists(System.IO.Path.Combine(path, "manifest.json"));
        }
    }
}
