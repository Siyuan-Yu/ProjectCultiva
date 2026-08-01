using System.IO;
using System.Linq;
using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Social;
using XianXia.Data.Bootstrap;
using XianXia.Data.Content;

namespace XianXia.Tests
{
    /// <summary>VS0.7-E: content-only character／NPC／manual additions + scenario wiring.</summary>
    public sealed class ContentFoundationPhaseETests
    {
        static string BaseGamePath =>
            Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));

        [Test]
        public void Loader_LoadsScenario_AndContentOnlyDefs()
        {
            var loaded = new ContentPackageLoader().Load(new[] { BaseGamePath });
            Assert.IsTrue(loaded.IsSuccess, loaded.IsFailure ? loaded.Error.ToString() : "");

            var registry = loaded.Value.Registry;
            Assert.IsTrue(registry.TryGetOpeningScenario(
                ContentGameStart.DefaultPlayableScenarioId, out var scenario));
            Assert.AreEqual(4, scenario.Spawns.Count);
            Assert.GreaterOrEqual(scenario.OpeningRelations.Count, 3);

            Assert.IsTrue(registry.TryGetCharacter(
                new DefinitionId("base", "character_village_recruit"), out var recruit));
            Assert.IsTrue(recruit.PersonalityTags.Contains("personality_steady"));
            Assert.IsTrue(recruit.BackgroundTags.Contains("background_villager"));

            Assert.IsTrue(registry.TryGetCharacter(
                new DefinitionId("base", "character_herb_gatherer"), out var herb));
            Assert.IsTrue(herb.TalentTags.Contains("talent_herb_sense"));

            Assert.IsTrue(registry.TryGetCultivation(
                new DefinitionId("base", "cultivation_wood_whisper"), out var manual));
            Assert.AreEqual(15, manual.CultivationSpeed);
        }

        [Test]
        public void PlayableDay_RecruitableComesFromScenario_NotSoftCodedLaborDisciple()
        {
            var started = new PlayableDayBootstrap().Start(BaseGamePath);
            Assert.IsTrue(started.IsSuccess, started.IsFailure ? started.Error.ToString() : "");

            Assert.IsTrue(started.Value.World.Entities.TryGet(
                started.Value.RecruitableNpcId, out var npc));
            Assert.AreEqual(
                new DefinitionId("base", "character_village_recruit"),
                npc.DefinitionId);
            Assert.AreEqual("村内可招者", npc.DisplayName);
            Assert.IsFalse(npc.Get<FactionMembershipComponent>().IsAffiliated);
            Assert.IsTrue(npc.Get<PersonalityProfileComponent>().HasTag("personality_steady"));
            Assert.IsTrue(npc.Get<PersonalityProfileComponent>().HasTag("background_villager"));
        }

        [Test]
        public void PlayableDay_RegistersContentOnlyManual()
        {
            var started = new PlayableDayBootstrap().Start(BaseGamePath);
            Assert.IsTrue(started.IsSuccess, started.IsFailure ? started.Error.ToString() : "");
            Assert.IsTrue(started.Value.World.TryGetManual(
                new DefinitionId("base", "cultivation_wood_whisper"), out _));
        }

        [Test]
        public void CharacterDefinition_EnumerateProfileTags_MergesBuckets()
        {
            var def = new CharacterDefinition
            {
                PersonalityTags = { "personality_bold" },
                BackgroundTags = { "background_wanderer" },
                TalentTags = { "talent_fire_root" },
                Tags = { "legacy" }
            };
            CollectionAssert.AreEqual(
                new[] { "personality_bold", "background_wanderer", "talent_fire_root", "legacy" },
                def.EnumerateProfileTags().ToArray());
        }
    }
}
