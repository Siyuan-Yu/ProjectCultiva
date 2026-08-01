using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Data.Bootstrap;

namespace XianXia.Tests
{
    public sealed class OpeningRelationsPhaseCTests
    {
        [Test]
        public void Seeder_WritesMutualOpeningFavor()
        {
            var world = new SimulationWorld();
            var ids = new[]
            {
                world.Entities.CreateCharacter(new DefinitionId("base", "a"), "甲").Value.Id,
                world.Entities.CreateCharacter(new DefinitionId("base", "b"), "乙").Value.Id,
                world.Entities.CreateCharacter(new DefinitionId("base", "c"), "丙").Value.Id
            };

            Assert.IsTrue(OpeningRelationsSeeder.SeedCompanions(world, ids).IsSuccess);
            Assert.AreEqual(
                SocialAlphaConstants.OpeningCompanionFavor,
                world.Relationships.Score(ids[0], ids[1]));
            Assert.AreEqual(
                SocialAlphaConstants.OpeningCompanionFavor,
                world.Relationships.Score(ids[1], ids[0]));
            Assert.AreEqual(6, world.Relationships.EventCount); // 3 pairs × 2 directions
        }

        [Test]
        public void Help_And_Slight_GoThroughLedger()
        {
            var world = new SimulationWorld();
            var a = world.Entities.CreateCharacter(new DefinitionId("base", "a"), "甲").Value;
            var b = world.Entities.CreateCharacter(new DefinitionId("base", "b"), "乙").Value;
            var social = new SocialInteractionService();

            Assert.IsTrue(social.Help(world, a.Id, b.Id).IsSuccess);
            Assert.AreEqual(SocialAlphaConstants.HelpDelta, world.Relationships.Score(a.Id, b.Id));

            Assert.IsTrue(social.Slight(world, a.Id, b.Id).IsSuccess);
            Assert.AreEqual(
                SocialAlphaConstants.HelpDelta + SocialAlphaConstants.SlightDelta,
                world.Relationships.Score(a.Id, b.Id));
            Assert.AreEqual(
                SocialAlphaConstants.HelpDelta + SocialAlphaConstants.SlightDelta,
                a.Get<RelationshipComponent>().GetCachedToward(b.Id));
        }

        [Test]
        public void PlayableDay_SeedsOpeningRelations()
        {
            var package = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));
            Assert.IsTrue(System.IO.Directory.Exists(package));

            var started = new PlayableDayBootstrap().Start(package);
            Assert.IsTrue(started.IsSuccess, started.IsFailure ? started.Error.ToString() : "");

            var ids = started.Value.CharacterIds;
            Assert.AreEqual(3, ids.Count);
            Assert.AreEqual(
                SocialAlphaConstants.OpeningCompanionFavor,
                started.Value.World.Relationships.Score(ids[0], ids[1]));
            Assert.GreaterOrEqual(started.Value.World.Relationships.EventCount, 6);
        }
    }
}
